using EfCore.BulkOperations.API.Repositories;

namespace EfCore.BulkOperations.API.Startup;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        return services;
    }
}