using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Contact;

namespace TBM.Infrastructure.Data.Configurations;

public class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable("ContactMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(x => x.Subject)
            .HasMaxLength(300);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(x => x.EmailError)
            .HasMaxLength(2000);

        builder.Property(x => x.EmailSent)
            .HasDefaultValue(false);

        builder.HasIndex(x => x.CreatedAt);
    }
}
