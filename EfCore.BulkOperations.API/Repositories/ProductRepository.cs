using System.Data;
using System.Data.Common;
using EfCore.BulkOperations.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        IDbContextTransaction? transaction = null;
        DbConnection? connection = null;
        try
        {
            connection = dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();
            transaction = await dbContext.Database.BeginTransactionAsync();
            var dbTransaction = transaction.GetDbTransaction();

            await dbContext.BulkInsertAsync(
                list1,
                option =>
                {
                    option.BatchSize = 1000;
                    option.CommandTimeout = 60;
                },
                dbTransaction);
            await dbContext.BulkInsertAsync(list2, null, dbTransaction);

            await transaction.CommitAsync();
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
    }

    public async Task SyncDataThenRollback(Product item1, List<Product> list2, List<Product> list3)
    {
        IDbContextTransaction? transaction = null;
        DbConnection? connection = null;
        try
        {
            connection = dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();
            transaction = await dbContext.Database.BeginTransactionAsync();
            var dbTransaction = transaction.GetDbTransaction();

            await dbContext.Products.AddAsync(item1);
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
    }
}