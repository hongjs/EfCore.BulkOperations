using EfCore.BulkOperations.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.API.Repositories;

public class OrderRepository(ApplicationDbContext dbContext) : IOrderRepository
{
    public async Task<List<Order>> GetOrders(bool trackChanges = false)
    {
        var query = trackChanges ? dbContext.Orders.AsQueryable() : dbContext.Orders.AsNoTracking();
        return await query.ToListAsync();
    }

    public async Task<int> BulkInsertOrders(List<Order> orders)
    {
        var rowAffected = await dbContext.BulkInsertAsync(
            orders,
            o =>
            {
                o.UniqueKeys = x => new { x.Id };
                o.BatchSize = 1000;
                o.CommandTimeout = 120;
            });
        return rowAffected;
    }

    public async Task<int> BulkUpdateOrders(List<Order> orders)
    {
        var rowAffected = await dbContext.BulkUpdateAsync(
            orders,
            o =>
            {
                o.UniqueKeys = x => new { x.Id };
                o.BatchSize = 1000;
                o.CommandTimeout = 120;
            }
        );
        return rowAffected;
    }


    public async Task<int> InsertOrders(List<Order> orders)
    {
        await dbContext.Orders.AddRangeAsync(orders);
        var rowAffected = await dbContext.SaveChangesAsync();
        return rowAffected;
    }

    public async Task<int> UpdateOrders(List<Order> orders)
    {
        dbContext.Orders.UpdateRange(orders);
        var rowAffected = await dbContext.SaveChangesAsync();
        return rowAffected;
    }

    public async Task<int> DeleteAllOrders()
    {
        var rowAffected = await dbContext.Orders.ExecuteDeleteAsync();
        await dbContext.SaveChangesAsync();
        return rowAffected;
    }
}