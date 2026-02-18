using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.Users;

namespace TBM.Infrastructure.Data.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        
        // Composite primary key
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        
        builder.Property(ur => ur.AssignedAt)
            .IsRequired();
    }
}