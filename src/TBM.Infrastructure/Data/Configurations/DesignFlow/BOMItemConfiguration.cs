using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.DesignFlow;

namespace TBM.Infrastructure.Data.Configurations.DesignFlow;

public class BOMItemConfiguration : IEntityTypeConfiguration<BOMItem>
{
    public void Configure(EntityTypeBuilder<BOMItem> builder)
    {
        builder.ToTable("BOMItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SKU)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3);

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.TotalPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.Reason)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.BOMId);
        builder.HasIndex(x => x.ProductId);

        builder.HasOne<TBM.Core.Entities.DesignFlow.BillOfMaterials>()
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.BOMId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TBM.Core.Entities.Products.Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
