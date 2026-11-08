using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class ProjectPhaseConfiguration : IEntityTypeConfiguration<ProjectPhase>
{
    public void Configure(EntityTypeBuilder<ProjectPhase> builder)
    {
        builder.ToTable("ProjectPhases");

        builder.HasKey(pp => pp.Id);
        builder.Property(pp => pp.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(pp => pp.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(pp => pp.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(pp => pp.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(pp => pp.Position)
            .HasColumnName("position")
            .IsRequired();

        builder.Property(pp => pp.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(pp => pp.ExitCriteria)
            .HasColumnName("exit_criteria")
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(pp => pp.LifecycleStatus)
            .HasColumnName("lifecycle_status")
            .HasMaxLength(20)
            .HasDefaultValue("UPCOMING")
            .IsRequired();

        builder.Property(pp => pp.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("datetime2(3)");

        builder.Property(pp => pp.CompletedById)
            .HasColumnName("completed_by_id");

        builder.Property(pp => pp.OverrideReason)
            .HasColumnName("override_reason")
            .HasMaxLength(500);

        builder.Property(pp => pp.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(pp => pp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ProjectPhases_Lifecycle", "[lifecycle_status] IN (N'UPCOMING', N'ACTIVE', N'COMPLETED')");
            t.HasCheckConstraint("CK_ProjectPhases_ExitCriteria_Json", "ISJSON([exit_criteria]) = 1");
        });

        builder.HasIndex(pp => new { pp.ProjectId, pp.Name })
            .IsUnique()
            .HasDatabaseName("UQ_ProjectPhases_Project_Name")
            .HasFilter("[is_active] = 1");

        builder.HasIndex(pp => new { pp.ProjectId, pp.IsActive, pp.Position })
            .HasDatabaseName("IX_ProjectPhases_Project_Order");

        builder.HasOne(pp => pp.Project)
            .WithMany(p => p.Phases)
            .HasForeignKey(pp => pp.ProjectId)
            .HasConstraintName("FK_ProjectPhases_Project")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pp => pp.CompletedBy)
            .WithMany()
            .HasForeignKey(pp => pp.CompletedById)
            .HasConstraintName("FK_ProjectPhases_CompletedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class ProjectPhaseLogConfiguration : IEntityTypeConfiguration<ProjectPhaseLog>
{
    public void Configure(EntityTypeBuilder<ProjectPhaseLog> builder)
    {
        builder.ToTable("ProjectPhaseLogs", t =>
        {
            t.HasCheckConstraint("CK_ProjectPhaseLogs_Action", "[action] IN (N'COMPLETE', N'OVERRIDE_COMPLETE', N'REOPEN')");
        });

        builder.HasKey(x => x.Id).HasName("PK_ProjectPhaseLogs");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(x => x.PhaseId)
            .HasColumnName("phase_id")
            .IsRequired();

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(x => x.At)
            .HasColumnName("at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UnmetCriteria)
            .HasColumnName("unmet_criteria")
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.PhaseId, x.At })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ProjectPhaseLogs_Phase");

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ProjectPhaseLogs_Project");

        builder.HasOne(x => x.Phase)
            .WithMany(p => p.Logs)
            .HasForeignKey(x => x.PhaseId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ProjectPhaseLogs_Phase");

        builder.HasOne(x => x.Actor)
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ProjectPhaseLogs_Actor");
    }
}
