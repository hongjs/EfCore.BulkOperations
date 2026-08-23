using EfCore.BulkOperations.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.API.Startup;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddDbContextPool<ApplicationDbContext>((servicesProvider, dbOptions) =>
        {
            // A newly constructed ConfigurationManager has no providers, so it never sees
            // appsettings.json; resolve the configuration that was built for the host instead.
            var connectionString = servicesProvider.GetRequiredService<IConfiguration>()
                                       .GetConnectionString("App")
                                   ?? throw new InvalidOperationException(
                                       "Connection string 'App' was not found in configuration.");
            dbOptions
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    o => { o.MigrationsHistoryTable($"__{nameof(ApplicationDbContext)}"); });
        });

        return services;
    }
}