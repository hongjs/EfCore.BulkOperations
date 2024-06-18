using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.BulkOperations.API.Models;

public class OrderMap : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(i => i.Id);

        builder.HasIndex(x => x.Id)
            .IsUnique();

        builder
            .HasIndex(x => x.ProductId);
        builder
            .HasIndex(x => x.OrderDate)
            .IsDescending();

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Unit)
            .HasPrecision(19, 6)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(19, 6)
            .IsRequired();

        builder.HasOne<Product>(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .HasPrincipalKey(x => x.Id)
            .IsRequired();
    }
}