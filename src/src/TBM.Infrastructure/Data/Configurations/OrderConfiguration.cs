using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Orders;

namespace TBM.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        
        builder.HasKey(o => o.Id);
        
        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();
        
        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<int>();
        
        builder.Property(o => o.PaymentStatus)
            .IsRequired()
            .HasConversion<int>();
        
        builder.Property(o => o.PaymentMethod)
            .HasConversion<int>();
        
        builder.Property(o => o.SubTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
        
        builder.Property(o => o.ShippingCost)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
        
        builder.Property(o => o.Tax)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
        
        builder.Property(o => o.Discount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
        
        builder.Property(o => o.Total)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
        
        builder.Property(o => o.ShippingFullName)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(o => o.ShippingPhone)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(o => o.ShippingAddress)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(o => o.ShippingCity)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(o => o.ShippingState)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(o => o.ShippingNotes)
            .HasMaxLength(1000);
        
        builder.Property(o => o.PaymentReference)
            .HasMaxLength(200);
        
        builder.Property(o => o.TrackingNumber)
            .HasMaxLength(100);
        
        builder.Property(o => o.CancellationReason)
            .HasMaxLength(500);
        
        builder.Property(o => o.CustomerNotes)
            .HasMaxLength(1000);
        
        builder.Property(o => o.AdminNotes)
            .HasMaxLength(1000);
        
        // Relationship with User
        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Relationship with Items
        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Query filter for soft delete
        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}