using EfCore.BulkOperations.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Test.Setup;

/// <summary>
///     Builds an ApplicationDbContext for tests that only read EF Core's model metadata.
///     The server version is pinned rather than auto-detected, so no connection is opened and
///     these tests can run on any platform without Docker.
/// </summary>
internal static class ModelOnlyDbContext
{
    private const string ConnectionString = "server=localhost;database=model_only;user=root;password=root";

    internal static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new ApplicationDbContext(options);
    }
}
