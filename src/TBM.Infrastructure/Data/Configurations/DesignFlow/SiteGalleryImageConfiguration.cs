using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.DesignFlow;

namespace TBM.Infrastructure.Data.Configurations.DesignFlow;

public class SiteGalleryImageConfiguration : IEntityTypeConfiguration<SiteGalleryImage>
{
    public void Configure(EntityTypeBuilder<SiteGalleryImage> builder)
    {
        builder.ToTable("SiteGalleryImages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Thumbnail)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Caption)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
