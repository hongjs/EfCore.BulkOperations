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
    Expression<Func<T, object>>? uniqueKeys = null,
    bool sortByKeys = true
) where T : class
{
    /// <summary>
    ///     The batch size used when the caller passes no option at all. Internal so that
    ///     <see cref="BulkCommand" /> reads it from here rather than keeping a second copy: the two
    ///     were separate constants that happened to hold the same number, so a change to this one
    ///     silently did nothing for callers who passed no option.
    /// </summary>
    internal const int DefaultBatchSize = 500;

    private int _batchSize = ValidateBatchSize(batchSize ?? DefaultBatchSize, nameof(batchSize));

    /// <summary>
    ///     Gets or sets how many rows go into one statement. Defaults to 500. Must be greater than zero.
    ///     Measured on a 50,000 row insert, 500 was the fastest setting and about 15% quicker than the
    ///     200 this used to default to; above it the curve turns back up as the statements grow. The
    ///     right value depends on how wide the rows are, so a table of few narrow columns can afford
    ///     more than one carrying long strings.
    /// </summary>
    public int BatchSize
    {
        get => _batchSize;
        set => _batchSize = ValidateBatchSize(value, nameof(value));
    }

    private static int ValidateBatchSize(int value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, value,
                "BatchSize must be greater than zero.");
        return value;
    }

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

    /// <summary>
    ///     Gets or sets whether rows are ordered by their key columns before being sent. Defaults to true.
    ///     InnoDB stores rows in primary key order, so sending them in key order keeps each statement working
    ///     on a narrow, contiguous part of the index. Sending them in an arbitrary order - which is what a
    ///     random Guid key gives you - walks the whole index, and once the index outgrows the buffer pool
    ///     every row costs a page read.
    ///     Turn this off if the rows must reach the database in the order they were given, or if ordering the
    ///     keys client-side is more expensive than the round trips it saves.
    /// </summary>
    public bool SortByKeys { get; set; } = sortByKeys;
}