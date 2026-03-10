using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBM.Core.Entities.AI;
using TBM.Core.Enums;

namespace TBM.Infrastructure.Data.Configurations.AI;

public class AIAssistantSessionConfiguration : IEntityTypeConfiguration<AIAssistantSession>
{
    public void Configure(EntityTypeBuilder<AIAssistantSession> builder)
    {
        builder.ToTable("AIAssistantSessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(180);

        builder.HasIndex(x => new { x.UserId, x.LastUpdatedAtUtc });

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Tasks)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ToolActions)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AIAssistantMessageConfiguration : IEntityTypeConfiguration<AIAssistantMessage>
{
    public void Configure(EntityTypeBuilder<AIAssistantMessage> builder)
    {
        builder.ToTable("AIAssistantMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(8000);

        builder.Property(x => x.Intent)
            .HasMaxLength(80);

        builder.Property(x => x.LinksJson)
            .HasColumnType("nvarchar(max)");

        builder.HasMany(x => x.ToolActions)
            .WithOne(x => x.Message)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class AIAssistantTaskConfiguration : IEntityTypeConfiguration<AIAssistantTask>
{
    public void Configure(EntityTypeBuilder<AIAssistantTask> builder)
    {
        builder.ToTable("AIAssistantTasks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(180);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(AIAssistantTaskStatus.Pending);

        builder.Property(x => x.ActionUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.ActionMethod)
            .IsRequired()
            .HasMaxLength(12);

        builder.HasOne(x => x.ToolAction)
            .WithMany()
            .HasForeignKey(x => x.ToolActionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class AIAssistantToolActionConfiguration : IEntityTypeConfiguration<AIAssistantToolAction>
{
    public void Configure(EntityTypeBuilder<AIAssistantToolAction> builder)
    {
        builder.ToTable("AIAssistantToolActions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ActionUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.ActionMethod)
            .IsRequired()
            .HasMaxLength(12);

        builder.Property(x => x.PayloadJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(AIAssistantToolActionStatus.PendingApproval);

        builder.HasMany(x => x.Approvals)
            .WithOne(x => x.ToolAction)
            .HasForeignKey(x => x.ToolActionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Executions)
            .WithOne(x => x.ToolAction)
            .HasForeignKey(x => x.ToolActionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AIAssistantToolApprovalConfiguration : IEntityTypeConfiguration<AIAssistantToolApproval>
{
    public void Configure(EntityTypeBuilder<AIAssistantToolApproval> builder)
    {
        builder.ToTable("AIAssistantToolApprovals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(AIAssistantApprovalStatus.Pending);

        builder.Property(x => x.Reason)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.ToolActionId, x.UserId, x.CreatedAt });
    }
}

public class AIAssistantToolExecutionConfiguration : IEntityTypeConfiguration<AIAssistantToolExecution>
{
    public void Configure(EntityTypeBuilder<AIAssistantToolExecution> builder)
    {
        builder.ToTable("AIAssistantToolExecutions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(AIAssistantToolExecutionStatus.Succeeded);

        builder.Property(x => x.ResultJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);
    }
}
