using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class WorkflowTransitionRoleConfiguration : IEntityTypeConfiguration<WorkflowTransitionRole>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionRole> builder)
    {
        builder.ToTable("WorkflowTransitionRoles");

        builder.HasKey(wtr => new { wtr.TransitionId, wtr.RoleId });

        builder.Property(wtr => wtr.TransitionId)
            .HasColumnName("transition_id")
            .IsRequired();

        builder.Property(wtr => wtr.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.HasIndex(wtr => wtr.RoleId)
            .HasDatabaseName("IX_WorkflowTransitionRoles_RoleId");

        builder.HasOne(wtr => wtr.Transition)
            .WithMany(wt => wt.AllowedRoles)
            .HasForeignKey(wtr => wtr.TransitionId)
            .HasConstraintName("FK_WorkflowTransitionRoles_Transition")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wtr => wtr.Role)
            .WithMany()
            .HasForeignKey(wtr => wtr.RoleId)
            .HasConstraintName("FK_WorkflowTransitionRoles_Role")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
