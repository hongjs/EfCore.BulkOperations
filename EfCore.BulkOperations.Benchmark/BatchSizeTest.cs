using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

/*
    | Method        | BatchSize | Mean    | Error    | StdDev   | Median  | Allocated |
    |-------------- |---------- |--------:|---------:|---------:|--------:|----------:|
    | BulkOperation | 200       | 1.568 s | 0.0439 s | 0.1246 s | 1.540 s | 158.17 MB |
    | BulkOperation | 500       | 1.641 s | 0.0654 s | 0.1906 s | 1.596 s | 152.94 MB |
    | BulkOperation | 1000      | 1.610 s | 0.0789 s | 0.2301 s | 1.576 s |    160 MB |
    | BulkOperation | 2000      | 1.534 s | 0.0701 s | 0.2055 s | 1.465 s | 168.76 MB |
    | BulkOperation | 5000      | 1.466 s | 0.0487 s | 0.1397 s | 1.425 s | 169.73 MB |
*/

[Config(typeof(BenchmarksConfig))]
public class BatchSizeTest : BaseTest
{
    private const int Row = 50000;
    [Params(200, 500, 1000, 2000, 5000)] public int BatchSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        InitDbContext();
        Products = await InsertProducts(10);
    }

    [Benchmark]
    public async Task BulkOperation()
    {
        var orders = CreateOrders(Row, Products);
        await DbContext.BulkInsertAsync(orders, option => { option.BatchSize = BatchSize; });
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