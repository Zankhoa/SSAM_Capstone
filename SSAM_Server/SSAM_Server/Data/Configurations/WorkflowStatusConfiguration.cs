using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.ToTable("WorkflowStatuses", t =>
        {
            t.HasCheckConstraint("CK_WorkflowStatuses_Category", "[category] IN ('TODO', 'IN_PROGRESS', 'DONE')");
        });

        builder.HasKey(ws => ws.Id);
        builder.Property(ws => ws.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(ws => ws.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(ws => ws.PhaseId)
            .HasColumnName("phase_id")
            .IsRequired();

        builder.Property(ws => ws.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ws => ws.Category)
            .HasColumnName("category")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ws => ws.Color)
            .HasColumnName("color")
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(ws => ws.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(ws => ws.IsInitial)
            .HasColumnName("is_initial")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(ws => ws.Position)
            .HasColumnName("position")
            .IsRequired();

        builder.Property(ws => ws.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(ws => new { ws.PhaseId, ws.Name })
            .IsUnique()
            .HasDatabaseName("UQ_WorkflowStatuses_Phase_Name")
            .HasFilter("[is_active] = 1");

        builder.HasIndex(ws => ws.PhaseId)
            .IsUnique()
            .HasDatabaseName("UQ_WorkflowStatuses_Phase_Initial")
            .HasFilter("[is_initial] = 1 AND [is_active] = 1");

        builder.HasIndex(ws => new { ws.PhaseId, ws.IsActive, ws.Position })
            .HasDatabaseName("IX_WorkflowStatuses_Phase_Order");

        builder.HasOne(ws => ws.Project)
            .WithMany(p => p.WorkflowStatuses)
            .HasForeignKey(ws => ws.ProjectId)
            .HasConstraintName("FK_WorkflowStatuses_Project")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ws => ws.Phase)
            .WithMany(pp => pp.Statuses)
            .HasForeignKey(ws => ws.PhaseId)
            .HasConstraintName("FK_WorkflowStatuses_Phase")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
