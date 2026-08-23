using EfCore.BulkOperations.Models;
using EfCore.BulkOperations.Test.Setup;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Test;

/// <summary>
///     BulkCommand asks EF Core for each property's ValueConverter and applies it before the value
///     becomes a parameter. Nothing in the API project's model declares a converter, so that branch
///     used to run in no test at all - these send an entity that has two.
/// </summary>
[Trait("Category", "Unit")]
public class ValueConverterTest : IDisposable
{
    private ConvertedEntityDbContext DbContext { get; } = ConvertedEntityDbContext.Create();

    public void Dispose()
    {
        DbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private static List<Ticket> Tickets()
    {
        return
        [
            new Ticket
            {
                Id = new Guid("11111111-0000-0000-0000-000000000000"),
                Title = "First",
                Status = TicketStatus.Closed,
                IsUrgent = true
            }
        ];
    }

    /// <summary>The value sent for a column, found by the column's position in the entity type.</summary>
    private object? SentValue(BatchData batch, string columnName)
    {
        var columns = DbContext.Model.FindEntityType(typeof(Ticket))!
            .GetProperties()
            .Select(p => p.GetColumnName())
            .ToList();
        var index = columns.IndexOf(columnName);
        Assert.InRange(index, 0, columns.Count - 1);
        return batch.Parameters.Single(p => p.Name == $"@p0_{index}").Value;
    }

    [Fact]
    public void Should_SendConvertedValues_OnInsert()
    {
        // Act
        var batch = BulkCommand.GenerateInsertBatches(DbContext, Tickets(), null).Single();

        // Assert
        Assert.Equal("Closed", SentValue(batch, "Status"));
        Assert.Equal("Y", SentValue(batch, "IsUrgent"));
        Assert.Equal("First", SentValue(batch, "Title"));
    }

    [Fact]
    public void Should_SendConvertedValues_OnUpdate()
    {
        // The update path builds its rows through a different method than insert does, so the
        // conversion has to be asserted on both.

        // Act
        var batch = BulkCommand.GenerateUpdateBatches(DbContext, Tickets(), null).Single();

        // Assert
        Assert.Equal("Closed", SentValue(batch, "Status"));
        Assert.Equal("Y", SentValue(batch, "IsUrgent"));
    }

    [Fact]
    public void Should_SendConvertedValues_OnMerge()
    {
        // Act
        var batch = BulkCommand.GenerateMergeBatches(DbContext, Tickets(), null).Single();

        // Assert
        Assert.Equal("Closed", SentValue(batch, "Status"));
        Assert.Equal("Y", SentValue(batch, "IsUrgent"));
    }
}
