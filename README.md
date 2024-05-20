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
IDbContextTransaction? transaction = null;
DbConnection? connection = null;
try
{
    connection = dbContext.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
    transaction = await dbContext.Database.BeginTransactionAsync();
    var dbTransaction = transaction.GetDbTransaction();

    dbContext.Products.Add(item1);
    await dbContext.SaveChangesAsync();
    await dbContext.BulkInsertAsync(list2, null, dbTransaction);
    await dbContext.BulkInsertAsync(list3, null, dbTransaction);

    throw new DbUpdateException("Internal Server Error");
}
catch (Exception)
{
    if (transaction is not null) await transaction.RollbackAsync();
    throw;
}
finally
{
    if (connection is { State: ConnectionState.Open }) await connection.CloseAsync();
}
```