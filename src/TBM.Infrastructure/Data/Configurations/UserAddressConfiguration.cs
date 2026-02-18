using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Users;

namespace TBM.Infrastructure.Data.Configurations;

public class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.ToTable("UserAddresses");
        
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.FullName)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(a => a.Street)
            .IsRequired()
            .HasMaxLength(255);
        
        builder.Property(a => a.City)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(a => a.State)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(a => a.PostalCode)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(a => a.Country)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(a => a.Phone)
            .HasMaxLength(20);
        
        builder.Property(a => a.DeliveryNotes)
            .HasMaxLength(500);
    }
}