# EfCore.BulkOperations


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
