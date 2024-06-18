using System.Diagnostics;
using System.Text;
using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.API.Repositories;
using EfCore.BulkOperations.Test.Setup;
using Xunit.Abstractions;

namespace EfCore.BulkOperations.Test;

public class BenchmarkTest : BaseIntegrationTest
{
    private readonly IOrderRepository _orderRepo;
    private readonly IProductRepository _productRepo;
    private readonly ITestOutputHelper _testOutputHelper;

    public BenchmarkTest(IntegrationTestFactory factory, ITestOutputHelper testOutputHelper)
        : base(factory)
    {
        _productRepo = GetRequiredService<IProductRepository>();
        _orderRepo = GetRequiredService<IOrderRepository>();
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestInsertPerformance()
    {
        var counts = new[] { 100, 1000, 10000, 100000 };
        var results = new List<BenchmarkResult>();
        foreach (var count in counts)
        {
            var result = await InsertOrders(count);
            results.Add(result);
        }

        LogResults(results);
        Assert.Equal(counts.Length, results.Count);
    }


    [Fact]
    public async Task TestUpdatePerformance()
    {
        var counts = new[] { 100, 1000, 10000, 100000 };
        var results = new List<BenchmarkResult>();
        foreach (var count in counts)
        {
            var result = await UpdateOrders(count);
            results.Add(result);
        }

        LogResults(results);
        Assert.Equal(counts.Length, results.Count);
    }


    private async Task<BenchmarkResult> InsertOrders(int count)
    {
        var products = await InsertProducts();
        var orders1 = CreateOrders(count, products);

        var watchBulk = new Stopwatch();
        watchBulk.Start();
        await _orderRepo.BulkInsertOrders(orders1);
        watchBulk.Stop();
        await _orderRepo.DeleteAllOrders();

        var watchEf = new Stopwatch();
        var orders2 = CreateOrders(count, products);
        watchEf.Start();
        await _orderRepo.InsertOrders(orders2);
        watchEf.Stop();
        await _orderRepo.DeleteAllOrders();

        return new BenchmarkResult(
            "Insert",
            count,
            watchBulk.ElapsedMilliseconds,
            watchEf.ElapsedMilliseconds);
    }


    private async Task<BenchmarkResult> UpdateOrders(int count)
    {
        var products = await InsertProducts();
        var watchBulk = new Stopwatch();
        var orders1 = CreateOrders(count, products);
        await _orderRepo.BulkInsertOrders(orders1);


        watchBulk.Start();
        ModifyOrders(orders1);
        await _orderRepo.BulkUpdateOrders(orders1);
        watchBulk.Stop();

        var orders2 = await _orderRepo.GetOrders(true);
        var watchEf = new Stopwatch();
        watchEf.Start();
        ModifyOrders(orders2);
        await _orderRepo.UpdateOrders(orders2);
        watchEf.Stop();
        await _orderRepo.DeleteAllOrders();

        return new BenchmarkResult(
            "Update",
            count,
            watchBulk.ElapsedMilliseconds,
            watchEf.ElapsedMilliseconds);
    }

    private List<Order> CreateOrders(int count, List<Product> products)
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

    private void ModifyOrders(List<Order> orders)
    {
        var rnd = new Random();
        for (var i = 0; i < orders.Count; i++)
            orders[i].UpdateOrder(rnd.Next(0, 9999999) * 0.01m, rnd.Next(0, 9999999) * 0.01m);
    }

    private async Task<List<Product>> InsertProducts()
    {
        var products = new List<Product>();
        for (var i = 0; i < 10; i++)
        {
            var product = new Product($"Product {i}", i * 100);
            products.Add(product);
        }

        await _productRepo.BulkInsertProducts(products);
        return await _productRepo.GetProducts(true);
    }


    private void LogResults(List<BenchmarkResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"{"Operation".PadLeft(15)}|{"Records".PadLeft(15)}|{"EF Core (ms)".PadLeft(15)}|{"Bulk (ms)".PadLeft(15)}");
        foreach (var result in results)
            sb.AppendLine(
                $"{result.Operation.PadLeft(15)}|{result.Count.ToString().PadLeft(15)}|{result.EfElapsedMs.ToString().PadLeft(15)}|{result.BulkElapsedMs.ToString().PadLeft(15)}");

        _testOutputHelper.WriteLine(sb.ToString());
    }
}

public record BenchmarkResult(string Operation, int Count, long BulkElapsedMs, long EfElapsedMs);