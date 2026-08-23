using System.Data;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using EfCore.BulkOperations.Extensions;
using EfCore.BulkOperations.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

[assembly: InternalsVisibleTo("EfCore.BulkOperations.Test")]
namespace EfCore.BulkOperations;

internal static class BulkCommand
{
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
                var skipInsert = x.ValueGenerated == ValueGenerated.OnAddOrUpdate;
                var skipUpdate = x.ValueGenerated == ValueGenerated.OnAddOrUpdate;
                var isUniqueIndex = x.IsUniqueIndex();
                var isPrimaryKey = x.IsPrimaryKey();
                var isKey = x.IsKey();

                // GetValueConverter only returns a converter configured directly on the property,
                // as HasConversion(toProvider, fromProvider) does. A conversion expressed as a
                // provider type - HasConversion<string>() on an enum is the common case - lives on
                // the type mapping instead, and reading only the first sends the CLR value raw.
                var converter = x.GetValueConverter() ?? x.FindTypeMapping()?.Converter;

                return new ColumnInfo(name, refName, isPrimaryKey, isUniqueIndex, isKey, skipInsert, skipUpdate)
                {
                    ValueConverter = converter
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
    /// <param name="items">The collection of entities to be executed.</param>
    /// <param name="option">Optional configuration for the bulk operation.</param>
    /// <returns>A list of 'BatchData' objects, each containing SQL and parameters for a single batch.</returns>
    internal static IEnumerable<BatchData> GenerateInsertBatches<T>(DbContext dbContext, IReadOnlyCollection<T> items,
        BulkOption<T>? option)
        where T : class
    {
        if (items.Count == 0) yield break;

        var info = GetEntityInfo<T>(dbContext);
        string[] ignoreFields = [];
        if (option?.IgnoreOnInsert is not null) ignoreFields = GetExpressionFields(option.IgnoreOnInsert);

        var columns = info.Columns
            .Where(x => !x.SkipInsert
                        && !ignoreFields.Contains(x.RefName)
            )
            .ToList();

        var rows = items.ToList();
        if (option?.SortByKeys ?? true)
            rows = SortByKeys(rows, info.Columns.Where(x => x.IsPrimaryKey).ToList());

        var chunk = rows.ChunkSplit(option?.BatchSize ?? BulkOption<T>.DefaultBatchSize);
        foreach (var t in chunk)
        {
            var tmpTable = ToInsertTemp(columns, t);
            if (tmpTable is null) continue;

            tmpTable.Sql.Insert(0, @$"INSERT INTO `{info.TableName}`
({string.Join(", ", columns.Select(x => $"`{x.Name}`"))})
VALUES
");
            yield return new BatchData(tmpTable.Sql, tmpTable.Parameters);
        }
    }

    /// <summary>
    ///     Helper method to generate batches of SQL statements and parameters for a bulk operation.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be executed.</param>
    /// <param name="option">Optional configuration for the bulk operation.</param>
    /// <returns>A list of 'BatchData' objects, each containing SQL and parameters for a single batch.</returns>
    internal static IEnumerable<BatchData> GenerateUpdateBatches<T>(DbContext dbContext, IReadOnlyCollection<T> items,
        BulkOption<T>? option)
        where T : class
    {
        if (items.Count == 0) yield break;
        var info = GetEntityInfo<T>(dbContext);

        string[] ignoreFields = [];
        if (option?.IgnoreOnUpdate is not null) ignoreFields = GetExpressionFields(option.IgnoreOnUpdate);

        var columns = info.Columns
            .Where(x => !x.SkipUpdate
                        && !ignoreFields.Contains(x.RefName)
            )
            .ToList();

        List<ColumnInfo> keyColumns;
        if (option?.UniqueKeys is not null)
        {
            // Specific custom unique keys
            var uniqueKeys = GetExpressionFields(option.UniqueKeys);
            keyColumns = columns.Where(x => uniqueKeys.Contains(x.RefName)).ToList();
        }
        else
        {
            // Auto detects unique keys
            keyColumns = info.Columns.Where(x => x.IsUniqueIndex).ToList();
        }

        if (keyColumns.Count == 0)
            throw new MissingPrimaryKeyException(
                "A unique key in the database is required to perform a bulk operation");

        var keys = keyColumns.Select(x => x.Name).ToList();

        var rows = items.ToList();
        if (option?.SortByKeys ?? true) rows = SortByKeys(rows, keyColumns);

        var offset = 0;
        var chunkList = rows.ChunkSplit(option?.BatchSize ?? BulkOption<T>.DefaultBatchSize);

        foreach (var chunk in chunkList)
        {
            var tmpTable = ToTempTable(columns, chunk, offset);
            if (tmpTable is null) continue;

            tmpTable.Sql.Insert(0,
                @$"UPDATE `{info.TableName}` AS tb
INNER JOIN ");

            var index = 0;
            foreach (var key in keys)
            {
                tmpTable.Sql.Append(index++ == 0 ? "ON " : "AND ");
                tmpTable.Sql.AppendLine($"tb.`{key}` = tmp.`{key}`");
            }

            tmpTable.Sql.Append("SET ");
            var setIndex = 0;
            foreach (var col in columns.Where(x => !x.IsPrimaryKey))
            {
                if (setIndex++ > 0) tmpTable.Sql.AppendLine(",");
                tmpTable.Sql.Append($"tb.`{col.Name}` = tmp.`{col.Name}`");
            }

            tmpTable.Sql.AppendLine();

            offset += chunk.Count;
            yield return new BatchData(tmpTable.Sql, tmpTable.Parameters);
        }
    }

    /// <summary>
    ///     Helper method to generate batches of SQL statements and parameters for a bulk operation.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be executed.</param>
    /// <param name="option">Optional configuration for the bulk operation.</param>
    /// <returns>A list of 'BatchData' objects, each containing SQL and parameters for a single batch.</returns>
    internal static IEnumerable<BatchData> GenerateDeleteBatches<T>(DbContext dbContext, IReadOnlyCollection<T> items,
        BulkOption<T>? option)
        where T : class
    {
        if (items.Count == 0) yield break;
        var info = GetEntityInfo<T>(dbContext);

        List<ColumnInfo> keys;
        if (option?.UniqueKeys is null)
        {
            // Auto detects unique keys
            keys = info.Columns
                .Where(x => x.IsUniqueIndex)
                .ToList();
        }
        else
        {
            // Specific custom unique keys
            var uniqueKeys = GetExpressionFields(option.UniqueKeys);
            keys = info.Columns
                .Where(x => uniqueKeys.Contains(x.RefName))
                .ToList();
        }

        if (keys.Count == 0)
            throw new MissingPrimaryKeyException(
                "A unique key in the database is required to perform a bulk operation");

        var rows = items.ToList();
        if (option?.SortByKeys ?? true) rows = SortByKeys(rows, keys);

        var chunkList = rows.ChunkSplit(option?.BatchSize ?? BulkOption<T>.DefaultBatchSize);
        var offset = 0;

        foreach (var chunk in chunkList)
        {
            var tmpTable = ToTempTable(keys, chunk, offset);
            if (tmpTable is null) continue;

            tmpTable.Sql.Insert(0,
                @$"DELETE tb
FROM `{info.TableName}` AS tb
INNER JOIN ");
            var index = 0;
            foreach (var key in keys)
            {
                tmpTable.Sql.Append(index++ == 0 ? "ON " : "AND ");
                tmpTable.Sql.AppendLine($"tb.`{key.Name}` = tmp.`{key.Name}`");
            }

            offset += chunk.Count;
            yield return new BatchData(tmpTable.Sql, tmpTable.Parameters);
        }
    }

    /// <summary>
    ///     Helper method to generate batches of SQL statements and parameters for a bulk operation.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be executed.</param>
    /// <param name="option">Optional configuration for the bulk operation.</param>
    /// <returns>A list of 'BatchData' objects, each containing SQL and parameters for a single batch.</returns>
    internal static IEnumerable<BatchData> GenerateMergeBatches<T>(DbContext dbContext,
        IReadOnlyCollection<T> items,
        BulkOption<T>? option)
        where T : class
    {
        if (items.Count == 0) yield break;

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

        var mergeKeys = info.Columns.Where(x => x.IsPrimaryKey).ToList();
        if (mergeKeys.Count == 0) mergeKeys = info.Columns.Where(x => x.IsUniqueIndex).ToList();

        var rows = items.ToList();
        if (option?.SortByKeys ?? true) rows = SortByKeys(rows, mergeKeys);

        var chunkList = rows.ChunkSplit(option?.BatchSize ?? BulkOption<T>.DefaultBatchSize);

        foreach (var chunk in chunkList)
        {
            var tmpTable = ToTempTable(combineColumns, chunk, offset);
            if (tmpTable is null) continue;

            tmpTable.Sql.Insert(0,
                @$"INSERT INTO `{info.TableName}`
({string.Join(", ", insertCols.Select(x => $"`{x.Name}`"))})
SELECT {string.Join(", ", insertCols.Select(x => $"`{x.Name}`"))}
FROM ");
            tmpTable.Sql.AppendLine(" ON DUPLICATE KEY UPDATE");
            var updateIndex = 0;
            foreach (var x in updateCols)
            {
                if (updateIndex++ > 0) tmpTable.Sql.AppendLine(",");
                tmpTable.Sql.Append($" `{info.TableName}`.`{x.Name}` = tmp.`{x.Name}`");
            }

            tmpTable.Sql.AppendLine();
            offset += chunk.Count;
            yield return new BatchData(tmpTable.Sql, tmpTable.Parameters);
        }
    }

    /// <summary>
    ///     Orders rows by their key columns before they are batched.
    ///     InnoDB stores rows in primary key order, so sending them in key order keeps each statement
    ///     working on a narrow, contiguous part of the index; sending them in an arbitrary order walks
    ///     the whole index, and once the index outgrows the buffer pool every row costs a page read.
    ///     EF Core sorts its own commands by key, which is most of why it does not degrade the same way.
    ///     The sort is stable, so rows whose keys compare equal - including rows whose key is generated by
    ///     the database and therefore still at its default value - keep the order they were given in.
    /// </summary>
    private static List<T> SortByKeys<T>(List<T> items, IReadOnlyCollection<ColumnInfo> keyColumns)
        where T : class
    {
        if (items.Count < 2 || keyColumns.Count == 0) return items;

        var type = items[0].GetType();
        var accessors = keyColumns
            .Select(column => type.GetProperty(column.RefName))
            .Where(property => property is not null)
            .ToList();
        if (accessors.Count == 0) return items;

        IOrderedEnumerable<T>? ordered = null;
        foreach (var accessor in accessors)
        {
            var property = accessor!;
            ordered = ordered is null
                ? items.OrderBy(item => property.GetValue(item), KeyComparer.Instance)
                : ordered.ThenBy(item => property.GetValue(item), KeyComparer.Instance);
        }

        return ordered!.ToList();
    }

    /// <summary>
    ///     Compares key values without assuming they can be compared: anything that is not a directly
    ///     comparable value of the same type is treated as equal, which leaves those rows where they were.
    /// </summary>
    private sealed class KeyComparer : IComparer<object?>
    {
        internal static readonly KeyComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            if (x.GetType() != y.GetType()) return 0;
            return x is IComparable comparable ? comparable.CompareTo(y) : 0;
        }
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
        var rowIndex = 0;
        foreach (var row in rows)
        {
            sql.Append(rowIndex == 0 ? "SELECT " : "UNION ALL SELECT ");
            List<SqlParameter> list = [];
            var type = row.GetType();
            var colIndex = 0;
            foreach (var column in columns)
            {
                var paramName = ProcessParameter(type, column, row, rowIndex, colIndex, list);
                sql.Append($"{paramName} AS `{column.Name}`, ");
                colIndex++;
            }

            sql.AppendLine($"{offset + rowIndex} AS zRowNo");

            parameters.AddRange(list);
            rowIndex++;
        }

        sql.AppendLine(") AS tmp");
        return new TempTable(sql, parameters);
    }

    private static TempTable? ToInsertTemp<T>(
        IReadOnlyCollection<ColumnInfo> columns,
        IReadOnlyCollection<T> rows)
        where T : class
    {
        if (rows.Count == 0) return null;
        List<SqlParameter> parameters = [];
        var sql = new StringBuilder("");
        var rowIndex = 0;
        foreach (var row in rows)
        {
            // The separator is written before each row (rather than trimming a trailing comma
            // afterwards) so the SQL does not depend on the length of Environment.NewLine.
            if (rowIndex > 0) sql.AppendLine(",");
            sql.Append('(');
            List<SqlParameter> list = [];
            var type = row.GetType();
            var colIndex = 0;
            foreach (var column in columns)
            {
                var paramName = ProcessParameter(type, column, row, rowIndex, colIndex, list);
                sql.Append($"{paramName}, ");
                colIndex++;
            }

            parameters.AddRange(list);
            sql.Remove(sql.Length - 2, 2);
            sql.Append(')');
            rowIndex++;
        }

        sql.AppendLine();
        return new TempTable(sql, parameters);
    }

    private static string ProcessParameter<T>(Type type, ColumnInfo column, T row, int rowIndex, int colIndex,
        List<SqlParameter> list)
    {
        var value = type.GetProperty(column.RefName)?.GetValue(row);
        if (column.ValueConverter is not null)
            value = column.ValueConverter.ConvertToProvider(value);

        var paramName = $"{Prefix}{rowIndex}_{colIndex}".ToString();
        list.Add(new SqlParameter(paramName, value));
        return paramName;
    }
}