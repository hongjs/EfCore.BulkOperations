# EfCore.BulkOperations

EfCore.BulkOperations simplifies bulk operations like insert, update, and delete with efficient SQL queries compatible
with most databases.

EfCore.BulkOperations Mapping columns from unique keys. You can configure custom column mapping if needed.

ps. BulkMerge works with MySQL only.

[Go to NuGet](https://www.nuget.org/packages/EfCore.BulkOperations)

---

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=coverage)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations)

## Example

### Bulk Insert

```js
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkInsertAsync(items);
```

```js
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkInsertAsync(
    items, 
    option =>
    {
        option.BatchSize = 1000;
        option.CommandTimeout = 120;
        option.IgnoreOnInsert = x => new { x.CreatedAt };
    }
);
```

### Bulk Update

```js
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkUpdateAsync(items);
```

```js
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkUpdateAsync(
    items, 
    option => { option.IgnoreOnUpdate = x => new { x.CreatedAt }; }
);
```

```js
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkUpdateAsync(
    items, 
    option => { option.UniqueKeys = x => new { x.Id }; }
);
```

### Bulk Delete

```js
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkDeleteAsync(items);
```

```js
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkDeleteAsync(
    items, 
    option => { option.UniqueKeys = x => new { x.Id }; }
);
```

### Bulk Merge (MySql only)

Do not use BulkMergeAsync with other databases; it relies on a MySQL-specific query.

```js
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkMergeAsync(items);
```

```js
await _dbContext.BulkMergeAsync(
    items,
    option =>
    {
        option.IgnoreOnInsert = x => new { x.CreatedAt };
        option.IgnoreOnUpdate = x => new { x.CreatedAt };
    });
```

### Working with Global Transaction

EfCore.BulkOperations utilizes local transactions within bulk processes. If you require manual transaction control, you
can pass an existing transaction into the bulk process.

```js
try
{
    var dbTransaction = dbContext.BeginTransactionAsync();

    await dbContext.Products.AddAsync (item1);
    await dbContext.SaveChangesAsync();
    await dbContext.BulkInsertAsync(list2, null, dbTransaction);
    await dbContext.BulkInsertAsync(list3, null, dbTransaction);

    await dbContext.CommitAsync();
}
catch (Exception)
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
| Database | MySQL 8.0 in Docker on the same machine, `innodb_buffer_pool_size=2G` |
| EF Core | 8.0.4 with Pomelo.EntityFrameworkCore.MySql 8.0.2 |

The buffer pool size is part of the result, not a footnote — see
[The database has to be sized for the data set](#the-database-has-to-be-sized-for-the-data-set).

## Method

Anything that is not the operation under test is kept out of the measurement:

- **`RunStrategy.Monitoring`, 1 warm-up plus 15 measured iterations, one operation per iteration**
  (5 iterations at one million rows, where a single iteration runs for up to a minute). These
  operations write to a database, so they cannot be repeated inside one iteration without changing
  the data they work on.
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
- **EF Core runs with its defaults**; `BulkOption.BatchSize` is set to 5,000.

## Results


### Insert

EF Core batches inserts well, which makes this one of the narrower margins.

| Rows | EF Core | EfCore.BulkOperations | Ratio | Allocated (EF Core) | Allocated (Bulk) |
|-----:|--------:|----------------------:|------:|--------------------:|-----------------:|
| 1,000 | 84.4 ms ± 10.3 | 37.5 ms ± 10.6 | **0.45** | 10.3 MB | 3.9 MB |
| 10,000 | 362.1 ms ± 184.1 | 207.2 ms ± 33.8 | **0.63** | 99.7 MB | 33.1 MB |
| 100,000 | 3.78 s ± 0.08 | 1.86 s ± 0.06 | **0.49** | 988.4 MB | 318.6 MB |
| 1,000,000 | 44.26 s ± 10.95 | 25.67 s ± 1.18 | **0.58** | 9.58 GB | 3.07 GB |

### Update

EF Core has to materialise and track every row before it can write it back.

| Rows | EF Core | EfCore.BulkOperations | Ratio | Allocated (EF Core) | Allocated (Bulk) |
|-----:|--------:|----------------------:|------:|--------------------:|-----------------:|
| 1,000 | 115.7 ms ± 12.8 | 28.5 ms ± 3.3 | **0.25** | 11.4 MB | 4.7 MB |
| 10,000 | 616.3 ms ± 104.6 | 203.5 ms ± 48.0 | **0.34** | 109.7 MB | 43.1 MB |
| 100,000 | 6.49 s ± 0.25 | 1.83 s ± 0.02 | **0.28** | 1.04 GB | 396.3 MB |
| 1,000,000 | 68.70 s ± 6.47 | 19.56 s ± 0.26 | **0.28** | 10.42 GB | 3.75 GB |

### Delete

The widest margin: BulkOperations sends only the key columns.

| Rows | EF Core | EfCore.BulkOperations | Ratio | Allocated (EF Core) | Allocated (Bulk) |
|-----:|--------:|----------------------:|------:|--------------------:|-----------------:|
| 1,000 | 75.3 ms ± 12.1 | 15.0 ms ± 3.6 | **0.20** | 6.6 MB | 1.2 MB |
| 10,000 | 546.3 ms ± 97.0 | 90.2 ms ± 16.4 | **0.17** | 59.5 MB | 9.3 MB |
| 100,000 | 5.50 s ± 0.24 | 896.5 ms ± 16.8 | **0.16** | 576.4 MB | 82.9 MB |
| 1,000,000 | 62.92 s ± 3.85 | 12.50 s ± 1.04 | **0.20** | 5.71 GB | 796.1 MB |

### Merge

EF Core has no upsert, so the baseline looks the rows up in chunks of 1,000 and then updates or adds each one.

| Rows | EF Core | EfCore.BulkOperations | Ratio | Allocated (EF Core) | Allocated (Bulk) |
|-----:|--------:|----------------------:|------:|--------------------:|-----------------:|
| 1,000 | 64.3 ms ± 6.6 | 27.3 ms ± 10.8 | **0.43** | 7.0 MB | 4.4 MB |
| 10,000 | 239.8 ms ± 86.7 | 190.4 ms ± 28.1 | **0.85** | 65.0 MB | 43.1 MB |
| 100,000 | 2.43 s ± 0.06 | 2.07 s ± 0.04 | **0.85** | 641.1 MB | 422.5 MB |
| 1,000,000 | 30.07 s ± 2.53 | 25.59 s ± 0.79 | **0.85** | 6.21 GB | 3.69 GB |

`±` is half of the 99.9% confidence interval, as BenchmarkDotNet reports it.

## Batch size

`BulkOption.BatchSize` swept over a 50,000 row insert:

| BatchSize | Mean | Allocated |
|----------:|-----:|----------:|
| 200 (the default) | 1,137.4 ms ± 41.2 | 152.1 MB |
| 500 | 923.6 ms ± 26.1 | 146.5 MB |
| 1,000 | 908.4 ms ± 45.9 | 150.4 MB |
| 2,000 | 938.3 ms ± 39.6 | 157.9 MB |
| 5,000 | 930.9 ms ± 23.6 | 161.9 MB |
| 10,000 | 966.8 ms ± 23.2 | 162.2 MB |

The current default of 200 is the slowest setting measured. Everything from 500 upwards lands
within 6% of everything else, so the choice above 500 barely matters — but raising it off the
default is worth about 20%.

## The database has to be sized for the data set

The first version of this benchmark reported that `BulkOperations` was **3.7x slower** than EF Core
at one million rows. That number was repeatable, and it was not about the library:

| 1,000,000 row insert | EF Core | EfCore.BulkOperations |
|---|--------:|----------------------:|
| `innodb_buffer_pool_size` = 128 MB (MySQL's default) | 51.5 s | 132.7 s |
| `innodb_buffer_pool_size` = 2 GB | 51.5 s | **26.8 s** |

Past roughly half a million rows the working set stops fitting in a 128 MB buffer pool. A multi-row
`INSERT` carrying thousands of rows then spends most of its time waiting on page flushes, while EF
Core — sending 42 rows per statement and spending most of its wall time on round trips and change
tracking — never pushes the server hard enough to run into it. Sizing the pool for the data set
removes the effect completely and leaves EF Core's numbers untouched.

That it is the server and not the client was checked directly: SQL generation accounted for 2.7 s
of a 145 s run, GC pauses for under 4%, `performance_schema` attributed the time to statement
execution on the server, and hand-written ADO.NET issuing the same statements was just as slow.

Two things follow. Measurements at this scale describe the server's configuration as much as the
client's code, so the configuration belongs next to the numbers. And if you are loading millions of
rows into a MySQL instance left on its defaults, that is worth fixing before reaching for any
library.

## Reproducing

```sh
docker compose up -d

export BENCHMARK_MYSQL="server=localhost;port=3306;database=test_db;user=root;password=root"
dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter 'EfCore.BulkOperations.Benchmark.Bulk*'
```

The compose file pins the MySQL version and buffer pool used above; give the container more memory
than the pool, or MySQL is OOM-killed mid-run. The schema is created on first run. `BENCHMARK_ROWS`
and `BENCHMARK_ITERATIONS` override the row counts and the iteration count, and `--filter` selects
a suite:

```sh
BENCHMARK_ROWS=1000000 BENCHMARK_ITERATIONS=5 \
  dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter '*BulkInsertTest*'

dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter '*BatchSizeTest*'
```

## Reading these numbers

- They are **relative**, not absolute. The database is local, so there is no network latency between
  the application and MySQL. A remote database moves both columns and narrows the gap on the
  operations whose advantage comes from sending less data.
- The advantage is **not uniform**. Delete is where it is largest and merge is where it is smallest,
  so a codebase that mostly merges should expect the merge numbers, not the delete ones.
- EF Core's 10,000-row measurements are the noisy ones — insert ± 184 ms on a 362 ms mean, merge
  ± 87 ms on a 240 ms mean. The BulkOperations column is stable at every size.
