using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Products;

namespace TBM.Infrastructure.Data.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(i => i.AltText)
            .HasMaxLength(200);
    }
}