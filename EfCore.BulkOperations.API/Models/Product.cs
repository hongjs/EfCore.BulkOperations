namespace EfCore.BulkOperations.API.Models;

public class Product(string name, decimal price)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; private set; } = name;

    public decimal Price { get; private set; } = price;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public void UpdateName(string name)
    {
        Name = name;
    }
}