using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using EfCore.BulkOperations.Extensions;
using EfCore.BulkOperations.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

// Bulk Command XXX
[assembly: InternalsVisibleTo("EfCore.BulkOperations.Test")]
namespace EfCore.BulkOperations;

internal abstract class BulkCommand
{
    private const int BatchSize = 200;
    private const string Prefix = "@p";

    /// <summary>
    ///     Helper method to retrieve Entity metadata from EF Core.
    /// </summary>
    private static EntityInfo GetEntityInfo<T>(DbContext dbContext)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(T));
        if (entityType is null) throw new InvalidOperationException($"Unable to resolve EntityType '{nameof(T)}'");

        var tableName = entityType.GetTableName() ?? "";
        if (string.IsNullOrEmpty(tableName))
            throw new InvalidOperationException($"Unable to resolve TableName from Type '{nameof(T)}'");

        var annotations = dbContext.Model.FindEntityType(typeof(T))?.GetAnnotations().ToList();
        var schema = annotations?.Find(c => c.Name == "Relational:Schema")?.Value?.ToString() ?? "dbo";

        var columns = entityType
            .GetProperties()
            .Select(x =>
            {
                var name = x.GetColumnName();
                var refName = x.Name;
                var isIdentity = x.ValueGenerated == ValueGenerated.OnAddOrUpdate;
                var skipInsert = x.ValueGenerated == ValueGenerated.OnAddOrUpdate;
                var skipUpdate = x.ValueGenerated == ValueGenerated.OnAddOrUpdate;
                var isUniqueIndex = x.IsUniqueIndex();
                var isPrimaryKey = x.IsPrimaryKey();
                var isKey = x.IsKey();

                return new ColumnInfo(name, refName, isPrimaryKey, isUniqueIndex, isKey, isIdentity, skipInsert,
                    skipUpdate)
                {
                    ValueConverter = x.GetValueConverter()
                };
            })
            .ToList();

        return new EntityInfo(tableName, schema, columns);
    }

    /// <summary>
    ///     Extracts ignored property names from an `Expression`
    /// </summary>
    private static string[] GetExpressionFields<T>(Expression<Func<T, object>>? expression)
    {
        if (expression is null) return [];
        var instance = JsonSerializer.Deserialize<T>("{}");
        if (instance is null) return [];

        var expr = expression.Compile();
        var anonymousInstance = expr.Invoke(instance);
        return anonymousInstance.GetType()
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();
    }

    /// <summary>
    ///     Helper method to generate batches of SQL statements and parameters for a bulk operation.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be execute.</param>
    /// <param name="option">Optional configuration for the bulk operation.</param>
    /// <returns>A list of 'BatchData' objects, each containing SQL and parameters for a single batch.</returns>
    internal static List<BatchData> GenerateInsertBatches<T>(DbContext dbContext, IReadOnlyCollection<T> items,
        BulkOption<T>? option)
        where T : class
    {
        if (items.Count == 0) return [];

        var info = GetEntityInfo<T>(dbContext);
        string[] ignoreFields = [];
        if (option?.IgnoreOnInsert is not null) ignoreFields = GetExpressionFields(option.IgnoreOnInsert);

        var columns = info.Columns
            .Where(x => !x.SkipInsert
                        && !ignoreFields.Contains(x.RefName)
            )
            .ToList();

        var offset = 0;
        var batchData = items
            .ToList()
            .ChunkSplit(option?.BatchSize ?? BatchSize)
            .Select(rows =>
            {
                var tmpTable = ToTempTable(columns, rows, offset);
                if (tmpTable is null) return new BatchData(new StringBuilder(), []);

                tmpTable.Sql.Insert(0,
                    @$"INSERT INTO `{info.TableName}`
({string.Join(", ", columns.Select(x => $"`{x.Name}`"))})
SELECT {string.Join(", ", columns.Select(x => $"`{x.Name}`"))}
FROM ");
                tmpTable.Sql.AppendLine("ORDER BY zRowNo");

                offset += rows.Count;
                return new BatchData(tmpTable.Sql, tmpTable.Parameters);
            })
            .ToList();

        return batchData;
    }

    /// <summary>
    ///     Helper method to generate batches of SQL statements and parameters for a bulk operation.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be execute.</param>
    /// <param name="option">Optional configuration for the bulk operation.</param>
    /// <returns>A list of 'BatchData' objects, each containing SQL and parameters for a single batch.</returns>
    internal static List<BatchData> GenerateUpdateBatches<T>(DbContext dbContext, IReadOnlyCollection<T> items,
        BulkOption<T>? option)
        where T : class
    {
        if (items.Count == 0) return [];
        var info = GetEntityInfo<T>(dbContext);

        string[] ignoreFields = [];
        if (option?.IgnoreOnUpdate is not null) ignoreFields = GetExpressionFields(option.IgnoreOnUpdate);

        var columns = info.Columns
            .Where(x => !x.SkipUpdate
                        && !ignoreFields.Contains(x.RefName)
            )
            .ToList();

        var offset = 0;
        var batchData = items
            .ToList()
            .ChunkSplit(option?.BatchSize ?? BatchSize)
            .Select(rows =>
            {
                var tmpTable = ToTempTable(columns, rows, offset);
                if (tmpTable is null) return new BatchData(new StringBuilder(), []);

                tmpTable.Sql.Insert(0,
                    @$"UPDATE `{info.TableName}` AS tb
INNER JOIN ");

                List<string> keys;
                if (option?.UniqueKeys is not null)
                {
                    // Specific custom unique keys
                    var uniqueKeys = GetExpressionFields(option.UniqueKeys);
                    keys = columns
                        .Where(x => uniqueKeys.Contains(x.RefName))
                        .Select(x => x.Name)
                        .ToList();
                }
                else
                {
                    // Auto detects unique keys
                    keys = columns
                        .Where(x => x.IsUniqueIndex)
                        .Select(x => x.Name)
                        .ToList();
                }

                keys
                    .ForEachWithIndex((key, index) =>
                    {
                        tmpTable.Sql.Append(index == 0 ? "ON " : "AND ");
                        tmpTable.Sql.AppendLine($"tb.`{key}` = tmp.`{key}`");
                    });

                tmpTable.Sql.Append("SET ");
                columns
                    .Where(x => !x.IsPrimaryKey)
                    .ToList()
                    .ForEach(col =>
                    {
                        tmpTable.Sql.AppendLine($"tb.`{col.Name}` = tmp.`{col.Name}`,");
                    });
                tmpTable.Sql.Remove(tmpTable.Sql.Length - 2, 1);

                offset += rows.Count;
                return new BatchData(tmpTable.Sql, tmpTable.Parameters);
            })
            .ToList();

        return batchData;
    }

    /// <summary>
    ///     Helper method to generate batches of SQL statements and parameters for a bulk operation.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be execute.</param>
    /// <param name="option">Optional configuration for the bulk operation.</param>
    /// <returns>A list of 'BatchData' objects, each containing SQL and parameters for a single batch.</returns>
    internal static List<BatchData> GenerateDeleteBatches<T>(DbContext dbContext, IReadOnlyCollection<T> items,
        BulkOption<T>? option)
        where T : class
    {
        if (items.Count == 0) return [];
        var info = GetEntityInfo<T>(dbContext);

        List<ColumnInfo> columns;
        if (option?.UniqueKeys is null)
        {
            // Auto detects unique keys
            columns = info.Columns
                .Where(x => x.IsUniqueIndex)
                .ToList();
        }
        else
        {
            // Specific custom unique keys
            var uniqueKeys = GetExpressionFields(option.UniqueKeys);
            columns = info.Columns
                .Where(x => uniqueKeys.Contains(x.RefName))
                .ToList();
        }



        var offset = 0;
        var batchData = items
            .ToList()
            .ChunkSplit(option?.BatchSize ?? BatchSize)
            .Select(rows =>
            {
                var tmpTable = ToTempTable(columns, rows, offset);
                if (tmpTable is null) return new BatchData(new StringBuilder(), []);

                tmpTable.Sql.Insert(0,
                    @$"DELETE tb
FROM `{info.TableName}` AS tb
INNER JOIN ");
                columns
                    .ForEachWithIndex((key, index) =>
                    {
                        tmpTable.Sql.Append(index == 0 ? "ON " : "AND ");
                        tmpTable.Sql.AppendLine($"tb.`{key.Name}` = tmp.`{key.Name}`");
                    });


                offset += rows.Count;
                return new BatchData(tmpTable.Sql, tmpTable.Parameters);
            })
            .ToList();

        return batchData;
    }

    /// <summary>
    ///     Helper method to generate batches of SQL statements and parameters for a bulk operation.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be execute.</param>
    /// <param name="option">Optional configuration for the bulk operation.</param>
    /// <returns>A list of 'BatchData' objects, each containing SQL and parameters for a single batch.</returns>
    internal static List<BatchData> GenerateMergeBatches<T>(DbContext dbContext,
        IReadOnlyCollection<T> items,
        BulkOption<T>? option)
        where T : class
    {
        if (items.Count == 0) return [];

        var info = GetEntityInfo<T>(dbContext);
        string[] ignoreInsertFields = [];
        if (option?.IgnoreOnInsert is not null) ignoreInsertFields = GetExpressionFields(option.IgnoreOnInsert);
        var insertCols = info.Columns
            .Where(x => x is { SkipInsert: false }
                        && !ignoreInsertFields.Contains(x.RefName)
            )
            .ToList();

        string[] ignoreUpdateFields = [];
        if (option?.IgnoreOnUpdate is not null) ignoreUpdateFields = GetExpressionFields(option.IgnoreOnUpdate);
        var updateCols = info.Columns
            .Where(x => x is { IsPrimaryKey: false, IsUniqueIndex: false, SkipUpdate: false }
                        && !ignoreUpdateFields.Contains(x.RefName)
            )
            .ToList();

        var offset = 0;
        var combineColumns = insertCols.Concat(updateCols)
            .GroupBy(x => x.Name)
            .Select(g => g.First())
            .ToList();

        var batchData = items
            .ToList()
            .ChunkSplit(option?.BatchSize ?? BatchSize)
            .Select(rows =>
            {
                var tmpTable = ToTempTable(combineColumns, rows, offset);
                if (tmpTable is null) return new BatchData(new StringBuilder(), []);

                tmpTable.Sql.Insert(0,
                    @$"INSERT INTO `{info.TableName}`
({string.Join(", ", insertCols.Select(x => $"`{x.Name}`"))})
SELECT {string.Join(", ", insertCols.Select(x => $"`{x.Name}`"))}
FROM ");
                tmpTable.Sql.AppendLine(" ON DUPLICATE KEY UPDATE ");
                updateCols
                    .ForEach(x =>
                    {
                        tmpTable.Sql.AppendLine($" `{info.TableName}`.`{x.Name}` = tmp.`{x.Name}`,");
                    });

                tmpTable.Sql.Remove(tmpTable.Sql.Length - 2, 2);
                tmpTable.Sql.AppendLine();
                offset += rows.Count;
                return new BatchData(tmpTable.Sql, tmpTable.Parameters);
            })
            .ToList();


        return batchData;
    }


    /// <summary>
    ///     Generates a temporary 'tmp' table definition (SQL and parameters)
    ///     for use in bulk operations.
    /// </summary>
    private static TempTable? ToTempTable<T>(
        IReadOnlyCollection<ColumnInfo> columns,
        IReadOnlyCollection<T> rows,
        int offset)
        where T : class
    {
        if (rows.Count == 0) return null;
        List<SqlParameter> parameters = [];
        var sql = new StringBuilder("(");
        sql.AppendLine();
        rows.ForEachWithIndex((row, rowIndex) =>
        {
            sql.Append(rowIndex == 0 ? "SELECT " : "UNION ALL SELECT ");
            List<SqlParameter> list = [];
            var type = row.GetType();
            var colIndex = 0;
            columns.ToList().ForEach(column =>
            {
                var value = type.GetProperty(column.RefName)?.GetValue(row);
                if (column.ValueConverter is not null)
                    value = column.ValueConverter.ConvertToProvider(value);

                var paramName = $"{Prefix}{rowIndex}_{colIndex}".ToString();
                list.Add(new SqlParameter(paramName, value));
                sql.Append($"{paramName} AS `{column.Name}`, ");
                colIndex++;
            });
            sql.AppendLine($"{offset + rowIndex} AS zRowNo");

            parameters.AddRange(list);
        });

        sql.AppendLine(") AS tmp");
        return new TempTable(sql, parameters);
    }
}