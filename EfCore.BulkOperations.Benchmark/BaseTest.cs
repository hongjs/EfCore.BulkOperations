using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

public abstract class BaseTest
{
    private const string ConnectionString =
        "server=localhost; database=test_db; user=root; password=root";

    protected const int DefaultBatchSize = 5000;

    private ApplicationDbContext _dbContext { get; set; }
    protected List<Product> Products { get; set; } = [];
    protected List<Order> Orders { get; set; } = [];

    protected ApplicationDbContext DbContext
    {
        get
        {
            if (_dbContext is null) throw new Exception("DbContext is null");
            return _dbContext;
        }
    }

    protected void InitDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>();
        dbOptions.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString),
            o => { o.EnableRetryOnFailure(); });
        dbOptions.EnableDetailedErrors();
        _dbContext = new ApplicationDbContext(dbOptions.Options);
    }

    protected void InitDbContext(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected static List<Order> CreateOrders(int count, List<Product> products)
    {
        var items = new List<Order>();
        var rnd = new Random();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < count; i++)
        {
            var product = products[rnd.Next(0, products.Count - 1)];
            items.Add(new Order(
                product.Id,
                date,
                rnd.Next(0, 9999999) * 0.01m,
                rnd.Next(0, 9999999) * 0.01m
            ));
        }

        return items;
    }

    protected async Task<List<Product>> InsertProducts(int count)
    {
        var products = new List<Product>();
        for (var i = 0; i < count; i++)
        {
            var product = new Product($"Product {i}", i * 100);
            products.Add(product);
        }

        await DbContext.Products.AddRangeAsync(products);
        await DbContext.SaveChangesAsync();

        return await DbContext.Products.AsNoTracking().ToListAsync();
    }
}