using Microsoft.EntityFrameworkCore;

namespace EfCore.BulkOperations.Test.Setup;

/// <summary>Has a primary key but no unique index, which is how most entities are configured.</summary>
internal class PkOnlyEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;

    /// <summary>A CLR property the model ignores, so it is not a column.</summary>
    public string Unmapped { get; init; } = string.Empty;
}

/// <summary>Nothing but its key: a join table, or a set of identifiers.</summary>
internal class KeyOnlyEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
}

/// <summary>A keyless entity (a view): no primary key, no unique index.</summary>
internal class KeylessEntity
{
    public string Name { get; init; } = string.Empty;
}

/// <summary>Has a column EF Core knows about that the CLR type does not.</summary>
internal class ShadowEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
}

/// <summary>Column names that differ from the property names.</summary>
internal class RenamedEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = string.Empty;
}

/// <summary>
///     A context whose entities exercise the edges of key resolution and property reading. The
///     server version is pinned so no connection is opened; these are model-only tests.
/// </summary>
internal class EdgeCaseDbContext(DbContextOptions<EdgeCaseDbContext> options) : DbContext(options)
{
    private const string ConnectionString = "server=localhost;database=model_only;user=root;password=root";

    internal static EdgeCaseDbContext Create()
    {
        var options = new DbContextOptionsBuilder<EdgeCaseDbContext>()
            .UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new EdgeCaseDbContext(options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PkOnlyEntity>(e =>
        {
            e.ToTable("PkOnly");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Unmapped);
        });

        modelBuilder.Entity<KeyOnlyEntity>(e =>
        {
            e.ToTable("KeyOnly");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<KeylessEntity>(e =>
        {
            e.ToTable("Keyless");
            e.HasNoKey();
        });

        modelBuilder.Entity<ShadowEntity>(e =>
        {
            e.ToTable("Shadow");
            e.HasKey(x => x.Id);
            e.Property<string>("Tenant");
        });

        modelBuilder.Entity<RenamedEntity>(e =>
        {
            e.ToTable("renamed");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("renamed_id");
            e.Property(x => x.Code).HasColumnName("renamed_code");
        });
    }
}
