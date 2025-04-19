using System.Linq.Expressions;

namespace EfCore.BulkOperations;

/// <summary>
///     The configurable options for bulk operations (insert/update) on entities using EfCoreBulkUtils.
/// </summary>
public class BulkOption<T>(
    int? batchSize = null,
    int? commandTimeout = null,
    Expression<Func<T, object>>? ignoreOnInsert = null,
    Expression<Func<T, object>>? ignoreOnUpdate = null,
    // Expression<Func<T, object>>? fieldsToUpdate = null,
    Expression<Func<T, object>>? uniqueKeys = null
) where T : class
{
    /// <summary>
    ///     Gets or sets the batch size for bulk operations. Defaults to 200 if not specified.
    /// </summary>
    public int BatchSize { get; set; } = batchSize ?? 200;

    /// <summary>
    ///     Gets or sets the wait time (in seconds) before terminating the attempt to execute the command and generating an
    ///     error.
    /// </summary>
    public int? CommandTimeout { get; set; } = commandTimeout;

    /// <summary>
    ///     Gets or sets an expression that identifies a property on the entity type `T` to be ignored during insert
    ///     operations.
    ///     The expression allows you to selectively skip columns within the insert process without using hardcoded values.
    /// </summary>
    /// <example>
    ///     new BulkOption(ignoreOnInsert: x => new { x.CreatedAt }))
    ///     This would ignore the 'CreatedAt' property during bulk inserts.
    /// </example>
    public Expression<Func<T, object>>? IgnoreOnInsert { get; set; } = ignoreOnInsert;

    /// <summary>
    ///     Gets or sets an expression that identifies a property on the entity type `T` to be ignored during update
    ///     operations.
    ///     The expression allows you to selectively skip columns within the update process without using hardcoded values.
    /// </summary>
    /// <example>
    ///     new BulkOption(ignoreOnUpdate: x => new { x.CreatedAt }))
    ///     This would ignore the 'CreatedAt' property during bulk updates.
    /// </example>
    public Expression<Func<T, object>>? IgnoreOnUpdate { get; set; } = ignoreOnUpdate;

    // TODO: Implement support of updating specific fields.
    // /// <summary>
    // ///     Gets or sets an Expression that specifies properties on the entity type `T`
    // ///     which should be explicitly updated during update operations.
    // ///     This allows you to selectively update specific columns without relying on hard-coded values.
    // /// </summary>
    // /// <example>
    // ///     new BulkOption(fieldsToUpdate: x => new { x.Amount }))
    // ///     This would update only the 'Amount' property during bulk updates.
    // /// </example>
    // public Expression<Func<T, object>>? FieldsToUpdate
    // {
    //     get => fieldsToUpdate;
    //     set => throw new NotImplementedException();
    // }

    /// <summary>
    ///     Gets or sets an expression that identifies a property on the entity type 'T' as a custom unique key for update or
    ///     delete operations.
    /// </summary>
    public Expression<Func<T, object>>? UniqueKeys { get; set; } = uniqueKeys;
}