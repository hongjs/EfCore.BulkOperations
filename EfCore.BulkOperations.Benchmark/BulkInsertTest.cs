using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

[MinIterationCount(2)]
[MaxIterationCount(3)]
[WarmupCount(3)]
[Config(typeof(BenchmarksConfig))]
public class BulkInsertTest : BaseTest
{
    [Params(1_000, 10_000, 100_000)] public int Row { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        InitDbContext();
        Products = await InsertProducts(10);
    }

    [Benchmark]
    public async Task EfCore()
    {
        var orders = CreateOrders(Row, Products);
        await DbContext.Orders.AddRangeAsync(orders);
        await DbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        var orders = CreateOrders(Row, Products);
        await DbContext.BulkInsertAsync(orders, option => { option.BatchSize = DefaultBatchSize; });
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await DbContext.Products.ExecuteDeleteAsync();
        await DbContext.SaveChangesAsync();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        DbContext.Orders.ExecuteDelete();
        DbContext.SaveChanges();
    }
}