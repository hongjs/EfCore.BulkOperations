using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

/// <summary>
///     Sweeps BulkOption.BatchSize at a fixed row count to show where the default (200) sits.
/// </summary>
[Config(typeof(BenchmarksConfig))]
public class BatchSizeTest : BaseTest
{
    private const int Row = 50_000;

    [Params(200, 500, 1_000, 2_000, 5_000, 10_000)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        InitAdminContext();
        await PrepareDatabaseAsync();
        await SeedProductsAsync(10);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        NewIterationContext();
        Orders = CreateOrders(Row, Products);
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        await DbContext.BulkInsertAsync(Orders, option => option.BatchSize = BatchSize);
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
