using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.AI;

namespace TBM.Infrastructure.Data.Configurations.AI;

public class AIRenovationEstimateConfiguration : IEntityTypeConfiguration<AIRenovationEstimate>
{
    public void Configure(EntityTypeBuilder<AIRenovationEstimate> builder)
    {
        builder.ToTable("AIRenovationEstimates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EstimateNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.ProjectName)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.RoomType)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(x => x.FinishLevel)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(x => x.Summary)
            .HasMaxLength(1000);

        builder.Property(x => x.LengthMeters).HasPrecision(18, 3);
        builder.Property(x => x.WidthMeters).HasPrecision(18, 3);
        builder.Property(x => x.HeightMeters).HasPrecision(18, 3);
        builder.Property(x => x.ContingencyPercent).HasPrecision(18, 3);
        builder.Property(x => x.FloorAreaSqm).HasPrecision(18, 3);
        builder.Property(x => x.WallAreaSqm).HasPrecision(18, 3);
        builder.Property(x => x.MaterialsSubtotal).HasPrecision(18, 2);
        builder.Property(x => x.LaborSubtotal).HasPrecision(18, 2);
        builder.Property(x => x.ContingencyAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalEstimate).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => x.EstimateNumber).IsUnique();

        builder.HasMany(x => x.LineItems)
            .WithOne(x => x.Estimate)
            .HasForeignKey(x => x.EstimateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.SuggestedProducts)
            .WithOne(x => x.Estimate)
            .HasForeignKey(x => x.EstimateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
