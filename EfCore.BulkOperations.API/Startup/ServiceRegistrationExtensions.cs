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
            var connectionString = "server=localhost; database=test_db; user=incentive_service_user; password=password";
            dbOptions
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    o => { o.MigrationsHistoryTable($"__{nameof(ApplicationDbContext)}"); });
        });

        return services;
    }
}