using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Orders;

namespace TBM.Infrastructure.Data.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.UserId)
            .IsRequired();
        
        builder.HasIndex(c => c.UserId)
            .IsUnique();
        
        // One user can have one active cart
        builder.HasMany(c => c.Items)
            .WithOne(i => i.Cart)
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Query filter for soft delete
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}