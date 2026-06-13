using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Portfolio;

namespace TBM.Infrastructure.Data.Configurations.Portfolio;

public class VendorPortfolioProjectConfiguration : IEntityTypeConfiguration<VendorPortfolioProject>
{
    public void Configure(EntityTypeBuilder<VendorPortfolioProject> builder)
    {
        builder.ToTable("VendorPortfolioProjects");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VendorId).IsRequired();
        builder.Property(x => x.VendorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.Property(x => x.Category).HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ScopeOfWork).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.BudgetMin).HasColumnType("decimal(18,2)");
        builder.Property(x => x.BudgetMax).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasMany(x => x.Images)
            .WithOne(x => x.Project)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VendorId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.PublishedAt);
    }
}

public class PortfolioProjectImageConfiguration : IEntityTypeConfiguration<PortfolioProjectImage>
{
    public void Configure(EntityTypeBuilder<PortfolioProjectImage> builder)
    {
        builder.ToTable("PortfolioProjectImages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageUrl).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Caption).HasMaxLength(500);
        builder.Property(x => x.ImageType).HasConversion<int>();

        builder.HasIndex(x => x.ProjectId);
    }
}
