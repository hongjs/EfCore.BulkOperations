using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.API.Repositories;
using EfCore.BulkOperations.Test.Setup;
using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Test;

/// <summary>
///     Order is the entity with a foreign key, a DateOnly column and a descending index - the
///     shape the benchmarks use - and until now no test sent one through the library. These run
///     every OrderRepository method against MySQL and compare the bulk path with EF Core's.
/// </summary>
public class OrderRepositoryTest : BaseIntegrationTest
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;

    public OrderRepositoryTest(IntegrationTestFactory factory) : base(factory)
    {
        _orders = GetRequiredService<IOrderRepository>();
        _products = GetRequiredService<IProductRepository>();
    }

    private async Task<Product> InsertProduct()
    {
        var product = new Product("Widget", 10m);
        await _products.BulkInsertProducts([product]);
        return product;
    }

    private static List<Order> NewOrders(Product product, int count)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Enumerable.Range(1, count)
            .Select(i => new Order(product.Id, today.AddDays(-i), i, i * 10m))
            .ToList();
    }

    [Fact]
    public async Task Should_BulkInsertOrders_WithForeignKeyAndDateOnly()
    {
        // Arrange
        var product = await InsertProduct();
        var orders = NewOrders(product, 3);

        // Act
        var inserted = await _orders.BulkInsertOrders(orders);

        // Assert
        Assert.Equal(3, inserted);
        var stored = await _orders.GetOrders();
        Assert.Equal(3, stored.Count);
        Assert.All(stored, o => Assert.Equal(product.Id, o.ProductId));
        Assert.Equal(orders.Select(o => o.OrderDate).Order(), stored.Select(o => o.OrderDate).Order());
    }

    [Fact]
    public async Task Should_BulkUpdateOrders()
    {
        // Arrange
        var product = await InsertProduct();
        var orders = NewOrders(product, 2);
        await _orders.BulkInsertOrders(orders);
        foreach (var order in orders) order.UpdateOrder(99m, 999m);

        // Act
        var updated = await _orders.BulkUpdateOrders(orders);

        // Assert
        Assert.Equal(2, updated);
        var stored = await _orders.GetOrders();
        Assert.All(stored, o =>
        {
            Assert.Equal(99m, o.Unit);
            Assert.Equal(999m, o.Amount);
        });
    }

    [Fact]
    public async Task Should_InsertAndUpdateOrders_ThroughEfCore()
    {
        // The EF Core path the benchmarks compare against.
        var product = await InsertProduct();
        var orders = NewOrders(product, 2);

        // Act
        var inserted = await _orders.InsertOrders(orders);
        foreach (var order in orders) order.UpdateOrder(5m, 50m);
        var updated = await _orders.UpdateOrders(orders);

        // Assert
        Assert.Equal(2, inserted);
        Assert.Equal(2, updated);
        var stored = await _orders.GetOrders();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, o => Assert.Equal(50m, o.Amount));
    }

    [Fact]
    public async Task Should_GetOrders_TrackedOrNot()
    {
        // Arrange
        var product = await InsertProduct();
        await _orders.BulkInsertOrders(NewOrders(product, 1));

        // Act
        var untracked = await _orders.GetOrders();
        var tracked = await _orders.GetOrders(true);

        // Assert
        Assert.Single(untracked);
        Assert.Single(tracked);
        Assert.Equal(EntityState.Detached, DbContext.Entry(untracked[0]).State);
        Assert.Equal(EntityState.Unchanged, DbContext.Entry(tracked[0]).State);
        Assert.Null(untracked[0].Product); // the repository does not Include it
    }

    [Fact]
    public async Task Should_DeleteAllOrders()
    {
        // Arrange
        var product = await InsertProduct();
        await _orders.BulkInsertOrders(NewOrders(product, 4));

        // Act
        var deleted = await _orders.DeleteAllOrders();

        // Assert
        Assert.Equal(4, deleted);
        Assert.Empty(await _orders.GetOrders());
    }

    [Fact]
    public async Task ShouldError_WhenOrderReferencesMissingProduct()
    {
        // The FK constraint is real: the library does not bypass it.
        var orphan = new Order(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), 1, 1);

        await Assert.ThrowsAnyAsync<Exception>(() => _orders.BulkInsertOrders([orphan]));
        Assert.Empty(await _orders.GetOrders());
    }
}
