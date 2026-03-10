using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.AI;

namespace TBM.Infrastructure.Data.Configurations.AI;

public class AIRenovationEstimateLineItemConfiguration : IEntityTypeConfiguration<AIRenovationEstimateLineItem>
{
    public void Configure(EntityTypeBuilder<AIRenovationEstimateLineItem> builder)
    {
        builder.ToTable("AIRenovationEstimateLineItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Group)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Unit)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.TotalCost).HasPrecision(18, 2);
    }
}
