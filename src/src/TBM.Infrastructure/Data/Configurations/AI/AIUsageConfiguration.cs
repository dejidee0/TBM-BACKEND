using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.AI;

namespace TBM.Infrastructure.Data.Configurations.AI;

public class AIUsageConfiguration : IEntityTypeConfiguration<AIUsage>
{
    public void Configure(EntityTypeBuilder<AIUsage> builder)
    {
        builder.ToTable("AIUsages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GenerationType)
            .IsRequired();

        builder.Property(x => x.CreditsUsed)
            .IsRequired();

        builder.Property(x => x.EstimatedCost)
            .HasPrecision(18, 6);

        builder.Property(x => x.Provider)
            .HasMaxLength(100);
    }
}
