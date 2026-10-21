using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions", t =>
        {
            t.HasCheckConstraint("CK_WorkflowTransitions_NotSelf", "[from_status_id] <> [to_status_id]");
        });

        builder.HasKey(wt => wt.Id);
        builder.Property(wt => wt.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(wt => wt.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(wt => wt.PhaseId)
            .HasColumnName("phase_id")
            .IsRequired();

        builder.Property(wt => wt.FromStatusId)
            .HasColumnName("from_status_id")
            .IsRequired();

        builder.Property(wt => wt.ToStatusId)
            .HasColumnName("to_status_id")
            .IsRequired();

        builder.Property(wt => wt.RequireComment)
            .HasColumnName("require_comment")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(wt => wt.CommentHint)
            .HasColumnName("comment_hint")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(wt => wt.Condition)
            .HasColumnName("condition")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.HasIndex(wt => new { wt.PhaseId, wt.FromStatusId, wt.ToStatusId })
            .IsUnique()
            .HasDatabaseName("UQ_WorkflowTransitions_Pair");

        builder.HasIndex(wt => wt.FromStatusId)
            .HasDatabaseName("IX_WorkflowTransitions_From");

        builder.HasIndex(wt => wt.ToStatusId)
            .HasDatabaseName("IX_WorkflowTransitions_To");

        builder.HasOne(wt => wt.Project)
            .WithMany(p => p.WorkflowTransitions)
            .HasForeignKey(wt => wt.ProjectId)
            .HasConstraintName("FK_WorkflowTransitions_Project")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(wt => wt.Phase)
            .WithMany(pp => pp.Transitions)
            .HasForeignKey(wt => wt.PhaseId)
            .HasConstraintName("FK_WorkflowTransitions_Phase")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(wt => wt.FromStatus)
            .WithMany()
            .HasForeignKey(wt => wt.FromStatusId)
            .HasConstraintName("FK_WorkflowTransitions_From")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(wt => wt.ToStatus)
            .WithMany()
            .HasForeignKey(wt => wt.ToStatusId)
            .HasConstraintName("FK_WorkflowTransitions_To")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
