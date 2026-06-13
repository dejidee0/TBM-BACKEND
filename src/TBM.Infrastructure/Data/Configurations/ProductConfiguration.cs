using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Products;

namespace TBM.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(300);
        
        builder.Property(p => p.Description)
            .HasMaxLength(5000);
        
        builder.Property(p => p.ShortDescription)
            .HasMaxLength(500);
        
        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(350);
        
        builder.HasIndex(p => p.Slug)
            .IsUnique();
        
        builder.Property(p => p.SKU)
            .HasMaxLength(100);
        
        builder.HasIndex(p => p.SKU);
        
        builder.Property(p => p.BrandType)
            .IsRequired()
            .HasConversion<int>();
        
        builder.Property(p => p.ProductType)
            .IsRequired()
            .HasConversion<int>();
        
        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)");
        
        builder.Property(p => p.CompareAtPrice)
            .HasColumnType("decimal(18,2)");
        
        builder.Property(p => p.MetaTitle)
            .HasMaxLength(200);
        
        builder.Property(p => p.MetaDescription)
            .HasMaxLength(500);
        
        builder.Property(p => p.Tags)
            .HasMaxLength(500);

        builder.Property(p => p.AIKeywords)
            .HasMaxLength(500);

        builder.Property(p => p.MaterialType)
            .HasMaxLength(120);

        builder.Property(p => p.QualityTier)
            .HasMaxLength(80);

        builder.Property(p => p.RecommendedFor)
            .HasMaxLength(500);

        // JSON content columns (NVARCHAR(MAX))
        builder.Property(p => p.Specifications).HasColumnType("nvarchar(max)");
        builder.Property(p => p.KeyFeatures).HasColumnType("nvarchar(max)");
        builder.Property(p => p.WhatsIncluded).HasColumnType("nvarchar(max)");
        builder.Property(p => p.WhatsNotIncluded).HasColumnType("nvarchar(max)");

        // Structured attribute columns
        builder.Property(p => p.Dimensions).HasMaxLength(500);
        builder.Property(p => p.Warranty).HasMaxLength(255);
        builder.Property(p => p.FinishType).HasMaxLength(255);
        builder.Property(p => p.InstallationType).HasMaxLength(255);
        builder.Property(p => p.Material).HasMaxLength(255);
        builder.Property(p => p.Color).HasMaxLength(255);

        // Relationship with Category
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Relationship with Images
        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Query filter for soft delete
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
