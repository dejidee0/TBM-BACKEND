using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.AI;

namespace TBM.Infrastructure.Data.Configurations.AI;

public class AIProjectConfiguration : IEntityTypeConfiguration<AIProject>
{
    public void Configure(EntityTypeBuilder<AIProject> builder)
    {
        builder.ToTable("AIProjects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceImageUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.GenerationType)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Prompt)
            .HasMaxLength(2000);

        builder.Property(x => x.NegativePrompt)
            .HasMaxLength(2000);

        builder.Property(x => x.ContextLabel)
            .HasMaxLength(100);

        builder.HasMany(x => x.Designs)
            .WithOne(d => d.AIProject)
            .HasForeignKey(d => d.AIProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
