using System.Data;
using System.Data.Common;
using System.Reflection;
using DotNet.Testcontainers.Builders;
using EfCore.BulkOperations.API;
using EfCore.BulkOperations.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.MySql;

namespace EfCore.BulkOperations.Test.Setup;

internal record DbConfig(
    string DbName,
    string Username,
    string Password,
    int Port = 3306,
    string CharSet = "utf8mb4",
    string Collate = "utf8mb4_0900_ai_ci");

public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly DbConfig Config = new("incentive_db", "user", "password");

    private readonly MySqlContainer _container = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase(Config.DbName)
        .WithUsername(Config.Username)
        .WithPassword(Config.Password)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(Config.Port))
        .WithCleanUp(true)
        .Build();

    private DbConnection? _connection;

    private Respawner? _respawner;
    public IServiceProvider? ServiceProvider { get; private set; }
    public ApplicationDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await _container.ExecAsync([
            "mysql",
            "-p",
            "mysql",
            "-e",
            $"ALTER DATABASE {Config.DbName} CHARACTER SET ${Config.CharSet} COLLATE ${Config.Collate};"
        ]);

        ServiceProvider = Services.CreateScope().ServiceProvider;
        DbContext = ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _connection = DbContext.Database.GetDbConnection();
        await _connection.OpenAsync();

        // Reset the database's data before each test
        _respawner = await Respawner.CreateAsync(_connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.MySql,
                SchemasToInclude = [Config.DbName],
                // Tables to ignore deletion
                TablesToIgnore = []
            }
        );
    }

    public new async Task DisposeAsync()
    {
        if (_connection is not null) await _connection.CloseAsync();
        await DbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connString = _container.GetConnectionString();
        var migrationAssemblyName = Assembly.GetAssembly(typeof(PlaceHolderForAssemblyReference))!.GetName().Name;

        builder.ConfigureTestServices(services =>
        {
            services.RemoveDbContext<ApplicationDbContext>();
            services.AddDbContextPool<ApplicationDbContext>(options =>
            {
                options.UseMySql(connString, ServerVersion.AutoDetect(connString), o =>
                    {
                        o.MigrationsAssembly(migrationAssemblyName);
                        o.MigrationsHistoryTable($"__{nameof(ApplicationDbContext)}");
                    })
                    .EnableSensitiveDataLogging(false);
            });
            services.EnsureDbCreated<ApplicationDbContext>();
        });
    }

    public async Task ResetDatabase()
    {
        if (_connection is null) return;
        try
        {
            if (_connection.State != ConnectionState.Open) await _connection.OpenAsync();
            if (_respawner is not null) await _respawner.ResetAsync(_connection);
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }
}