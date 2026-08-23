using System.Data;
using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.Test.Setup;

namespace EfCore.BulkOperations.Test;

/// <summary>
///     SQL generation at the edges of the model: entities without a unique index, with nothing but
///     a key, with no key at all, with shadow properties, and with renamed columns - plus the shapes
///     of expression the options accept. Model-only, so no database is needed.
/// </summary>
[Trait("Category", "Unit")]
public class EdgeCaseTest : IDisposable
{
    private EdgeCaseDbContext DbContext { get; } = EdgeCaseDbContext.Create();

    public void Dispose()
    {
        DbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string Lf(string sql)
    {
        return sql.Replace("\r\n", "\n");
    }

    #region Key resolution

    [Fact]
    public void Should_FallBackToPrimaryKey_WhenEntityHasNoUniqueIndex()
    {
        // Arrange
        var items = new List<PkOnlyEntity> { new() };

        // Act
        var update = BulkCommand.GenerateUpdateBatches(DbContext, items, null).Single();
        var delete = BulkCommand.GenerateDeleteBatches(DbContext, items, null).Single();

        // Assert
        Assert.Contains("ON tb.`Id` = tmp.`Id`", Lf(update.Sql.ToString()));
        Assert.Contains("SET tb.`Name` = tmp.`Name`", Lf(update.Sql.ToString()));
        Assert.Contains("ON tb.`Id` = tmp.`Id`", Lf(delete.Sql.ToString()));
        Assert.Single(delete.Parameters);
    }

    [Fact]
    public void ShouldError_WhenEntityHasNoKeyAtAll()
    {
        // Arrange
        var items = new List<KeylessEntity> { new() };

        // Act & Assert
        Assert.Throws<MissingPrimaryKeyException>(() =>
            BulkCommand.GenerateUpdateBatches(DbContext, items, null).ToList());
        Assert.Throws<MissingPrimaryKeyException>(() =>
            BulkCommand.GenerateDeleteBatches(DbContext, items, null).ToList());
    }

    [Fact]
    public void ShouldError_WhenUniqueKeysNamesAnUnmappedProperty()
    {
        // Arrange: Unmapped is a CLR property the model ignores, so there is no column to match on.
        var items = new List<PkOnlyEntity> { new() };
        var option = new BulkOption<PkOnlyEntity>(uniqueKeys: x => new { x.Unmapped });

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            BulkCommand.GenerateUpdateBatches(DbContext, items, option).ToList());

        // Assert
        Assert.Contains("'Unmapped'", exception.Message);
        Assert.Contains("not a mapped property of 'PkOnlyEntity'", exception.Message);
    }

    #endregion

    #region Ignoring a key on update

    [Fact]
    public void Should_KeepKeyInDerivedTable_WhenItIsIgnoredOnUpdate()
    {
        // The JOIN needs the key even when the caller asks to leave it out of SET.
        using var products = ModelOnlyDbContext.Create();
        var items = new List<Product> { new("Test", 1m) };
        var option = new BulkOption<Product>(ignoreOnUpdate: x => new { x.Id });

        // Act
        var sql = Lf(BulkCommand.GenerateUpdateBatches(products, items, option).Single().Sql.ToString());

        // Assert
        Assert.Contains("@p0_0 AS `Id`", sql);
        Assert.Contains("ON tb.`Id` = tmp.`Id`", sql);
        Assert.DoesNotContain("tb.`Id` = tmp.`Id`,", sql); // not in SET
        Assert.Contains("SET tb.`CreatedAt` = tmp.`CreatedAt`", sql);
    }

    #endregion

    #region Entities with nothing but a key

    [Fact]
    public void ShouldError_WhenUpdateHasNoColumnToSet()
    {
        // Arrange
        var items = new List<KeyOnlyEntity> { new() };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BulkCommand.GenerateUpdateBatches(DbContext, items, null).ToList());

        // Assert
        Assert.Contains("no column to update", exception.Message);
    }

    [Fact]
    public void Should_MergeKeyOnlyEntity_AsInsertIfAbsent()
    {
        // Arrange
        var items = new List<KeyOnlyEntity> { new() };

        // Act
        var sql = Lf(BulkCommand.GenerateMergeBatches(DbContext, items, null).Single().Sql.ToString());

        // Assert: MySQL needs at least one assignment after ON DUPLICATE KEY UPDATE.
        Assert.EndsWith(" ON DUPLICATE KEY UPDATE\n `KeyOnly`.`Id` = `KeyOnly`.`Id`\n", sql);
    }

    #endregion

    #region Shadow properties and renamed columns

    [Fact]
    public void ShouldError_WhenEntityHasShadowProperty()
    {
        // Arrange
        var items = new List<ShadowEntity> { new() };

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            BulkCommand.GenerateInsertBatches(DbContext, items, null).ToList());

        // Assert
        Assert.Contains("shadow property 'Tenant'", exception.Message);
        Assert.Contains("ShadowEntity", exception.Message);
    }

    [Fact]
    public void Should_UseColumnNamesInSqlAndPropertyNamesForValues()
    {
        // Arrange
        var items = new List<RenamedEntity> { new() { Code = "abc" } };

        // Act
        var batch = BulkCommand.GenerateInsertBatches(DbContext, items, null).Single();

        // Assert
        Assert.Contains("(`renamed_id`, `renamed_code`)", batch.Sql.ToString());
        Assert.Equal("abc", batch.Parameters.Single(p => p.Name == "@p0_1").Value);
    }

    [Fact]
    public void Should_NameTheTypeInTheError_WhenItIsNotAnEntity()
    {
        // Arrange
        var items = new List<DummyEntity> { new("x") };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BulkCommand.GenerateInsertBatches(DbContext, items, null).ToList());

        // Assert
        Assert.Equal("Unable to resolve EntityType 'DummyEntity'", exception.Message);
    }

    #endregion

    #region Option expressions

    [Fact]
    public void Should_AcceptSinglePropertyExpression()
    {
        // x => x.CreatedAt used to be compiled, run, and have DateTime's own properties read back,
        // so the option silently ignored nothing.
        var fields = BulkCommand.GetExpressionFields<Product>(x => x.CreatedAt);
        Assert.Equal(new[] { "CreatedAt" }, fields);
    }

    [Fact]
    public void Should_AcceptAnonymousObjectExpression()
    {
        var fields = BulkCommand.GetExpressionFields<Product>(x => new { x.CreatedAt, x.Name });
        Assert.Equal(new[] { "CreatedAt", "Name" }, fields);
    }

    [Fact]
    public void Should_IgnoreSinglePropertyOnUpdate()
    {
        using var products = ModelOnlyDbContext.Create();
        var items = new List<Product> { new("Test", 1m) };
        var option = new BulkOption<Product>(ignoreOnUpdate: x => x.CreatedAt);

        var batch = BulkCommand.GenerateUpdateBatches(products, items, option).Single();

        Assert.DoesNotContain("CreatedAt", batch.Sql.ToString());
        Assert.Equal(3, batch.Parameters.Count);
    }

    [Fact]
    public void ShouldError_WhenExpressionIsNotAPropertyAccess()
    {
        Assert.Throws<ArgumentException>(() => BulkCommand.GetExpressionFields<Product>(x => x.Name.ToUpper()));
        Assert.Throws<ArgumentException>(() => BulkCommand.GetExpressionFields<Product>(x => new { Upper = x.Name.ToUpper() }));
        Assert.Throws<ArgumentException>(() => BulkCommand.GetExpressionFields<Product>(x => "literal"));
    }

    #endregion
}
