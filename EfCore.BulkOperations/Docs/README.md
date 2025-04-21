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

    throw new DbUpdateException("Some error occurs");
    await dbTransaction.CommitAsync();
}
catch (Exception)
{
    await dbContext.RollbackAsync();
    throw;
}
```

# Benchmark

Environment: `MySql v9.3.0` and `EfCore 8.0.4`

### Insert performance

```css
| Insert        | Row     |  Mean      |   Error     | StdDev     |  Median    |    Allocated |
|-------------- |--------:|-----------:|------------:|-----------:|-----------:|-------------:|
| EfCore        |    1000 |   86.59 ms |    3.516 ms |  10.089 ms |   85.07 ms |     93.88 MB |
| BulkOperation |    1000 |   35.06 ms |    3.327 ms |   9.809 ms |   30.61 ms |      3.31 MB |
| EfCore        |   10000 |  794.71 ms |   46.966 ms | 137.744 ms |  795.53 ms |    945.18 MB |
| BulkOperation |   10000 |  290.38 ms |    5.805 ms |  14.131 ms |  289.01 ms |     31.90 MB |
| EfCore        |  100000 |     7.45 s |     0.459 s |    1.345 s |     7.30 s |   9598.30 MB |
| BulkOperation |  100000 |     3.06 s |     0.058 s |    0.057 s |     3.06 s |    315.46 MB |
| EfCore        | 1000000 |   108.60 s |   242.160 s |   37.500 s |    93.00 s |     21.42 GB |
| BulkOperation | 1000000 |   146.40 s |      2.16 s |    0.540 s |   147.00 s |      3.07 GB |
```

### Update performance

```css
| Update        |  Row    | Mean       | Error       | StdDev     | Median     |    Allocated |
|-------------- |--------:|-----------:|------------:|-----------:|-----------:|-------------:|
| EfCore        |    1000 |  74.143 ms |  28.9783 ms |  1.5884 ms |  74.336 ms |  11601.60 KB |
| BulkOperation |    1000 |  27.199 ms |   6.5049 ms |  0.3566 ms |  27.013 ms |   4882.72 KB |
| EfCore        |   10000 | 743.882 ms | 115.1237 ms |  6.3103 ms | 745.372 ms | 116387.54 KB |
| BulkOperation |   10000 | 267.868 ms | 291.6891 ms | 15.9885 ms | 276.343 ms |  54097.69 KB |
| EfCore        |  100000 |    8.495 s |     1.063 s |   0.0583 s |    8.488 s |   1124.82 MB |
| BulkOperation |  100000 |    3.347 s |     1.370 s |   0.0751 s |    3.354 s |    465.16 MB |
| EfCore        | 1000000 |   116.98 s |    170.89 s |    9.367 s |   112.62 s |     10.89 GB |
| BulkOperation | 1000000 |    57.61 s |     48.31 s |    2.648 s |    56.62 s |      4.13 GB |
```

### Delete performance

```css
| Delete        |  Row    | Mean       | Error       | StdDev     | Median     |    Allocated |
|-------------- |--------:|-----------:|------------:|-----------:|-----------:|-------------:|
| EfCore        |    1000 |   98.91 ms |   321.45 ms |  17.620 ms |   91.17 ms |       5.8 MB |
| BulkOperation |    1000 |   25.97 ms |    75.30 ms |   4.127 ms |   25.11 ms |      1.14 MB |
| EfCore        |   10000 |  651.26 ms |   848.94 ms |  46.533 ms |  654.91 ms |     56.12 MB |
| BulkOperation |   10000 |  130.54 ms |   105.78 ms |   5.798 ms |  130.89 ms |       9.7 MB |
| EfCore        |  100000 |     7.55 s |     10.36 s | 567.950 ms |    7.540 s |    560.51 MB |
| BulkOperation |  100000 |     1.52 s |      0.95 s |  52.065 ms |    1.549 s |     80.36 MB |
| EfCore        | 1000000 |    72.93 s |     40.54 s |    2.222 s |    73.30 s |   5649.52 MB |
| BulkOperation | 1000000 |    18.05 s |     37.30 s |    2.045 s |    19.18 s |    785.01 MB |
```