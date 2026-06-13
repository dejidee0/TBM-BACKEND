using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.DesignFlow;

namespace TBM.Infrastructure.Data.Configurations.DesignFlow;

public class ProjectTimelineConfiguration : IEntityTypeConfiguration<ProjectTimeline>
{
    public void Configure(EntityTypeBuilder<ProjectTimeline> builder)
    {
        builder.ToTable("ProjectTimelines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MilestoneName)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
