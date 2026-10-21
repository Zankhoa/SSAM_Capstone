using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");

        builder.HasKey(pm => pm.Id);
        builder.Property(pm => pm.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(pm => pm.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(pm => pm.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(pm => pm.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(pm => pm.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(pm => pm.JoinedAt)
            .HasColumnName("joined_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(pm => new { pm.ProjectId, pm.UserId })
            .IsUnique()
            .HasDatabaseName("UQ_ProjectMembers_Project_User");

        builder.HasIndex(pm => new { pm.UserId, pm.IsActive })
            .HasDatabaseName("IX_ProjectMembers_User_Active");

        builder.HasIndex(pm => new { pm.ProjectId, pm.IsActive })
            .HasDatabaseName("IX_ProjectMembers_Project_Active");

        builder.HasIndex(pm => pm.RoleId)
            .HasDatabaseName("IX_ProjectMembers_RoleId");

        builder.HasOne(pm => pm.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(pm => pm.ProjectId)
            .HasConstraintName("FK_ProjectMembers_Project")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pm => pm.User)
            .WithMany()
            .HasForeignKey(pm => pm.UserId)
            .HasConstraintName("FK_ProjectMembers_User")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pm => pm.Role)
            .WithMany()
            .HasForeignKey(pm => pm.RoleId)
            .HasConstraintName("FK_ProjectMembers_Role")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
