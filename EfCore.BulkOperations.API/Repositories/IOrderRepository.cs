using EfCore.BulkOperations.API.Models;

namespace EfCore.BulkOperations.API.Repositories;

public interface IOrderRepository
{
    public Task<List<Order>> GetOrders(bool trackChanges = false);

    public Task<int> BulkInsertOrders(List<Order> orders);
    public Task<int> BulkUpdateOrders(List<Order> orders);

    public Task<int> InsertOrders(List<Order> orders);
    public Task<int> UpdateOrders(List<Order> orders);

    public Task<int> DeleteAllOrders();
}