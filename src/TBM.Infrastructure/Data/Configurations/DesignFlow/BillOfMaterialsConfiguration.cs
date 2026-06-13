using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.DesignFlow;

namespace TBM.Infrastructure.Data.Configurations.DesignFlow;

public class BillOfMaterialsConfiguration : IEntityTypeConfiguration<BillOfMaterials>
{
    public void Configure(EntityTypeBuilder<BillOfMaterials> builder)
    {
        builder.ToTable("BillsOfMaterials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BomNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.HasIndex(x => x.BomNumber).IsUnique();
        builder.HasIndex(x => x.DesignSessionId).IsUnique();

        builder.Property(x => x.TotalEstimatedCost)
            .HasPrecision(18, 2);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne<DesignSession>()
            .WithMany()
            .HasForeignKey(x => x.DesignSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.BOMId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
