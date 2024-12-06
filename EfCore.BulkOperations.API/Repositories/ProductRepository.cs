using EfCore.BulkOperations.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.API.Repositories;

public class ProductRepository(ApplicationDbContext dbContext) : IProductRepository
{
    public async Task<List<Product>> GetProducts(bool trackChanges = false)
    {
        var query = trackChanges ? dbContext.Products.AsQueryable() : dbContext.Products.AsNoTracking();
        return await query.ToListAsync();
    }

    public async Task<Product?> GetProduct(Guid id)
    {
        return await dbContext
            .Products
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> BulkInsertProducts(List<Product> products)
    {
        var rowAffected = await dbContext.BulkInsertAsync(
            products,
            o => { o.UniqueKeys = x => new { x.Id }; });
        return rowAffected;
    }

    public async Task<int> BulkUpdateProducts(List<Product> products)
    {
        var rowAffected = await dbContext.BulkUpdateAsync(
            products,
            o => { o.UniqueKeys = x => new { x.Id }; }
        );
        return rowAffected;
    }

    public async Task<int> BulkDeleteProducts(List<Product> products)
    {
        var rowAffected = await dbContext.BulkDeleteAsync(
            products,
            option => { option.UniqueKeys = x => new { x.Id }; });
        return rowAffected;
    }

    public async Task<int> BulkMergeProducts(List<Product> products)
    {
        var rowAffected = await dbContext.BulkMergeAsync(
            products,
            option => { option.UniqueKeys = x => new { x.Id }; });
        return rowAffected;
    }

    public async Task SyncDataThenCommit(List<Product> list1, List<Product> list2)
    {
        try
        {
            var dbTransaction = await dbContext.BeginTransactionAsync();

            await dbContext.BulkInsertAsync(
                list1,
                option =>
                {
                    option.BatchSize = 1000;
                    option.CommandTimeout = 60;
                },
                dbTransaction);
            await dbContext.BulkInsertAsync(list2, null, dbTransaction);

            await dbContext.CommitAsync();
        }
        catch (Exception)
        {
            await dbContext.RollbackAsync();
            throw;
        }
    }

    public async Task SyncDataThenRollback(Product item1, List<Product> list2, List<Product> list3)
    {
        try
        {
            var dbTransaction = await dbContext.BeginTransactionAsync();

            await dbContext.Products.AddAsync(item1);
            await dbContext.SaveChangesAsync();
            await dbContext.BulkInsertAsync(list2, null, dbTransaction);
            await dbContext.BulkInsertAsync(list3, null, dbTransaction);

            throw new DbUpdateException("Internal Server Error");
        }
        catch (Exception)
        {
            await dbContext.RollbackAsync();
            throw;
        }
    }
}