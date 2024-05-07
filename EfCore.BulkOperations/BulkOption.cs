using System.Linq.Expressions;

namespace EfCore.BulkOperations;


/// <summary>
///     The configurable options for bulk operations (insert/update) on entities using EfCoreBulkUtils.
/// </summary>
public class BulkOption<T> where T : class
{
    public BulkOption(int? batchSize = null,
        Expression<Func<T, object>>? ignoreOnInsert = null,
        Expression<Func<T, object>>? ignoreOnUpdate = null,
        Expression<Func<T, object>>? uniqueKeys = null)
    {
        BatchSize = batchSize ?? 200;
        IgnoreOnInsert = ignoreOnInsert;
        IgnoreOnUpdate = ignoreOnUpdate;
        UniqueKeys = uniqueKeys;
    }

    /// <summary>
    ///     Gets or sets the batch size for bulk operations. Defaults to 200 if not specified.
    /// </summary>
    public int BatchSize { get; private set; }

    /// <summary>
    ///     Gets or sets an expression that identifies a property on the entity type `T` to be ignored during insert
    ///     operations.
    ///     The expression allows you to selectively skip columns within the insert process without using hardcoded values.
    /// </summary>
    /// <example>
    ///     new BulkOption(ignoreOnInsert: x => new { x.CreatedAt }))
    ///     This would ignore the 'CreatedAt' property during bulk inserts.
    /// </example>
    public Expression<Func<T, object>>? IgnoreOnInsert { get; private set; }

    /// <summary>
    ///     Gets or sets an expression that identifies a property on the entity type `T` to be ignored during update
    ///     operations.
    ///     The expression allows you to selectively skip columns within the update process without using hardcoded values.
    /// </summary>
    /// <example>
    ///     new BulkOption(ignoreOnUpdate: x => new { x.CreatedAt }))
    ///     This would ignore the 'CreatedAt' property during bulk updates.
    /// </example>
    public Expression<Func<T, object>>? IgnoreOnUpdate { get; private set; }

    /// <summary>
    ///     Gets or sets an expression that identifies a property on the entity type 'T' as a custom unique key for update or
    ///     delete operations.
    /// </summary>
    public Expression<Func<T, object>>? UniqueKeys { get; private set; }
}