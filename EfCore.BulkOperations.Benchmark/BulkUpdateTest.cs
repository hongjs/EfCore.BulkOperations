using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

[Config(typeof(BenchmarksConfig))]
public class BulkUpdateTest : BaseTest
{
    [ParamsSource(nameof(RowCounts))] public int Row { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        InitAdminContext();
        await PrepareDatabaseAsync();
        await SeedProductsAsync(10);
        await SeedOrdersAsync(Row);
    }

    /// <summary>
    ///     Loading the rows is not the operation under test. EF Core needs them tracked to write
    ///     them back, BulkUpdate does not, so each side is given the form it actually works with.
    /// </summary>
    [IterationSetup(Target = nameof(EfCore))]
    public void BeforeEfCore()
    {
        NewIterationContext();
        Orders = DbContext.Orders.ToList();
    }

    [IterationSetup(Target = nameof(BulkOperation))]
    public void BeforeBulkOperation()
    {
        NewIterationContext();
        Orders = DbContext.Orders.AsNoTracking().ToList();
    }

    [Benchmark(Baseline = true)]
    public async Task EfCore()
    {
        foreach (var order in Orders) order.Unit += 1;

        // UpdateRange marks every column modified, which is the same work BulkUpdate does.
        DbContext.Orders.UpdateRange(Orders);
        await DbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        foreach (var order in Orders) order.Unit += 1;

        await DbContext.BulkUpdateAsync(Orders, option => option.BatchSize = DefaultBatchSize);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await ResetDatabaseAsync();
        await DisposeContextsAsync();
    }
}
