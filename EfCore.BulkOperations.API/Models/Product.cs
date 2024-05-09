namespace EfCore.BulkOperations.API.Models;

public class Product(string name, decimal price)
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public string Name { get; init; } = name;

  public decimal Price { get; init; } = price;

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}