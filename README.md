# EfCore.BulkOperations

EfCore.BulkOperations enables bulk operations like BulkInsert, BulkUpdate, BulkDelete, and BulkMerge. While most operations use simple SQL queries, BulkMerge requires a more complex approach.

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=hongjs_EfCore.BulkOperations&metric=coverage)](https://sonarcloud.io/summary/new_code?id=hongjs_EfCore.BulkOperations)

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
var connection = _dbContext.Database.GetDbConnection();
DbTransaction? transaction = null;
try
{
    transaction = await connection.BeginTransactionAsync();
    var insertItems = new List<Product> { new Product("Product1", 100m) };
    await _dbContext.BulkInsertAsync(insertItems, null, transaction);
    var updateItems = new List<Product> { new Product("Product2", 200m) };
    await _dbContext.BulkUpdateAsync(updateItems, null, transaction);
    await transaction.CommitAsync();
}
catch (Exception e)
{
    if (transaction is not null) await transaction.RollbackAsync();
}
finally
{
    if(transaction is not null) await transaction.DisposeAsync();
    await connection.CloseAsync();
}
```
