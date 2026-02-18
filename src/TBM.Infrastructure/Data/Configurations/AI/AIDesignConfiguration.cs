using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.AI;

namespace TBM.Infrastructure.Data.Configurations.AI;

public class AIDesignConfiguration : IEntityTypeConfiguration<AIDesign>
{
    public void Configure(EntityTypeBuilder<AIDesign> builder)
    {
        builder.ToTable("AIDesigns");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OutputUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.OutputType)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasMaxLength(100);

        builder.Property(x => x.ProviderJobId)
            .HasMaxLength(200);

        builder.Property(x => x.DurationSeconds);

        builder.Property(x => x.Width);
        builder.Property(x => x.Height);
    }
}
