using EfCore.BulkOperations.API.Repositories;
using EfCore.BulkOperations.API.Startup;
using EfCore.BulkOperations.Test.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.BulkOperations.Test;

[Trait("Category", "Unit")]
public class ServiceRegistrationTest
{
    [Fact]
    public void ShouldError_WhenConnectionStringIsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

        // Assert
        Assert.Contains("Connection string 'App' was not found", exception.Message);
    }
}

/// <summary>
///     The registration reads the connection string from the host's configuration; a freshly
///     constructed ConfigurationManager has no providers and always returned null.
/// </summary>
public class ServiceRegistrationIntegrationTest(IntegrationTestFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Should_ReadConnectionStringFromConfiguration()
    {
        // Arrange
        var connectionString = DbContext.Database.GetConnectionString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:App"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddServices();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Assert
        Assert.True(await dbContext.Database.CanConnectAsync());
    }
}
