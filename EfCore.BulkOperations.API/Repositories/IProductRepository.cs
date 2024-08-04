using EfCore.BulkOperations.API.Models;

namespace EfCore.BulkOperations.API.Repositories;

public interface IProductRepository
{
    public Task<List<Product>> GetProducts(bool trackChanges = false);
    public Task<Product?> GetProduct(Guid id);

    public Task<int> BulkInsertProducts(List<Product> products);
    public Task<int> BulkUpdateProducts(List<Product> products);
    public Task<int> BulkDeleteProducts(List<Product> products);
    public Task<int> BulkMergeProducts(List<Product> products);

    public Task SyncDataThenCommit(List<Product> list1, List<Product> list2);

    public Task SyncDataThenRollback(Product item1, List<Product> list2, List<Product> list3);
}