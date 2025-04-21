using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

[MinIterationCount(2)]
[MaxIterationCount(3)]
[WarmupCount(3)]
[Config(typeof(BenchmarksConfig))]
public class BulkDeleteTest : BaseTest
{
    [Params(1_000, 10_000, 100_000)] public int Row { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        InitDbContext();
        Products = await InsertProducts(10);
    }

    [IterationSetup(Target = "EfCore")]
    public void BeforeEfCore()
    {
        var orders = CreateOrders(Row, Products);
        DbContext.Orders.AddRange(orders);
        DbContext.SaveChanges();
        Orders = DbContext.Orders.ToList();
    }

    [IterationSetup(Target = "BulkOperation")]
    public void BeforeBulkOperation()
    {
        var orders = CreateOrders(Row, Products);
        DbContext.Orders.AddRange(orders);
        DbContext.SaveChanges();
        Orders = DbContext.Orders.AsNoTracking().ToList();
    }

    [Benchmark]
    public async Task EfCore()
    {
        DbContext.Orders.RemoveRange(Orders);
        await DbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        await DbContext.BulkDeleteAsync(Orders, option => { option.BatchSize = DefaultBatchSize; });
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await DbContext.Products.ExecuteDeleteAsync();
        await DbContext.Orders.ExecuteDeleteAsync();
        await DbContext.SaveChangesAsync();
    }
}