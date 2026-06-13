using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.DesignFlow;

namespace TBM.Infrastructure.Data.Configurations.DesignFlow;

public class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.ToTable("ProjectDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(x => x.FileUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.MimeType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.FileSize)
            .IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.UploadedAt });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
