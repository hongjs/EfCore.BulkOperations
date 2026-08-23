# EfCore.BulkOperations

EfCore.BulkOperations simplifies bulk operations like insert, update, and delete with efficient SQL queries compatible
with most databases.

EfCore.BulkOperations Mapping columns from unique keys. You can configure custom column mapping if needed.

ps. BulkMerge works with MySQL only.

[Go to NuGet](https://www.nuget.org/packages/EfCore.BulkOperations)

---

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=coverage)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations)

## Example

Every call takes a list and returns the number of rows affected. The examples below share one list:

```csharp
var products = new List<Product> { new("Product1", 100m) };
```

### Bulk Insert

```csharp
var rowsAffected = await dbContext.BulkInsertAsync(products);
```

```csharp
await dbContext.BulkInsertAsync(products, option =>
{
    option.BatchSize = 1000;
    option.CommandTimeout = 120;
    option.IgnoreOnInsert = x => new { x.CreatedAt };
});
```

### Bulk Update

```csharp
await dbContext.BulkUpdateAsync(products);
```

```csharp
// Leave a column as the database has it
await dbContext.BulkUpdateAsync(products, option => option.IgnoreOnUpdate = x => new { x.CreatedAt });

// Match rows on something other than the unique index found in the model
await dbContext.BulkUpdateAsync(products, option => option.UniqueKeys = x => new { x.Id });
```

### Bulk Delete

```csharp
await dbContext.BulkDeleteAsync(products);

await dbContext.BulkDeleteAsync(products, option => option.UniqueKeys = x => new { x.Id });
```

### Bulk Merge (MySQL only)

`BulkMergeAsync` is built on `ON DUPLICATE KEY UPDATE`. Do not use it against another database.

```csharp
await dbContext.BulkMergeAsync(products);
```

```csharp
await dbContext.BulkMergeAsync(products, option =>
{
    option.IgnoreOnInsert = x => new { x.CreatedAt };
    option.IgnoreOnUpdate = x => new { x.CreatedAt };
});
```

### Options

| Option | Default | What it does |
|---|---|---|
| `BatchSize` | `500` | Rows per statement. See [Batch size](#batch-size) for how the number was chosen. |
| `CommandTimeout` | the provider's | Seconds before the command is abandoned. |
| `UniqueKeys` | the unique index in the model | Which columns update and delete match rows on. |
| `IgnoreOnInsert` | none | Columns to leave out of an insert, so the database's own default applies. |
| `IgnoreOnUpdate` | none | Columns to leave untouched by an update. |
| `SortByKeys` | `true` | Order rows by their keys before sending. Worth several times the speed on a large write — see [Why the rows are sorted](#why-the-rows-are-sorted-before-they-are-sent). Turn it off only if the rows must arrive in the order given. |

### Sharing one transaction

Each call runs in its own transaction unless you hand it one. A transaction you pass in stays yours:
the library will not commit it, roll it back, or close the connection.

```csharp
var transaction = await dbContext.BeginTransactionAsync();

try
{
    await dbContext.Products.AddAsync(product);
    await dbContext.SaveChangesAsync();

    await dbContext.BulkInsertAsync(orders, null, transaction);
    await dbContext.BulkInsertAsync(logs, null, transaction);

    await dbContext.CommitAsync();
}
catch
{
    await dbContext.RollbackAsync();
    throw;
}
```

# Benchmark

`EfCore.BulkOperations` against plain EF Core, on the same data, the same schema and the same
MySQL instance. Lower is better; **Ratio** is the operation's mean divided by EF Core's mean, so
0.16 means it took 16% of the time EF Core needed.

## Environment

| | |
|---|---|
| BenchmarkDotNet | v0.14.0 |
| Runtime | .NET 8.0.4, Arm64 RyuJIT AdvSIMD |
| OS / CPU | macOS 26.5, Apple M2 Pro (10 physical cores) |
| Database | MySQL 8.0 in Docker on the same machine, **stock configuration** |
| EF Core | 8.0.4 with Pomelo.EntityFrameworkCore.MySql 8.0.2 |

The server is left at its defaults, including `innodb_buffer_pool_size=128M`. Nothing here needs a
tuned database to reproduce.

## Method

Anything that is not the operation under test is kept out of the measurement:

- **`RunStrategy.Monitoring`, 1 warm-up plus 15 measured iterations, one operation per iteration.**
  These operations write to a database, so they cannot be repeated inside one iteration without
  changing the data they work on.
- **A fresh `DbContext` per iteration.** Reusing one context leaves every previously saved entity in
  the change tracker, so EF Core's `DetectChanges` grows with each iteration and the benchmark stops
  measuring the operation at all.
- **Row generation and row loading happen in `[IterationSetup]`**, which BenchmarkDotNet excludes
  from the measurement.
- **Each iteration starts from a truncated table.** `DELETE` leaves the tablespace fragmented with
  InnoDB purge lagging behind, which slows the next iteration's inserts. Since the methods of a
  class run in a fixed order, that penalty always lands on the same one and reads as a difference
  between the two implementations. It is worth about 1.7x at half a million rows — enough to invert
  the result.
- **EF Core runs with its defaults**; `BulkOption.BatchSize` is set to 5,000 rather than left at
  its default of 500. 5,000 is the slower of the two — see [Batch size](#batch-size) — so on insert
  the tables below understate what the library does out of the box, by roughly 15% at 50,000 rows.

## Results

### Delete

The widest margin: BulkOperations sends only the key columns.

| Rows | EF Core | EfCore.BulkOperations | Ratio | Allocated (EF Core) | Allocated (Bulk) |
|-----:|--------:|----------------------:|------:|--------------------:|-----------------:|
| 1,000 | 100.0 ms ± 41.3 | 15.6 ms ± 2.3 | **0.18** | 6.0 MB | 1.2 MB |
| 10,000 | 576.0 ms ± 69.5 | 91.7 ms ± 9.8 | **0.16** | 58.1 MB | 9.8 MB |
| 100,000 | 5.60 s ± 0.19 | 982.9 ms ± 31.0 | **0.18** | 575.8 MB | 86.2 MB |
| 1,000,000 | 85.31 s ± 6.54 | 28.06 s ± 4.06 | **0.33** | 6.06 GB | 855.1 MB |

### Update

EF Core has to materialise and track every row before it can write it back.

| Rows | EF Core | EfCore.BulkOperations | Ratio | Allocated (EF Core) | Allocated (Bulk) |
|-----:|--------:|----------------------:|------:|--------------------:|-----------------:|
| 1,000 | 113.2 ms ± 15.4 | 29.1 ms ± 3.1 | **0.26** | 11.6 MB | 4.8 MB |
| 10,000 | 634.8 ms ± 138.3 | 212.6 ms ± 18.4 | **0.34** | 107.0 MB | 43.7 MB |
| 100,000 | 6.57 s ± 0.17 | 1.87 s ± 0.04 | **0.29** | 1.04 GB | 408.0 MB |
| 1,000,000 | 85.07 s ± 14.97 | 22.15 s ± 1.92 | **0.26** | 10.51 GB | 3.79 GB |

### Insert

EF Core batches inserts well, which makes this one of the narrower margins.

| Rows | EF Core | EfCore.BulkOperations | Ratio | Allocated (EF Core) | Allocated (Bulk) |
|-----:|--------:|----------------------:|------:|--------------------:|-----------------:|
| 1,000 | 98.2 ms ± 9.1 | 45.5 ms ± 7.5 | **0.47** | 10.6 MB | 3.9 MB |
| 10,000 | 362.1 ms ± 165.9 | 196.0 ms ± 63.2 | **0.59** | 99.6 MB | 33.7 MB |
| 100,000 | 3.79 s ± 0.04 | 1.48 s ± 0.03 | **0.39** | 988.5 MB | 335.9 MB |
| 1,000,000 | 49.64 s ± 9.40 | 16.61 s ± 9.08 | **0.34** | 9.59 GB | 3.10 GB |

### Merge

EF Core has no upsert, so the baseline looks the rows up in chunks of 1,000 and then updates or adds
each one. That is the fastest thing EF Core can do here, and it is why this is the narrowest margin.

| Rows | EF Core | EfCore.BulkOperations | Ratio | Allocated (EF Core) | Allocated (Bulk) |
|-----:|--------:|----------------------:|------:|--------------------:|-----------------:|
| 1,000 | 68.3 ms ± 5.9 | 47.3 ms ± 8.6 | **0.70** | 7.0 MB | 4.4 MB |
| 10,000 | 250.8 ms ± 108.5 | 204.8 ms ± 16.7 | **0.89** | 64.8 MB | 43.6 MB |
| 100,000 | 2.61 s ± 0.19 | 2.24 s ± 0.03 | **0.86** | 641.0 MB | 413.6 MB |
| 1,000,000 | 38.60 s ± 5.34 | 26.94 s ± 3.85 | **0.70** | 6.21 GB | 3.81 GB |

`±` is half of the 99.9% confidence interval, as BenchmarkDotNet reports it. The million-row rows
are measured over 5 iterations rather than 15, because a single one of them runs for up to a
minute and a half.

## Why the rows are sorted before they are sent

An earlier version of this benchmark had no million-row rows to publish, because at that size the
library lost — 127.7 s against EF Core's 50.3 s on an insert. Everything cheap to blame was
ruled out: SQL generation accounted for 2.7 s of a 145 s run, GC pauses for under 4%,
`performance_schema` put the time in statement execution, and hand-written ADO.NET issuing the same
statements was just as slow. Statement size was not it either — forcing `BatchSize` to 42 so that
both sides sent exactly the same 23,810 statements left the gap where it was.

What was left was the order of the rows. EF Core sorts its commands by primary key; the library sent
them in whatever order the caller had. InnoDB stores rows in primary key order, so a million rows
with random `Guid` keys arriving unsorted write across the whole clustered index, and once that
index no longer fits in the buffer pool each row costs a page read. Sorting the rows first turned
that insert from 127.7 s into 16.6 s on the same stock server.

That is `BulkOption.SortByKeys`, on by default. The sort is stable, so rows whose keys compare equal
keep the order they were given in — which is what a merge relies on when the same key appears twice
and the last one has to win. Turn it off if the rows must reach the database in the order they were
passed.

## Batch size

`BulkOption.BatchSize` swept over a 50,000 row insert. This is the one measurement above that
the comparison tables do not use: they run at 5,000.

| BatchSize | Mean | Allocated |
|----------:|-----:|----------:|
| 200 | 823.1 ms ± 40.3 | 154.9 MB |
| 500 (the default) | 694.3 ms ± 22.3 | 149.2 MB |
| 1,000 | 698.5 ms ± 24.4 | 153.9 MB |
| 2,000 | 782.2 ms ± 21.8 | 162.6 MB |
| 5,000 | 793.7 ms ± 24.7 | 167.8 MB |
| 10,000 | 806.7 ms ± 19.6 | 170.7 MB |

This is the sweep that moved the default, which used to be 200 — the slowest setting measured. The
curve is a shallow trough rather than a plateau: 500 and 1,000 are the fastest, about 15% below 200,
and from there the numbers climb back until 10,000 has given most of the gain away. Allocation rises
with the batch size throughout, since a larger batch means a larger statement and more parameters
alive at once, so 500 is the cheaper of the two fastest settings.

Row width moves the optimum, and this is one entity of six narrow columns. A table carrying long
strings reaches the same statement size in fewer rows, so treat 500 as a starting point rather than
a finding.

## Reproducing

```sh
docker compose up -d

export BENCHMARK_MYSQL="server=localhost;port=3306;database=test_db;user=root;password=root"
dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter 'EfCore.BulkOperations.Benchmark.Bulk*'
```

The schema is created on first run. `BENCHMARK_ROWS` and `BENCHMARK_ITERATIONS` override the row
counts and the iteration count, and `--filter` selects a suite:

```sh
BENCHMARK_ROWS=1000000 BENCHMARK_ITERATIONS=5 \
  dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter '*BulkInsertTest*'

dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter '*BatchSizeTest*'
```

A full run writes and deletes tens of millions of rows. The compose file expires binary logs quickly
so a long run cannot fill the disk out from under the server.

## Reading these numbers

- They are **relative**, not absolute. The database is local, so there is no network latency between
  the application and MySQL. A remote database moves both columns and narrows the gap on the
  operations whose advantage comes from sending less data.
- The advantage is **not uniform**. Delete is where it is largest and merge is where it is smallest,
  so a codebase that mostly merges should expect the merge numbers, not the delete ones.
- **Some cells are noisy, in both columns.** EF Core's 10,000-row insert is ± 166 ms on a 362 ms
  mean and its merge ± 108 ms on 251 ms. The library's own worst cell is the million-row insert:
  ± 9.1 s on a 16.6 s mean, wider in proportion than anything EF Core does. Read the ratios where
  the error bars are tight — every 100,000-row row here is within a few percent — and treat the
  noisy cells as direction rather than measurement.
