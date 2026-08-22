using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

[Config(typeof(BenchmarksConfig))]
public class BulkInsertTest : BaseTest
{
    [ParamsSource(nameof(RowCounts))] public int Row { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        InitAdminContext();
        await PrepareDatabaseAsync();
        await SeedProductsAsync(10);
    }

    /// <summary>Building the rows is not the operation under test, so it stays out of it.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        NewIterationContext();
        Orders = CreateOrders(Row, Products);
    }

    [Benchmark(Baseline = true)]
    public async Task EfCore()
    {
        await DbContext.Orders.AddRangeAsync(Orders);
        await DbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        await DbContext.BulkInsertAsync(Orders, option => option.BatchSize = DefaultBatchSize);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        TruncateOrders();
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await ResetDatabaseAsync();
        await DisposeContextsAsync();
    }
}
