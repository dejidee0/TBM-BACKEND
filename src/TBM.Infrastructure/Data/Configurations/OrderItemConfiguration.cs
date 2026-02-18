using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Orders;

namespace TBM.Infrastructure.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.ProductName)
            .IsRequired()
            .HasMaxLength(300);
        
        builder.Property(i => i.ProductSKU)
            .HasMaxLength(100);
        
        builder.Property(i => i.ProductImageUrl)
            .HasMaxLength(500);
        
        builder.Property(i => i.Quantity)
            .IsRequired();
        
        builder.Property(i => i.UnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
        
        builder.Property(i => i.SubTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
        
        // Relationship with Product (optional - product might be deleted)
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}