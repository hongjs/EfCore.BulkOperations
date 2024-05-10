using EfCore.BulkOperations.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.BulkOperations.API.Repositories;

public class ProductMap : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(i => i.Id);

        builder.HasIndex(x => x.Id)
                .IsUnique();

        builder.Property(x => x.Id)
                .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

        builder.Property(x => x.Price)
                .HasPrecision(19, 6)
                .IsRequired();
    }
}