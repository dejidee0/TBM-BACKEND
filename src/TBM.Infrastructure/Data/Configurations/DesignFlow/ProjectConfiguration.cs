using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.DesignFlow;

namespace TBM.Infrastructure.Data.Configurations.DesignFlow;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProjectNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.HasIndex(x => x.ProjectNumber).IsUnique();
        builder.HasIndex(x => x.DesignSessionId).IsUnique();
        builder.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter("[OrderId] IS NOT NULL");
        builder.HasIndex(x => x.BOMId)
            .IsUnique()
            .HasFilter("[BOMId] IS NOT NULL");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.Description)
            .HasMaxLength(5000);

        builder.Property(x => x.RoomType)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.TotalBudget).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.AmountPending).HasPrecision(18, 2);

        builder.HasOne<TBM.Core.Entities.DesignFlow.DesignSession>()
            .WithMany()
            .HasForeignKey(x => x.DesignSessionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<TBM.Core.Entities.Orders.Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<TBM.Core.Entities.DesignFlow.BillOfMaterials>()
            .WithMany()
            .HasForeignKey(x => x.BOMId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Timelines)
            .WithOne()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.GalleryImages)
            .WithOne()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
