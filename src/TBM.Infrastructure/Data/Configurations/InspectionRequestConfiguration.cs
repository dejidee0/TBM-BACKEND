using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Inspections;

namespace TBM.Infrastructure.Data.Configurations;

public class InspectionRequestConfiguration : IEntityTypeConfiguration<InspectionRequest>
{
    public void Configure(EntityTypeBuilder<InspectionRequest> builder)
    {
        builder.Property(i => i.InspectionFee)
            .HasColumnType("decimal(18,2)");

        builder.HasIndex(i => i.PaymentReference);
    }
}
