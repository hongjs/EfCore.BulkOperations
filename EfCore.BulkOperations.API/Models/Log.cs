using System.ComponentModel.DataAnnotations;

namespace EfCore.BulkOperations.API.Models;

/// <summary>
///     A dummy entity with a primary key but no unique index
/// </summary>
public class Log
{
    public Log(string content)
    {
        Id = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
        Content = content;
    }

    [Key] public Guid Id { get; init; }
    [StringLength(1000)] public string Content { get; init; }
    public DateTime Timestamp { get; init; }
}