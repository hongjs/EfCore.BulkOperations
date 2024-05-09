using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.Test.Setup;

namespace EfCore.BulkOperations.Test;

internal record DummyEntity(string Id);

public class BulkCommandTest(IntegrationTestFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public void Should_GenerateInsertScript()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m) };
        var expectedSql = @"INSERT INTO `Products`
(`Id`, `CreatedAt`, `Name`, `Price`)
SELECT `Id`, `CreatedAt`, `Name`, `Price`
FROM (
SELECT @p0_0 AS `Id`, @p0_1 AS `CreatedAt`, @p0_2 AS `Name`, @p0_3 AS `Price`, 0 AS zRowNo
) AS tmp
ORDER BY zRowNo
";

        // Act
        var batches = BulkCommand.GenerateInsertBatches(DbContext, items, null);

        // Assert
        Assert.Single(batches);
        Assert.Equal(4, batches[0].Parameters.Count);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateInsertScriptAndIgnoreCreatedAt()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m) };
        var expectedSql = @"INSERT INTO `Products`
(`Id`, `Name`, `Price`)
SELECT `Id`, `Name`, `Price`
FROM (
SELECT @p0_0 AS `Id`, @p0_1 AS `Name`, @p0_2 AS `Price`, 0 AS zRowNo
) AS tmp
ORDER BY zRowNo
";
        var option = new BulkOption<Product>(
            ignoreOnInsert: x => new { x.CreatedAt }
        );

        // Act
        var batches = BulkCommand.GenerateInsertBatches(DbContext, items, option);

        // Assert
        Assert.Single(batches);
        Assert.Equal(3, batches[0].Parameters.Count);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateUpdateScript()
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

        // Act
        var batches = BulkCommand.GenerateUpdateBatches(DbContext, items, null);

        // Assert
        Assert.Single(batches);
        Assert.Equal(4, batches[0].Parameters.Count);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
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
        var batches = BulkCommand.GenerateUpdateBatches(DbContext, items, option);

        // Assert
        Assert.Single(batches);
        Assert.Equal(3, batches[0].Parameters.Count);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
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
        var batches = BulkCommand.GenerateUpdateBatches(DbContext, items, option);

        // Assert
        Assert.Single(batches);
        Assert.Equal(4, batches[0].Parameters.Count);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
    }


    [Fact]
    public void Should_GenerateDeleteScript()
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


        // Act
        var batches = BulkCommand.GenerateDeleteBatches(DbContext, items, null);

        // Assert
        Assert.Single(batches);
        Assert.Single(batches[0].Parameters);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
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
        var batches = BulkCommand.GenerateDeleteBatches(DbContext, items, option);

        // Assert
        Assert.Single(batches);
        Assert.Single(batches[0].Parameters);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
    }

    [Fact]
    public void Should_GenerateMergeScript()
    {
        // Arrange
        var items = new List<Product> { new("Test", 123.45m) };
        var expectedSql = @"INSERT INTO `Products`
(`Id`, `CreatedAt`, `Name`, `Price`)
SELECT `Id`, `CreatedAt`, `Name`, `Price`
FROM (
SELECT @p0_0 AS `Id`, @p0_1 AS `CreatedAt`, @p0_2 AS `Name`, @p0_3 AS `Price`, 0 AS zRowNo
) AS tmp
 ON DUPLICATE KEY UPDATE 
 `Products`.`CreatedAt` = tmp.`CreatedAt`,
 `Products`.`Name` = tmp.`Name`,
 `Products`.`Price` = tmp.`Price`
";

        // Act
        var batches = BulkCommand.GenerateMergeBatches(DbContext, items, null);

        // Assert
        Assert.Single(batches);
        Assert.Equal(4, batches[0].Parameters.Count);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
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
        var batches = BulkCommand.GenerateMergeBatches(DbContext, items, option);

        // Assert
        Assert.Single(batches);
        Assert.Equal(3, batches[0].Parameters.Count);
        Assert.Equal(expectedSql, batches[0].Sql.ToString());
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
        var batches = BulkCommand.GenerateInsertBatches(DbContext, items, option);

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
        Assert.Throws<InvalidOperationException>(() => { BulkCommand.GenerateInsertBatches(DbContext, items, null); });
    }
}