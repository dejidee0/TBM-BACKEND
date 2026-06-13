using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.DesignFlow;

namespace TBM.Infrastructure.Data.Configurations.DesignFlow;

public class DesignSessionConfiguration : IEntityTypeConfiguration<DesignSession>
{
    public void Configure(EntityTypeBuilder<DesignSession> builder)
    {
        builder.ToTable("DesignSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.HasIndex(x => x.SessionNumber).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.BOMId);
        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.ProjectId);

        builder.Property(x => x.ProjectName)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.RoomType)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(x => x.VisionText)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(x => x.Tier)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.OriginalImageUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.GeneratedImageUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.RoomLength).HasPrecision(18, 3);
        builder.Property(x => x.RoomWidth).HasPrecision(18, 3);
        builder.Property(x => x.RoomHeight).HasPrecision(18, 3);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.Progress)
            .IsRequired();

        builder.Property(x => x.CurrentStep)
            .HasMaxLength(200);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
