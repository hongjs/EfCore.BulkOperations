using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

[Config(typeof(BenchmarksConfig))]
public class BulkDeleteTest : BaseTest
{
    [ParamsSource(nameof(RowCounts))] public int Row { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        InitAdminContext();
        await PrepareDatabaseAsync();
        await SeedProductsAsync(10);
    }

    /// <summary>Rows are re-seeded and re-read per iteration; neither is part of the measurement.</summary>
    [IterationSetup(Target = nameof(EfCore))]
    public void BeforeEfCore()
    {
        NewIterationContext();
        SeedOrdersAsync(Row).GetAwaiter().GetResult();
        Orders = DbContext.Orders.ToList();
    }

    [IterationSetup(Target = nameof(BulkOperation))]
    public void BeforeBulkOperation()
    {
        NewIterationContext();
        SeedOrdersAsync(Row).GetAwaiter().GetResult();
        Orders = DbContext.Orders.AsNoTracking().ToList();
    }

    [Benchmark(Baseline = true)]
    public async Task EfCore()
    {
        DbContext.Orders.RemoveRange(Orders);
        await DbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        await DbContext.BulkDeleteAsync(Orders, option => option.BatchSize = DefaultBatchSize);
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
