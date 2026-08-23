using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.Test.Setup;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace EfCore.BulkOperations.Test;

/// <summary>
///     Runs the statements EdgeCaseTest only inspects against a real MySQL, so a string that
///     looks right is also one the server accepts and that does what the library claims.
/// </summary>
public class EdgeCaseIntegrationTest(IntegrationTestFactory factory) : BaseIntegrationTest(factory)
{
    /// <summary>
    ///     A second database on the same container for the EdgeCaseDbContext model. EnsureCreated
    ///     does nothing on a database that already has tables, so the shared one cannot be used, and
    ///     the configured user only has rights on that one - hence root, whose password Testcontainers
    ///     sets to the same value.
    /// </summary>
    private EdgeCaseDbContext CreateEdgeCaseDbContext()
    {
        var builder = new MySqlConnectionStringBuilder(DbContext.Database.GetConnectionString()!)
        {
            Database = "edge_db",
            UserID = "root"
        };
        var options = new DbContextOptionsBuilder<EdgeCaseDbContext>()
            .UseMySql(builder.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        var dbContext = new EdgeCaseDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    [Fact]
    public async Task Should_MergeKeyOnlyEntity_OnMySql()
    {
        // The generated statement ends in `KeyOnly`.`Id` = `KeyOnly`.`Id`; this is the one place
        // MySQL gets to say whether it accepts that.
        await using var db = CreateEdgeCaseDbContext();
        await db.Set<KeyOnlyEntity>().ExecuteDeleteAsync();
        var items = new List<KeyOnlyEntity> { new(), new(), new() };

        // Act
        var first = await db.BulkMergeAsync(items);
        var second = await db.BulkMergeAsync(items);

        // Assert: inserted once, matched and left alone the second time. MySqlConnector's default
        // UseAffectedRows=false reports matched rows, so the second call counts 1 per row, not 0.
        Assert.Equal(3, first);
        Assert.Equal(3, second);
        Assert.Equal(3, await db.Set<KeyOnlyEntity>().CountAsync());
    }

    [Fact]
    public async Task Should_UpdateAndDeleteOnPrimaryKey_OnMySql()
    {
        // Log has a primary key and no unique index. No UniqueKeys option is passed, so the match
        // is on the primary key the library fell back to.
        var log = new Log("before");
        await DbContext.BulkInsertAsync(new List<Log> { log });

        // Act
        var updated = await DbContext.BulkUpdateAsync(new List<Log> { new("after") { Id = log.Id } });

        // Assert
        Assert.Equal(1, updated);
        Assert.Equal("after", (await DbContext.Logs.AsNoTracking().SingleAsync()).Content);

        // Act
        var deleted = await DbContext.BulkDeleteAsync(new List<Log> { log });

        // Assert
        Assert.Equal(1, deleted);
        Assert.Equal(0, await DbContext.Logs.CountAsync());
    }

    [Fact]
    public async Task Should_UpdateWithoutTouchingKey_WhenKeyIsIgnored_OnMySql()
    {
        // Arrange
        var product = new Product("before", 1m);
        await DbContext.BulkInsertAsync(new List<Product> { product });
        product.UpdateName("after");

        // Act
        var updated = await DbContext.BulkUpdateAsync(
            new List<Product> { product },
            option => option.IgnoreOnUpdate = x => new { x.Id });

        // Assert
        Assert.Equal(1, updated);
        var stored = await DbContext.Products.AsNoTracking().SingleAsync();
        Assert.Equal(product.Id, stored.Id);
        Assert.Equal("after", stored.Name);
    }

    [Fact]
    public async Task Should_ReturnMySqlRowCounts_OnMerge()
    {
        // ON DUPLICATE KEY UPDATE reports 1 for an insert and 2 for an update. A row matched but
        // left as it was counts 1 under MySqlConnector's default UseAffectedRows=false (matched
        // rows), and would count 0 with UseAffectedRows=true. The README documents this, so it is
        // held to it here.
        var product = new Product("first", 1m);

        Assert.Equal(1, await DbContext.BulkMergeAsync(new List<Product> { product }));
        Assert.Equal(1, await DbContext.BulkMergeAsync(new List<Product> { product }));

        product.UpdateName("second");
        Assert.Equal(2, await DbContext.BulkMergeAsync(new List<Product> { product }));

        Assert.Equal("second", (await DbContext.Products.AsNoTracking().SingleAsync()).Name);
    }
}
