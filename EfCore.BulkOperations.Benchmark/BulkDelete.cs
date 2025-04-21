using BenchmarkDotNet.Attributes;
using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

[MinIterationCount(2)]
[MaxIterationCount(3)]
[WarmupCount(3)]
[Config(typeof(BenchmarksConfig))]
public class BulkDelete
{
    private const string ConnectionString =
        "server=localhost; database=test_db; user=root; password=root";

    private ApplicationDbContext _dbContext { get; set; }
    private List<Product> Products { get; set; } = [];

    [Params(10)] public int Row { get; set; }

    private ApplicationDbContext DbContext
    {
        get
        {
            if (_dbContext is null) throw new Exception("DbContext is null");
            return _dbContext;
        }
    }

    [GlobalSetup]
    public async Task Setup()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>();
        dbOptions.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString),
            o => { o.EnableRetryOnFailure(); });
        dbOptions.EnableDetailedErrors();
        _dbContext = new ApplicationDbContext(dbOptions.Options);
        Products = await InsertProducts(10);
    }

    [IterationSetup]
    public async Task BeforeEach()
    {
        var orders = CreateOrders(Row, Products);
        await _dbContext.Orders.AddRangeAsync(orders);
        await _dbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task EfCore()
    {
        var orders = await _dbContext.Orders.ToListAsync();
        DbContext.Orders.RemoveRange(orders);
        await DbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        var orders = await _dbContext.Orders.AsNoTracking().ToListAsync();
        await DbContext.BulkDeleteAsync(orders, option => { option.BatchSize = 10000; });
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await DbContext.Products.ExecuteDeleteAsync();
        await DbContext.Orders.ExecuteDeleteAsync();
        await DbContext.SaveChangesAsync();
    }

    private static List<Order> CreateOrders(int count, List<Product> products)
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

    private async Task<List<Product>> InsertProducts(int count)
    {
        var products = new List<Product>();
        for (var i = 0; i < count; i++)
        {
            var product = new Product($"Product {i}", i * 100);
            products.Add(product);
        }

        await DbContext.Products.AddRangeAsync(products);
        await DbContext.SaveChangesAsync();

        return await DbContext.Products.ToListAsync();
    }
}