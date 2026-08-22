using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Benchmark;

/// <summary>
///     Shared plumbing for the EF Core vs BulkOperations comparisons.
///     Two rules keep the two sides comparable:
///     every measured iteration runs against a context that has never tracked an entity, and no
///     data generation happens inside a [Benchmark] method.
/// </summary>
public abstract class BaseTest
{
    protected const int DefaultBatchSize = 5000;

    private const string DefaultConnectionString =
        "server=localhost;port=3306;database=test_db;user=root;password=root";

    private ServerVersion? _serverVersion;

    /// <summary>
    ///     Row counts to measure. Override with BENCHMARK_ROWS, e.g. "1000,10000,100000,1000000".
    /// </summary>
    public static IEnumerable<int> RowCounts =>
        (Environment.GetEnvironmentVariable("BENCHMARK_ROWS") ?? "1000,10000,100000")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(int.Parse);

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("BENCHMARK_MYSQL") ?? DefaultConnectionString;

    /// <summary>Long-lived context used for seeding and cleanup only. Never measured.</summary>
    protected ApplicationDbContext Admin { get; private set; } = null!;

    /// <summary>Rebuilt before every measured iteration.</summary>
    protected ApplicationDbContext DbContext { get; private set; } = null!;

    protected List<Product> Products { get; private set; } = [];
    protected List<Order> Orders { get; set; } = [];

    /// <summary>
    ///     A context that has never seen an entity. Sharing one context across iterations leaves
    ///     every previously saved entity in the change tracker, so EF Core's DetectChanges cost
    ///     grows with each iteration and the benchmark stops measuring the operation itself.
    /// </summary>
    private ApplicationDbContext CreateDbContext()
    {
        // Detected once: AutoDetect opens a connection, which has no place in a measured path.
        _serverVersion ??= ServerVersion.AutoDetect(ConnectionString);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnectionString, _serverVersion)
            .Options;
        return new ApplicationDbContext(options);
    }

    protected void InitAdminContext()
    {
        Admin = CreateDbContext();
    }

    protected void NewIterationContext()
    {
        DbContext?.Dispose();
        DbContext = CreateDbContext();
    }

    /// <summary>
    ///     Creates the schema when it is missing, so a bare MySQL instance is enough to run these.
    /// </summary>
    protected async Task PrepareDatabaseAsync()
    {
        await Admin.Database.EnsureCreatedAsync();
        await ResetDatabaseAsync();
    }

    protected async Task ResetDatabaseAsync()
    {
        await TruncateOrdersAsync();
        await Admin.Products.ExecuteDeleteAsync();
    }

    /// <summary>
    ///     Empties Orders with TRUNCATE rather than DELETE. Deleting the rows leaves the tablespace
    ///     fragmented with InnoDB purge lagging behind, which makes the following iteration's inserts
    ///     markedly slower on the server. Because BenchmarkDotNet runs the methods of a class in a
    ///     fixed order, that penalty always lands on whichever method runs second and shows up as a
    ///     difference between the two implementations that is not there.
    /// </summary>
    protected async Task TruncateOrdersAsync()
    {
        await Admin.Database.ExecuteSqlRawAsync("TRUNCATE TABLE `Orders`");
    }

    protected void TruncateOrders()
    {
        Admin.Database.ExecuteSqlRaw("TRUNCATE TABLE `Orders`");
    }

    protected async Task DisposeContextsAsync()
    {
        DbContext?.Dispose();
        if (Admin is not null) await Admin.DisposeAsync();
    }

    protected async Task SeedProductsAsync(int count)
    {
        var products = new List<Product>();
        for (var i = 0; i < count; i++) products.Add(new Product($"Product {i}", i * 100));

        await Admin.Products.AddRangeAsync(products);
        await Admin.SaveChangesAsync();
        Admin.ChangeTracker.Clear();

        Products = await Admin.Products.AsNoTracking().ToListAsync();
    }

    /// <summary>Seeds orders with the fastest available path; never part of a measurement.</summary>
    protected async Task SeedOrdersAsync(int count)
    {
        await Admin.BulkInsertAsync(CreateOrders(count, Products),
            option => option.BatchSize = DefaultBatchSize);
    }

    /// <summary>
    ///     Seeded so every run works on the same data, which keeps results comparable between runs.
    /// </summary>
    protected static List<Order> CreateOrders(int count, List<Product> products)
    {
        var items = new List<Order>(count);
        var rnd = new Random(20240401);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < count; i++)
        {
            var product = products[rnd.Next(products.Count)];
            items.Add(new Order(
                product.Id,
                date,
                rnd.Next(0, 9999999) * 0.01m,
                rnd.Next(0, 9999999) * 0.01m
            ));
        }

        return items;
    }
}
