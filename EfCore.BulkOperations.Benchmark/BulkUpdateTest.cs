using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

[MinIterationCount(2)]
[MaxIterationCount(3)]
[WarmupCount(3)]
[Config(typeof(BenchmarksConfig))]
public class BulkUpdateTest : BaseTest
{
    [Params(1_000, 10_000, 100_000)] public int Row { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        InitDbContext();
        Products = await InsertProducts(10);
        var orders = CreateOrders(Row, Products);
        await DbContext.Orders.AddRangeAsync(orders);
        await DbContext.SaveChangesAsync();
    }

    [IterationSetup(Target = "EfCore")]
    public void BeforeEfCore()
    {
        Orders = DbContext.Orders.ToList();
    }

    [IterationSetup(Target = "BulkOperation")]
    public void BeforeBulkOperation()
    {
        Orders = DbContext.Orders.AsNoTracking().ToList();
    }

    [Benchmark]
    public async Task EfCore()
    {
        foreach (var order in Orders) order.Unit += 1;
        DbContext.Orders.UpdateRange(Orders);
        await DbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        foreach (var order in Orders) order.Unit += 1;
        await DbContext.BulkUpdateAsync(Orders, option => { option.BatchSize = DefaultBatchSize; });
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await DbContext.Products.ExecuteDeleteAsync();
        await DbContext.Orders.ExecuteDeleteAsync();
        await DbContext.SaveChangesAsync();
    }
}