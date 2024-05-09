# EfCore.BulkOperations

EfCore.BulkOperations simplifies bulk operations like insert, update, and delete with efficient SQL queries compatible with most databases.

EfCore.BulkOperations Mapping columns from unique keys. You can configure custom column mapping if needed.

ps. BulkMerge works with MySQL only.

[Go to NuGet](https://www.nuget.org/packages/EfCore.BulkOperations)

---

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=coverage)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations)



## Example

### Bulk Insert
```csharp
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkInsertAsync(items);
```

```csharp
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkInsertAsync(
    items, 
    option => { option.IgnoreOnInsert = x => new { x.CreatedAt }; }
);
```

### Bulk Update
```csharp
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkUpdateAsync(items);
```

```csharp
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkUpdateAsync(
    items, 
    option => { option.IgnoreOnUpdate = x => new { x.CreatedAt }; }
);
```

```csharp
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkUpdateAsync(
    items, 
    option => { option.UniqueKeys = x => new { x.Id }; }
);
```

### Bulk Delete
```csharp
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkDeleteAsync(items);
```
```csharp
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkDeleteAsync(
    items, 
    option => { option.UniqueKeys = x => new { x.Id }; }
);
```



### Bulk Merge (MySql only)

Do not use BulkMergeAsync with other databases; it relies on a MySQL-specific query.

```csharp
var items = new List<Product> { new Product("Product1", 100m) };
await _dbContext.BulkMergeAsync(items);
```

```csharp
await _dbContext.BulkMergeAsync(
    items,
    option =>
    {
        option.IgnoreOnInsert = x => new { x.CreatedAt };
        option.IgnoreOnUpdate = x => new { x.CreatedAt };
    });
```

### Working with Global Transaction
EfCore.BulkOperations utilizes local transactions within bulk processes. If you require manual transaction control, you can pass an existing transaction into the bulk process.


```csharp
IDbContextTransaction? transaction = null;
DbConnection? connection = null;
try
{
    connection = _dbContext.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
    transaction = await _dbContext.Database.BeginTransactionAsync();
    var dbTransaction = transaction.GetDbTransaction();

    var insertItems = new List<Product> { new Product("Product1", 100m) };
    await _dbContext.BulkInsertAsync(insertItems, null, dbTransaction);
    var updateItems = new List<Product> { new Product("Product2", 200m) };
    await _dbContext.BulkUpdateAsync(updateItems, null, dbTransaction);

    await transaction.CommitAsync();
}
catch (Exception e)
{
    if (transaction is not null) await transaction.RollbackAsync();
}
finally
{
    if (connection is { State: ConnectionState.Open }) await connection.CloseAsync();
    if (transaction != null) await transaction.DisposeAsync();
}
```
