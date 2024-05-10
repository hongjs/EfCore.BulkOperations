using EfCore.BulkOperations.API.Models;

namespace EfCore.BulkOperations.API.Repositories;

public interface IProductRepository
{
    public Task<List<Product>> GetProducts();
    public Task<Product?> GetProduct(Guid id);

    public Task<int> InsertProducts(List<Product> products);
    public Task<int> UpdateProducts(List<Product> products);
    public Task<int> DeleteProducts(List<Product> products);
    public Task<int> MergeProducts(List<Product> products);

    public Task SyncDataThenCommit(List<Product> list1, List<Product> list2);

    public Task SyncDataThenRollback(Product item1, List<Product> list2, List<Product> list3);
}