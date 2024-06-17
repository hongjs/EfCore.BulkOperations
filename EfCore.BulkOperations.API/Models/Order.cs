namespace EfCore.BulkOperations.API.Models;

public class Order(Guid productId, DateOnly orderDate, decimal unit, decimal amount)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProductId { get; private set; } = productId;
    public DateOnly OrderDate { get; private set; } = orderDate;
    public decimal Unit { get; private set; } = unit;
    public decimal Amount { get; private set; } = amount;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public virtual Product? Product { get; init; }

    public void UpdateOrder(decimal unit, decimal amount)
    {
        Unit = unit;
        Amount = amount;
    }
}