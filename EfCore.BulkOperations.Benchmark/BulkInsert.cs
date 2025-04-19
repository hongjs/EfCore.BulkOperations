using BenchmarkDotNet.Attributes;
using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.API.Repositories;
using EfCore.BulkOperations.Test.Setup;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

public class BulkInsert
{
    private IntegrationTestFactory? Factory { get; set; }
    private List<Product> Products { get; set; } = [];

    [Params(1)] public int Count { get; set; }

    private ApplicationDbContext DbContext
    {
        get
        {
            if (Factory is null)
                throw new Exception("error");
            return Factory.DbContext;
        }
    }

    [GlobalSetup]
    public async Task Setup()
    {
        Factory = new IntegrationTestFactory();
        await Factory.InitializeAsync();
        Products = await InsertProducts(10);
    }

    [Benchmark]
    public async Task<int> EfCore()
    {
        var orders = CreateOrders(Count, Products);
        await DbContext.Orders.AddRangeAsync(orders);
        return -1;
    }

    [Benchmark]
    public async Task<int> BulkOperation()
    {
        var orders = CreateOrders(Count, Products);
        await DbContext.BulkInsertAsync(orders);
        return -1;
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await DbContext.Products.ExecuteDeleteAsync();
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await DbContext.Orders.ExecuteDeleteAsync();
    }

    private static List<Order> CreateOrders(int count, List<Product> products)
    {
        var items = new List<Order>();
        var rnd = new Random();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < count; i++)
        {
            var product = products[rnd.Next(0, products.Count)];
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