using EfCore.BulkOperations.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.API.Repositories;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Log> Logs => Set<Log>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductMap).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderMap).Assembly);
    }
}