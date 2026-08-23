# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A NuGet library (`net8.0`, EF Core 8.0.4) that adds `BulkInsertAsync` / `BulkUpdateAsync` /
`BulkDeleteAsync` / `BulkMergeAsync` extension methods to `DbContext`. It builds raw parameterised
SQL from EF Core's model metadata instead of going through the change tracker.

The generated SQL is **MySQL-flavoured throughout** — identifiers are backtick-quoted and
`BulkMergeAsync` uses `ON DUPLICATE KEY UPDATE`. Keep that in mind for any change to SQL generation.

## Commands

```sh
make build          # dotnet build -c Release
make test           # dotnet test (needs Docker: the integration tests start MySQL)
make format         # dotnet format
```

Single test:

```sh
dotnet test EfCore.BulkOperations.Test --filter FullyQualifiedName~Should_GenerateInsertScript
dotnet test EfCore.BulkOperations.Test --filter "Category=Unit"   # the tests that need no database
```

Migrations (the context lives in the API project):

```sh
make migrate-db name=SomeName
make update-db
```

Benchmarks — need a MySQL instance:

```sh
docker compose up -d
export BENCHMARK_MYSQL="server=localhost;port=3306;database=test_db;user=root;password=root"
dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter 'EfCore.BulkOperations.Benchmark.Bulk*'
```

`BENCHMARK_ROWS` and `BENCHMARK_ITERATIONS` override the row counts and iteration count. The schema
is created on first run.

## Projects

- **EfCore.BulkOperations** — the shipped library, and the only project packed to NuGet. `Version`
  lives in its `.csproj`; `Docs/README.md` is the package readme, separate from the root one.
- **EfCore.BulkOperations.API** — a minimal ASP.NET host with `ApplicationDbContext`, sample
  entities (`Product`, `Order`, `Log`), repositories and the EF migrations. It has no controllers:
  it exists as the **test host** for `WebApplicationFactory<Program>` and as a realistic consumer of
  the library. `Program` is deliberately `public abstract` so the test project can reference it, and
  `IPlaceHolderForAssemblyReference` exists so the migrations assembly resolves by name.
- **EfCore.BulkOperations.Test** — xUnit. Mostly integration tests against a real MySQL.
- **EfCore.BulkOperations.Benchmark** — BenchmarkDotNet, library vs plain EF Core.

## Library architecture

Three layers, one file each:

1. `DbContextExtensions.cs` — the public surface. A thin pass-through to `EfCoreBulkUtils`, plus
   `BeginTransactionAsync` / `CommitAsync` / `RollbackAsync` for callers who want several bulk calls
   in one transaction.
2. `EfCoreBulkUtils.cs` (internal) — materialises `BulkOption<T>` from the caller's `optionFactory`,
   asks `BulkCommand` for `IEnumerable<BatchData>`, then executes each batch on the raw
   `DbConnection`. **Ownership rule**: a `DbTransaction` passed in, or one the context already has
   open (`Database.CurrentTransaction`, picked up automatically), is neither committed nor rolled
   back here; only a locally-created transaction is. Likewise the connection is closed only if this
   code opened it — one the caller opened is left open.
3. `BulkCommand.cs` (internal) — all SQL generation. `GetEntityInfo<T>` reads EF Core metadata into
   `EntityInfo`/`ColumnInfo` records: column name vs CLR property name (`RefName`), key and
   unique-index flags, and the property's `ValueConverter`. Batches are `yield return`ed lazily and
   chunked by `BulkOption.BatchSize`. Parameters are named `@p{rowIndex}_{colIndex}` and values are
   always parameterised, never inlined.

`InternalsVisibleTo("EfCore.BulkOperations.Test")` sits at the top of `BulkCommand.cs`, which is how
the tests assert on generated SQL directly.

### SQL shapes

- **Insert** → `INSERT INTO ... VALUES (...),(...)` built by `ToInsertTemp`.
- **Update / Delete / Merge** → an inline derived table
  ``(SELECT @p.. AS `Col`, .. UNION ALL SELECT ..) AS tmp`` built by `ToTempTable`, then
  `UPDATE tb INNER JOIN tmp ON <keys>`, `DELETE tb ... INNER JOIN tmp`, or
  `INSERT ... SELECT ... FROM tmp ON DUPLICATE KEY UPDATE`.

`ToTempTable` appends a `zRowNo` column. It is not there to make rows distinct — `UNION ALL` keeps
duplicates anyway — it terminates the column list so the builder never has to trim a trailing comma.
That trimming is exactly what used to produce invalid SQL under CRLF, so leave it alone unless you
replace the separator logic too.

### Separators are written before items, never trimmed after

Several generators used to append `"...,"` and then remove the comma with
`Remove(Length - 2, 1)`. That offset assumes a one-character newline, so under CRLF it removed the
`\r` and left the comma — every statement was invalid SQL on Windows. Separators are now written
*before* each item. Keep it that way; `Should_NotEndStatementWithTrailingComma` guards the invariant
and CI runs the SQL tests on `windows-latest`, which is the only place the bug can reproduce.

### Rows are ordered by key before batching

`BulkOption.SortByKeys` (default `true`) sorts rows by their key columns — the primary key for
insert and merge, the resolved unique keys for update and delete. InnoDB stores rows in primary key
order, so an unordered load with random `Guid` keys writes across the whole clustered index; once
that index outgrows the buffer pool every row costs a page read. On a million-row insert against a
stock MySQL this is the difference between 127.7 s and 16.6 s. EF Core sorts its own commands the
same way.

The sort is stable, which is what keeps it semantically invisible: rows whose keys compare equal —
database-generated keys still at their default value, duplicate keys in a merge where the last one
has to win — keep the order they were given in. `KeyComparer` treats values it cannot compare as
equal rather than throwing.

### Key resolution

Update and delete need a unique key, resolved by `BulkCommand.ResolveKeys` in this order:
`BulkOption.UniqueKeys` if set (a name that is not a mapped property is an `ArgumentException`),
otherwise columns with `IsUniqueIndex`, otherwise the primary key. Only a keyless entity throws
`MissingPrimaryKeyException`. Key columns always travel in the derived table even when
`IgnoreOnUpdate` names them — the JOIN needs them; ignoring a key only keeps it out of `SET`. An
update with nothing left to `SET` is an `InvalidOperationException`; a merge with nothing to update
assigns a key to itself (`ON DUPLICATE KEY UPDATE t.Id = t.Id`), MySQL's insert-if-absent idiom.

Entities with a shadow property (navigation-only FK, TPH discriminator, `Property<T>("Name")`) are
rejected with `NotSupportedException` in `GetEntityInfo`: values are read from CLR properties by
reflection and a shadow property has none, so it used to be sent as NULL silently.

### The expression options are read from the expression tree

`BulkOption`'s `IgnoreOnInsert` / `IgnoreOnUpdate` / `UniqueKeys` are `Expression<Func<T, object>>`.
`GetExpressionFields` accepts exactly two shapes — `x => x.Name` and `x => new { x.Name, x.Price }`
(each member must be a property of the lambda parameter; boxing `Convert` nodes are unwrapped) —
and throws `ArgumentException` for anything else. It used to compile and run the expression
against a blank instance and read the result's properties, which meant `x => x.CreatedAt` yielded
DateTime's Year/Month/... and ignored nothing, silently.

## Testing

Integration tests spin up MySQL 8.0 through **Testcontainers**, so **Docker must be running** for
`dotnet test`. They derive from `BaseIntegrationTest` and share one container through the
`DatabaseTestCollection` fixture; **Respawn** wipes data after each test. The schema is created with
`EnsureCreated`, not migrations.

Tests that only need EF Core's model use `ModelOnlyDbContext` — it pins the server version instead
of calling `ServerVersion.AutoDetect`, which would open a connection — and are tagged
`[Trait("Category", "Unit")]` so CI can run them where Docker is unavailable.

`BulkCommandTest` asserts on exact generated SQL, line endings normalised. Changing SQL formatting
will break it; update the expected strings deliberately.

## Benchmarks

`BaseTest` enforces two rules that make the numbers mean anything, and both are easy to undo by
accident:

- **A fresh `DbContext` per iteration.** Sharing one leaves every saved entity in the change
  tracker, so EF Core degrades as the run goes on and the benchmark stops measuring the operation.
- **Iterations start from a truncated table.** `DELETE` leaves the tablespace fragmented with purge
  lagging, which slows the next iteration's inserts; since BenchmarkDotNet runs a class's methods in
  a fixed order, that penalty lands on the same method every time.

Row generation and row loading belong in `[IterationSetup]`, never inside a `[Benchmark]` method.
The config uses `RunStrategy.Monitoring` because an operation that writes to a database cannot be
repeated inside one iteration.

## CI

- `ci.yml` — lint, tests, and Windows SQL tests on every pull request to `main`. Needs no secrets.
- `sonarcloud.yml` — analysis on every pull request and on pushes to `main`. Needs `SONAR_TOKEN`.
- `publish_nuget.yml` — packs and tests on every run; pushes to NuGet only on a published GitHub
  release. **The published version is the release tag**, passed as `-p:Version`, so there is nothing
  to bump in the csproj — the `<Version>` there is only the default for a local `dotnet pack`. Only
  the library is packed; the benchmark project is `IsPackable=false` so a release cannot publish it.
