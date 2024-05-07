using System.Data;
using System.Data.Common;
using EfCore.BulkOperations.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations;


/// <summary>
///     Provides a set of static methods for performing efficient bulk operations (insert, update, delete, merge)
///     on entities using Entity Framework Core.
/// </summary>
internal static class EfCoreBulkUtils
{
    /// <summary>
    ///     Performs a bulk insert operation into the database.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be inserted.</param>
    /// <param name="optionFactory">Optional factory function for configuring bulk operation (batch size, ignore properties).</param>
    /// <param name="transaction">Optional external transaction to use.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>The total number of rows affected by the bulk operation.</returns>
    internal static async Task<int> BulkInsertAsync<T>(DbContext dbContext,
        IReadOnlyCollection<T> items,
        Action<BulkOption<T>>? optionFactory = null,
        DbTransaction? transaction = null,
        CancellationToken? cancellationToken = null)
        where T : class
    {
        var option = new BulkOption<T>();
        optionFactory?.Invoke(option);

        var batches = BulkCommand.GenerateInsertBatches(dbContext, items, option);
        await BulkExecuteAsync(dbContext, batches, transaction, cancellationToken);
        return batches.Sum(x => x.SuccessCount) ?? 0;
    }

    /// <summary>
    ///     Performs a bulk update operation into the database.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be updated.</param>
    /// <param name="optionFactory">Optional factory function for configuring bulk operation (batch size, ignore properties).</param>
    /// <param name="transaction">Optional external transaction to use.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>The total number of rows affected by the bulk operation.</returns>
    internal static async Task<int> BulkUpdateAsync<T>(DbContext dbContext,
        IReadOnlyCollection<T> items,
        Action<BulkOption<T>>? optionFactory = null,
        DbTransaction? transaction = null,
        CancellationToken? cancellationToken = null)
        where T : class
    {
        var option = new BulkOption<T>();
        optionFactory?.Invoke(option);

        var batches = BulkCommand.GenerateUpdateBatches(dbContext, items, option);
        await BulkExecuteAsync(dbContext, batches, transaction, cancellationToken);
        return batches.Sum(x => x.SuccessCount) ?? 0;
    }

    /// <summary>
    ///     Performs a bulk delete operation into the database.
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be deleted.</param>
    /// <param name="optionFactory">Optional factory function for configuring bulk operation (batch size, ignore properties).</param>
    /// <param name="transaction">Optional external transaction to use.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>The total number of rows affected by the bulk operation.</returns>
    internal static async Task<int> BulkDeleteAsync<T>(DbContext dbContext,
        IReadOnlyCollection<T> items,
        Action<BulkOption<T>>? optionFactory = null,
        DbTransaction? transaction = null,
        CancellationToken? cancellationToken = null)
        where T : class
    {
        var option = new BulkOption<T>();
        optionFactory?.Invoke(option);

        var batches = BulkCommand.GenerateDeleteBatches(dbContext, items, option);
        await BulkExecuteAsync(dbContext, batches, transaction, cancellationToken);
        return batches.Sum(x => x.SuccessCount) ?? 0;
    }

    /// <summary>
    ///     Performs a bulk merge (upsert) operation into the database.
    ///     (use `ON DUPLICATE KEY UPDATE` which is support MySql only)
    /// </summary>
    /// <param name="dbContext">The Entity Framework DbContext instance.</param>
    /// <param name="items">The collection of entities to be merged.</param>
    /// <param name="optionFactory">Optional factory function for configuring bulk operation (batch size, ignore properties).</param>
    /// <param name="transaction">Optional external transaction to use.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>The total number of rows affected by the bulk operation.</returns>
    internal static async Task<int> BulkMergeAsync<T>(DbContext dbContext,
        IReadOnlyCollection<T> items,
        Action<BulkOption<T>>? optionFactory = null,
        DbTransaction? transaction = null,
        CancellationToken? cancellationToken = null)
        where T : class
    {
        var option = new BulkOption<T>();
        optionFactory?.Invoke(option);

        var batches = BulkCommand.GenerateMergeBatches(dbContext, items, option);
        await BulkExecuteAsync(dbContext, batches, transaction, cancellationToken);
        return batches.Sum(x => x.SuccessCount) ?? 0;
    }


    #region Execute Batch

    /// <summary>
    ///     Executes all batches within a transaction (optionally uses an external transaction).
    /// </summary>
    private static async Task BulkExecuteAsync(
        DbContext dbContext,
        IEnumerable<BatchData> batches,
        DbTransaction? externalTransaction = null,
        CancellationToken? cancellationToken = null)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken ?? default);
        var transaction = externalTransaction ?? await connection.BeginTransactionAsync(cancellationToken ?? default);
        try
        {
            foreach (var batch in batches)
                await ExecuteBatchDataAsync(batch, connection, transaction, cancellationToken);

            if (externalTransaction is null) await transaction.CommitAsync(cancellationToken ?? default);
        }
        catch (Exception)
        {
            if (externalTransaction is null) await transaction.RollbackAsync(cancellationToken ?? default);
            throw;
        }
        finally
        {
            if (connection.State == ConnectionState.Open) await connection.CloseAsync();
            if (externalTransaction is null) await transaction.DisposeAsync();
        }
    }

    /// <summary>
    ///     Executes a single SQL batch asynchronously.
    /// </summary>
    /// <exception cref="ArgumentNullException">Throw when command.Connection is null.</exception>
    private static async Task ExecuteBatchDataAsync(
        BatchData batch,
        DbConnection connection,
        DbTransaction? dbTransaction,
        CancellationToken? cancellationToken = null)
    {
        await using var command = connection.CreateCommand();
        if (command.Connection is null) throw new ArgumentNullException(nameof(connection));
        if (dbTransaction is not null) command.Transaction = dbTransaction;
        command.CommandText = batch.Sql.ToString();

        batch.Parameters.ToList().ForEach(p =>
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = p.Name;
            parameter.Value = p.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        });

        if (command.Connection.State != ConnectionState.Open) // Ensure connection is open
            await command.Connection.OpenAsync(cancellationToken ?? default);

        batch.SuccessCount = await command.ExecuteNonQueryAsync(cancellationToken ?? default);
    }

    #endregion
}