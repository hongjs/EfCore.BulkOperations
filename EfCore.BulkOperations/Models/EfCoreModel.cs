using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EfCore.BulkOperations.Models;


/// <summary>
///     Metadata about an entity in the database, including its table and schema name.
/// </summary>
internal record EntityInfo(string TableName, string SchemaName, List<ColumnInfo> Columns);

/// <summary>
///     Details of a single database column associated with an entity.
/// </summary>
internal record ColumnInfo(
    string Name,
    string RefName,
    bool IsPrimaryKey,
    bool IsUniqueIndex,
    bool IsKey,
    bool IsIdentity,
    bool SkipInsert,
    bool SkipUpdate
)
{
    /// <summary>
    ///     An optional value converter for custom data type transformations.
    /// </summary>
    public ValueConverter? ValueConverter { get; init; }
}

/// <summary>
///     The SQL parameter along with its value.
/// </summary>
internal record SqlParameter(string Name, object? Value);

/// <summary>
///     The batch of data to be inserted or updated, including the SQL query and related parameters.
/// </summary>
internal record BatchData(StringBuilder Sql, IReadOnlyCollection<SqlParameter> Parameters)
{
    /// <summary>
    ///     The number of rows affected by the execution of this batch (if available).
    /// </summary>
    public int? SuccessCount { get; set; }
}

/// <summary>
///     The temporary temp table used during bulk operations, including its SQL definition and parameters.
/// </summary>
internal record TempTable(StringBuilder Sql, IReadOnlyCollection<SqlParameter> Parameters);