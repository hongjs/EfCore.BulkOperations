using System.Linq.Expressions;

namespace EfCore.BulkOperations;


/// <summary>
///     The configurable options for bulk operations (insert/update) on entities using EfCoreBulkUtils.
/// </summary>
public class BulkOption<T>(
    int? batchSize = null,
    Expression<Func<T, object>>? ignoreOnInsert = null,
    Expression<Func<T, object>>? ignoreOnUpdate = null)
    where T : class
{
    /// <summary>
    ///     Gets or sets the batch size for bulk operations. Defaults to 200 if not specified.
    /// </summary>
    internal int BatchSize { get; set; } = batchSize ?? 200;

    /// <summary>
    ///     Gets or sets an expression that identifies a property on the entity type `T` to be ignored during insert
    ///     operations.
    ///     The expression allows you to selectively skip columns within the insert process without using hardcoded values.
    /// </summary>
    /// <example>
    ///     new BulkOption(ignoreOnInsert: x => new { x.CreatedAt }))
    ///     This would ignore the 'CreatedAt' property during bulk inserts.
    /// </example>
    internal Expression<Func<T, object>>? IgnoreOnInsert { get; set; } = ignoreOnInsert;

    /// <summary>
    ///     Gets or sets an expression that identifies a property on the entity type `T` to be ignored during update
    ///     operations.
    ///     The expression allows you to selectively skip columns within the update process without using hardcoded values.
    /// </summary>
    /// <example>
    ///     new BulkOption(ignoreOnUpdate: x => new { x.CreatedAt }))
    ///     This would ignore the 'CreatedAt' property during bulk updates.
    /// </example>
    internal Expression<Func<T, object>>? IgnoreOnUpdate { get; set; } = ignoreOnUpdate;
}