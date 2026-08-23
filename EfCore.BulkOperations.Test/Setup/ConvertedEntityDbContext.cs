using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Test.Setup;

internal enum TicketStatus
{
    Open,
    Closed
}

/// <summary>
///     An entity whose properties reach the database as something other than their CLR type:
///     an enum stored as text, and a bool stored as 'Y' or 'N'.
/// </summary>
internal class Ticket
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public TicketStatus Status { get; init; }
    public bool IsUrgent { get; init; }
}

/// <summary>
///     A context that exists only to give the SQL generator an entity with value converters on it.
///     The model in the API project has none, so without this the conversion branch in
///     BulkCommand.ProcessParameter is never executed by any test.
///     Like ModelOnlyDbContext, the server version is pinned so no connection is opened.
/// </summary>
internal class ConvertedEntityDbContext(DbContextOptions<ConvertedEntityDbContext> options)
    : DbContext(options)
{
    private const string ConnectionString = "server=localhost;database=model_only;user=root;password=root";

    public DbSet<Ticket> Tickets => Set<Ticket>();

    internal static ConvertedEntityDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ConvertedEntityDbContext>()
            .UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new ConvertedEntityDbContext(options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ticket = modelBuilder.Entity<Ticket>();
        ticket.ToTable("Ticket");
        ticket.HasKey(x => x.Id);
        ticket.HasIndex(x => x.Id).IsUnique();
        ticket.Property(x => x.Status).HasConversion<string>();
        ticket.Property(x => x.IsUrgent).HasConversion(
            urgent => urgent ? "Y" : "N",
            stored => stored == "Y");
    }
}
