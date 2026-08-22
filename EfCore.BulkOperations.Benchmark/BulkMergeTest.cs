using BenchmarkDotNet.Attributes;
using EfCore.BulkOperations.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

/// <summary>
///     Upsert: half the rows already exist and half are new. EF Core has no upsert, so the
///     comparison is against what an EF Core application has to do instead — look the rows up in
///     chunks, then update or add each one.
/// </summary>
[Config(typeof(BenchmarksConfig))]
public class BulkMergeTest : BaseTest
{
    private const int LookupChunkSize = 1_000;

    private List<Order> _all = [];
    private List<Order> _existing = [];

    [ParamsSource(nameof(RowCounts))] public int Row { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        InitAdminContext();
        await PrepareDatabaseAsync();
        await SeedProductsAsync(10);

        _all = CreateOrders(Row, Products);
        _existing = _all.Take(Row / 2).ToList();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        NewIterationContext();
        TruncateOrders();
        Admin.BulkInsertAsync(_existing, option => option.BatchSize = DefaultBatchSize)
            .GetAwaiter().GetResult();
        Orders = _all;
    }

    [Benchmark(Baseline = true)]
    public async Task EfCore()
    {
        foreach (var chunk in Orders.Chunk(LookupChunkSize))
        {
            var ids = chunk.Select(x => x.Id).ToList();
            var existing = await DbContext.Orders
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            foreach (var order in chunk)
                if (existing.TryGetValue(order.Id, out var tracked))
                    tracked.UpdateOrder(order.Unit, order.Amount);
                else
                    DbContext.Orders.Add(order);
        }

        await DbContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        await DbContext.BulkMergeAsync(Orders, option => option.BatchSize = DefaultBatchSize);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await ResetDatabaseAsync();
        await DisposeContextsAsync();
    }
}
