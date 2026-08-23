using System.Data;
using EfCore.BulkOperations.Models;
using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.API.Repositories;
using EfCore.BulkOperations.Test.Setup;

namespace EfCore.BulkOperations.Test;

internal record DummyEntity(string Id);

/// <summary>
///     Asserts on the generated SQL only, so it needs the EF Core model but no database.
///     Tagged as a unit test so CI can run it on a platform where Testcontainers is unavailable.
/// </summary>
[Trait("Category", "Unit")]
public class BulkCommandTest : IDisposable
{
    private ApplicationDbContext DbContext { get; } = ModelOnlyDbContext.Create();

    public void Dispose()
    {
        DbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Compares SQL without letting the host's line terminator decide the outcome, so the
    ///     assertions mean the same thing on LF and CRLF checkouts.
    /// </summary>
    private static void AssertSqlEqual(string expected, string actual)
    {
        Assert.Equal(expected.Replace("\r\n", "\n"), actual.Replace("\r\n", "\n"));
    }
    [Fact]
    public void Should_GenerateInsertScript()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m), new("Test", 123.45m) };
        var expectedSql = @"INSERT INTO `Products`
(`Id`, `CreatedAt`, `Name`, `Price`)
VALUES
(@p0_0, @p0_1, @p0_2, @p0_3),
(@p1_0, @p1_1, @p1_2, @p1_3)
";

        // Act
        var batches = BulkCommand
            .GenerateInsertBatches(DbContext, items, null)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Equal(8, batches[0].Parameters.Count);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateInsertScriptAndIgnoreCreatedAt()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m) };
        var expectedSql = @"INSERT INTO `Products`
(`Id`, `Name`, `Price`)
VALUES
(@p0_0, @p0_1, @p0_2)
";
        var option = new BulkOption<Product>(
            ignoreOnInsert: x => new { x.CreatedAt }
        );

        // Act
        var batches = BulkCommand
            .GenerateInsertBatches(DbContext, items, option)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Equal(3, batches[0].Parameters.Count);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateUpdateScript()
    {
        // Arrange
        var items = new List<Product>
        {
            new("Test1", 123.45m),
            new("Test2", 123.45m)
        };
        var expectedSql = @"UPDATE `Products` AS tb
INNER JOIN (
SELECT @p0_0 AS `Id`, @p0_1 AS `CreatedAt`, @p0_2 AS `Name`, @p0_3 AS `Price`, 0 AS zRowNo
UNION ALL SELECT @p1_0 AS `Id`, @p1_1 AS `CreatedAt`, @p1_2 AS `Name`, @p1_3 AS `Price`, 1 AS zRowNo
) AS tmp
ON tb.`Id` = tmp.`Id`
SET tb.`CreatedAt` = tmp.`CreatedAt`,
tb.`Name` = tmp.`Name`,
tb.`Price` = tmp.`Price`
";

        // Act
        var batches = BulkCommand
            .GenerateUpdateBatches(DbContext, items, null)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Equal(8, batches[0].Parameters.Count);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateUpdateScriptWithIgnoreCreatedAt()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m) };
        var expectedSql = @"UPDATE `Products` AS tb
INNER JOIN (
SELECT @p0_0 AS `Id`, @p0_1 AS `Name`, @p0_2 AS `Price`, 0 AS zRowNo
) AS tmp
ON tb.`Id` = tmp.`Id`
SET tb.`Name` = tmp.`Name`,
tb.`Price` = tmp.`Price`
";

        var option = new BulkOption<Product>(
            ignoreOnUpdate: x => new { x.CreatedAt }
        );

        // Act
        var batches = BulkCommand
            .GenerateUpdateBatches(DbContext, items, option)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Equal(3, batches[0].Parameters.Count);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateUpdateScriptWithCustomUniqueKey()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m) };
        var expectedSql = @"UPDATE `Products` AS tb
INNER JOIN (
SELECT @p0_0 AS `Id`, @p0_1 AS `CreatedAt`, @p0_2 AS `Name`, @p0_3 AS `Price`, 0 AS zRowNo
) AS tmp
ON tb.`Id` = tmp.`Id`
SET tb.`CreatedAt` = tmp.`CreatedAt`,
tb.`Name` = tmp.`Name`,
tb.`Price` = tmp.`Price`
";
        var option = new BulkOption<Product>(
            uniqueKeys: x => new { x.Id }
        );

        // Act
        var batches = BulkCommand
            .GenerateUpdateBatches(DbContext, items, option)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Equal(4, batches[0].Parameters.Count);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }


    [Fact]
    public void Should_GenerateDeleteScript()
    {
        // Arrange
        var items = new List<Product>
        {
            new("Test1", 123.45m),
            new("Test2", 123.45m)
        };
        var expectedSql = @"DELETE tb
FROM `Products` AS tb
INNER JOIN (
SELECT @p0_0 AS `Id`, 0 AS zRowNo
UNION ALL SELECT @p1_0 AS `Id`, 1 AS zRowNo
) AS tmp
ON tb.`Id` = tmp.`Id`
";

        // Act
        var batches = BulkCommand
            .GenerateDeleteBatches(DbContext, items, null)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Equal(2, batches[0].Parameters.Count);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateDeleteScriptWithCustomUniqueKeys()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m) };
        var expectedSql = @"DELETE tb
FROM `Products` AS tb
INNER JOIN (
SELECT @p0_0 AS `Id`, 0 AS zRowNo
) AS tmp
ON tb.`Id` = tmp.`Id`
";
        var option = new BulkOption<Product>(
            uniqueKeys: x => new { x.Id }
        );

        // Act
        var batches = BulkCommand
            .GenerateDeleteBatches(DbContext, items, option)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Single(batches[0].Parameters);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateMergeScript()
    {
        // Arrange
        var items = new List<Product>
        {
            new("Test1", 123.45m),
            new("Test2", 123.45m)
        };
        var expectedSql = @"INSERT INTO `Products`
(`Id`, `CreatedAt`, `Name`, `Price`)
SELECT `Id`, `CreatedAt`, `Name`, `Price`
FROM (
SELECT @p0_0 AS `Id`, @p0_1 AS `CreatedAt`, @p0_2 AS `Name`, @p0_3 AS `Price`, 0 AS zRowNo
UNION ALL SELECT @p1_0 AS `Id`, @p1_1 AS `CreatedAt`, @p1_2 AS `Name`, @p1_3 AS `Price`, 1 AS zRowNo
) AS tmp
 ON DUPLICATE KEY UPDATE
 `Products`.`CreatedAt` = tmp.`CreatedAt`,
 `Products`.`Name` = tmp.`Name`,
 `Products`.`Price` = tmp.`Price`
";

        // Act
        var batches = BulkCommand
            .GenerateMergeBatches(DbContext, items, null)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Equal(8, batches[0].Parameters.Count);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateMergeScriptWithIgnoreFields()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m) };
        var expectedSql = @"INSERT INTO `Products`
(`Id`, `Name`, `Price`)
SELECT `Id`, `Name`, `Price`
FROM (
SELECT @p0_0 AS `Id`, @p0_1 AS `Name`, @p0_2 AS `Price`, 0 AS zRowNo
) AS tmp
 ON DUPLICATE KEY UPDATE
 `Products`.`Name` = tmp.`Name`,
 `Products`.`Price` = tmp.`Price`
";

        var option = new BulkOption<Product>(
            ignoreOnInsert: x => new { x.CreatedAt },
            ignoreOnUpdate: x => new { x.CreatedAt }
        );

        // Act
        var batches = BulkCommand
            .GenerateMergeBatches(DbContext, items, option)
            .ToList();

        // Assert
        Assert.Single(batches);
        Assert.Equal(3, batches[0].Parameters.Count);
        AssertSqlEqual(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_Split3Batches()
    {
        // Arrange
        var items = new List<Product>
        {
            new("Test1", 100),
            new("Test2", 200),
            new("Test3", 300)
        };
        var option = new BulkOption<Product>(1);

        // Act
        var batches = BulkCommand
            .GenerateInsertBatches(DbContext, items, option)
            .ToList();

        // Assert
        Assert.Equal(3, batches.Count);
        Assert.Equal(4, batches[0].Parameters.Count);
        Assert.Equal(4, batches[1].Parameters.Count);
        Assert.Equal(4, batches[2].Parameters.Count);
    }

    [Fact]
    public void ShouldError_WhenPassNonEntity()
    {
        // Arrange
        var items = new List<DummyEntity> { new("test") };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var _ = BulkCommand.GenerateInsertBatches(DbContext, items, null).ToList();
        });

        // Assert
        Assert.StartsWith("Unable to resolve EntityType", exception.Message);
    }

    [Fact]
    public void ShouldError_WhenUpdateEntityHasNoUniqueKey()
    {
        // Arrange
        var items = new List<Log> { new("Test") };

        // Act
        var exception = Assert.Throws<MissingPrimaryKeyException>(() =>
        {
            var _ = BulkCommand.GenerateUpdateBatches(DbContext, items, null).ToList();
        });

        // Assert
        Assert.StartsWith("A unique key in the database is required to perform a bulk operation", exception.Message);
    }

    [Fact]
    public void ShouldError_WhenDeleteEntityHasNoUniqueKey()
    {
        // Arrange
        var items = new List<Log> { new("Test") };

        // Act
        var exception = Assert.Throws<MissingPrimaryKeyException>(() =>
        {
            var _ = BulkCommand.GenerateDeleteBatches(DbContext, items, null).ToList();
        });

        // Assert
        Assert.StartsWith("A unique key in the database is required to perform a bulk operation", exception.Message);
    }

    [Fact]
    public void Should_GenerateNoBatches_WhenItemsAreEmpty()
    {
        // Arrange
        var items = new List<Product>();

        // Act & Assert
        Assert.Empty(BulkCommand.GenerateInsertBatches(DbContext, items, null).ToList());
        Assert.Empty(BulkCommand.GenerateUpdateBatches(DbContext, items, null).ToList());
        Assert.Empty(BulkCommand.GenerateDeleteBatches(DbContext, items, null).ToList());
        Assert.Empty(BulkCommand.GenerateMergeBatches(DbContext, items, null).ToList());
    }

    [Fact]
    public void Should_NotEndStatementWithTrailingComma()
    {
        // Guards the assumption that Environment.NewLine is a single character: the trailing comma
        // used to be trimmed at a fixed offset, which left the comma in place under CRLF.

        // Arrange
        var items = new List<Product> { new("Test1", 123.45m), new("Test2", 123.45m) };

        // Act
        var statements = new[]
        {
            BulkCommand.GenerateInsertBatches(DbContext, items, null).Single().Sql.ToString(),
            BulkCommand.GenerateUpdateBatches(DbContext, items, null).Single().Sql.ToString(),
            BulkCommand.GenerateDeleteBatches(DbContext, items, null).Single().Sql.ToString(),
            BulkCommand.GenerateMergeBatches(DbContext, items, null).Single().Sql.ToString()
        };

        // Assert
        foreach (var sql in statements)
            Assert.False(sql.TrimEnd().EndsWith(","), $"Statement ends with a trailing comma:\n{sql}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShouldError_WhenBatchSizeIsNotPositive(int batchSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BulkOption<Product>(batchSize));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BulkOption<Product> { BatchSize = batchSize });
    }

    /// <summary>Three products whose keys are deliberately not in ascending order.</summary>
    private static List<Product> UnsortedProducts()
    {
        return
        [
            new Product("C", 3m) { Id = new Guid("cccccccc-0000-0000-0000-000000000000") },
            new Product("A", 1m) { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000000") },
            new Product("B", 2m) { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000000") }
        ];
    }

    /// <summary>Column 2 of Product is Name, which has no value converter to read through.</summary>
    private static List<string> SentNames(BatchData batch)
    {
        return batch.Parameters
            .Where(p => p.Name.EndsWith("_2"))
            .Select(p => (string)p.Value!)
            .ToList();
    }

    [Fact]
    public void Should_OrderRowsByKey_BeforeSendingThem()
    {
        // Arrange
        var items = UnsortedProducts();
        var expected = items.OrderBy(x => x.Id).Select(x => x.Name).ToList();

        // Act
        var batches = BulkCommand.GenerateInsertBatches(DbContext, items, null).ToList();

        // Assert
        Assert.Equal(expected, SentNames(Assert.Single(batches)));
    }

    [Fact]
    public void Should_KeepTheGivenOrder_WhenSortByKeysIsOff()
    {
        // Arrange
        var items = UnsortedProducts();
        var expected = items.Select(x => x.Name).ToList();
        var option = new BulkOption<Product> { SortByKeys = false };

        // Act
        var batches = BulkCommand.GenerateInsertBatches(DbContext, items, option).ToList();

        // Assert
        Assert.Equal(expected, SentNames(Assert.Single(batches)));
    }

    [Fact]
    public void Should_OrderRowsByKey_ForUpdateDeleteAndMerge()
    {
        // Arrange
        var items = UnsortedProducts();
        var expected = items.OrderBy(x => x.Id).Select(x => x.Name).ToList();

        // Act & Assert
        Assert.Equal(expected, SentNames(BulkCommand.GenerateUpdateBatches(DbContext, items, null).Single()));
        Assert.Equal(expected, SentNames(BulkCommand.GenerateMergeBatches(DbContext, items, null).Single()));
    }
}
