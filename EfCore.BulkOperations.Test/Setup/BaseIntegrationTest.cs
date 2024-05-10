using EfCore.BulkOperations.API.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.BulkOperations.Test.Setup;

[Collection(nameof(DatabaseTestCollection))]
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    private static bool _initialized;
    private readonly IntegrationTestFactory _factory;
    private readonly Func<Task> _resetDatabase;
    protected readonly ApplicationDbContext DbContext;

    protected BaseIntegrationTest(IntegrationTestFactory factory)
    {
        _factory = factory;
        _resetDatabase = factory.ResetDatabase;
        DbContext = factory.DbContext;
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        // Seed data before initial testing
        _initialized = true;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return _resetDatabase();
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return (_factory.ServiceProvider ?? throw new InvalidOperationException("ServiceProvider is null"))
            .GetRequiredService<T>();
    }
}