# EfCore.BulkOperations

`BulkInsertAsync`, `BulkUpdateAsync`, `BulkDeleteAsync` and `BulkMergeAsync` on your `DbContext`.
Each one builds a single parameterised statement from EF Core's own model metadata and sends it
straight to the connection, instead of going through the change tracker.

**MySQL and MariaDB only.** Identifiers are backtick-quoted in every statement, update and delete
use MySQL's multi-table `INNER JOIN` form, and `BulkMergeAsync` is built on
`ON DUPLICATE KEY UPDATE`. None of the four is portable to SQL Server or PostgreSQL.

[Source and full benchmark](https://github.com/hongjs/EfCore.BulkOperations) ·
[NuGet](https://www.nuget.org/packages/EfCore.BulkOperations)

---

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

### Bulk Merge

Insert rows that are new and update the ones that are not, in one statement.

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
| `BatchSize` | `500` | Rows per statement. See [Batch size](https://github.com/hongjs/EfCore.BulkOperations#batch-size) for how the number was chosen. |
| `CommandTimeout` | the provider's | Seconds before the command is abandoned. |
| `UniqueKeys` | the unique index in the model | Which columns update and delete match rows on. |
| `IgnoreOnInsert` | none | Columns to leave out of an insert, so the database's own default applies. |
| `IgnoreOnUpdate` | none | Columns to leave untouched by an update. |
| `SortByKeys` | `true` | Order rows by their keys before sending. Worth several times the speed on a large write — see [Why the rows are sorted](https://github.com/hongjs/EfCore.BulkOperations#why-the-rows-are-sorted-before-they-are-sent). Turn it off only if the rows must arrive in the order given. |

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

Against plain EF Core on the same data, schema and MySQL instance, at MySQL's default settings.
**Ratio** is this library's mean divided by EF Core's, so 0.26 means it took 26% of the time.

| Operation | 1,000 | 10,000 | 100,000 | 1,000,000 |
|-----------|------:|-------:|--------:|----------:|
| Delete | 0.18 | 0.16 | 0.18 | 0.33 |
| Update | 0.26 | 0.34 | 0.29 | 0.26 |
| Insert | 0.47 | 0.59 | 0.39 | 0.34 |
| Merge | 0.70 | 0.89 | 0.86 | 0.70 |

Allocation is lower everywhere too, most of all on delete, which sends only the key columns: 86 MB
against EF Core's 576 MB at a hundred thousand rows.

Merge is the narrowest margin because EF Core's baseline for it is already the fastest thing EF Core
can do - look the rows up in chunks, then update or add each one.

[Method, hardware, error bars and the batch-size sweep](https://github.com/hongjs/EfCore.BulkOperations#benchmark)
