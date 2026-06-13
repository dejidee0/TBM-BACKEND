using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Products;

namespace TBM.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(c => c.Description)
            .HasMaxLength(1000);
        
        builder.Property(c => c.Slug)
            .IsRequired()
            .HasMaxLength(250);
        
        builder.HasIndex(c => c.Slug)
            .IsUnique();
        
        builder.Property(c => c.BrandType)
            .IsRequired()
            .HasConversion<int>();
        
        builder.Property(c => c.ImageUrl)
            .HasMaxLength(500);
        
        // Self-referencing relationship for parent-child categories
        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Query filter for soft delete
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}