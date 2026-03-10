using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.AI;

namespace TBM.Infrastructure.Data.Configurations.AI;

public class AIRenovationEstimateSuggestedProductConfiguration : IEntityTypeConfiguration<AIRenovationEstimateSuggestedProduct>
{
    public void Configure(EntityTypeBuilder<AIRenovationEstimateSuggestedProduct> builder)
    {
        builder.ToTable("AIRenovationEstimateSuggestedProducts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Category)
            .HasMaxLength(120);

        builder.Property(x => x.Link)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Price).HasPrecision(18, 2);
    }
}
