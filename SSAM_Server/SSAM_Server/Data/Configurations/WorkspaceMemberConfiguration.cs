using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("WorkspaceMembers");

        builder.HasKey(wm => wm.Id);
        builder.Property(wm => wm.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(wm => wm.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(wm => wm.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(wm => wm.RoleId)
            .HasColumnName("role_id")
            .IsRequired(false);

        builder.Property(wm => wm.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(wm => wm.JoinedAt)
            .HasColumnName("joined_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(wm => new { wm.WorkspaceId, wm.UserId })
            .IsUnique()
            .HasDatabaseName("UQ_WorkspaceMembers_Workspace_User");

        builder.HasIndex(wm => new { wm.UserId, wm.IsActive })
            .HasDatabaseName("IX_WorkspaceMembers_User_Active");

        builder.HasIndex(wm => wm.RoleId)
            .HasDatabaseName("IX_WorkspaceMembers_RoleId");

        builder.HasOne(wm => wm.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(wm => wm.WorkspaceId)
            .HasConstraintName("FK_WorkspaceMembers_Workspaces")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(wm => wm.User)
            .WithMany()
            .HasForeignKey(wm => wm.UserId)
            .HasConstraintName("FK_WorkspaceMembers_Users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(wm => wm.Role)
            .WithMany()
            .HasForeignKey(wm => wm.RoleId)
            .HasConstraintName("FK_WorkspaceMembers_Role")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

