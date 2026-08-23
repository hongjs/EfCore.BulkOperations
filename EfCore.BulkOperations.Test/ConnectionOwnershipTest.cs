using System.Data;
using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.Test.Setup;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Test;

/// <summary>
///     Who owns the connection and the transaction. A bulk call must leave what it did not open
///     alone: a connection the caller opened stays open, and a transaction the context already has
///     is joined rather than nested.
/// </summary>
public class ConnectionOwnershipTest(IntegrationTestFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Should_LeaveConnectionOpen_WhenCallerOpenedIt()
    {
        // Arrange
        var connection = DbContext.Database.GetDbConnection();
        await DbContext.Database.OpenConnectionAsync();
        try
        {
            Assert.Equal(ConnectionState.Open, connection.State);

            // Act
            await DbContext.BulkInsertAsync(new List<Product> { new("Test", 1m) });

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(1, await DbContext.Products.CountAsync());
        }
        finally
        {
            await DbContext.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task Should_CloseConnection_WhenItOpenedIt()
    {
        // Arrange
        var connection = DbContext.Database.GetDbConnection();
        Assert.Equal(ConnectionState.Closed, connection.State);

        // Act
        await DbContext.BulkInsertAsync(new List<Product> { new("Test", 1m) });

        // Assert
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task Should_JoinTheContextTransaction_WhenOneIsOpen()
    {
        // Arrange: a transaction begun through EF Core, not passed to the bulk call.
        await using var transaction = await DbContext.Database.BeginTransactionAsync();

        // Act: used to fail with "Transactions may not be nested".
        await DbContext.BulkInsertAsync(new List<Product> { new("Test", 1m) });
        await transaction.RollbackAsync();

        // Assert: it ran inside the caller's transaction, so the rollback took it away.
        Assert.Equal(0, await DbContext.Products.CountAsync());
    }

    [Fact]
    public async Task Should_CommitWithTheContextTransaction_WhenOneIsOpen()
    {
        // Arrange
        await using var transaction = await DbContext.Database.BeginTransactionAsync();

        // Act
        await DbContext.BulkInsertAsync(new List<Product> { new("Test", 1m) });
        await transaction.CommitAsync();

        // Assert
        Assert.Equal(1, await DbContext.Products.CountAsync());
    }
}
