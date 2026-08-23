using EfCore.BulkOperations.Test.Setup;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Test;

/// <summary>
///     Cancels the operation's token the first time a value is read, so the failure and the
///     cancellation happen together while the bulk operation owns an open transaction.
/// </summary>
internal class ThrowingEntity
{
    internal static CancellationTokenSource? CancelOnRead;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get
        {
            CancelOnRead?.Cancel();
            throw new InvalidOperationException($"value of {Id} could not be read");
        }
        // ReSharper disable once ValueParameterNotUsed
        set { }
    }
}

internal class ThrowingDbContext(DbContextOptions<ThrowingDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ThrowingEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Id).IsUnique();
        });
    }
}

public class BulkTransactionTest(IntegrationTestFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Should_SurfaceOriginalError_WhenTokenIsCancelledDuringTheOperation()
    {
        // Arrange
        var connectionString = DbContext.Database.GetConnectionString()!;
        var options = new DbContextOptionsBuilder<ThrowingDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        await using var dbContext = new ThrowingDbContext(options);

        using var cts = new CancellationTokenSource();
        ThrowingEntity.CancelOnRead = cts;

        try
        {
            // Act
            var items = new List<ThrowingEntity> { new() };
            var exception = await Assert.ThrowsAnyAsync<Exception>(
                () => dbContext.BulkInsertAsync(items, null, null, cts.Token));

            // Assert: rolling back with the cancelled token would throw and hide the real cause.
            Assert.DoesNotContain("canceled", exception.GetType().Name, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("could not be read", exception.ToString());
        }
        finally
        {
            ThrowingEntity.CancelOnRead = null;
        }
    }
}
