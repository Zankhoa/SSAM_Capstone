using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class MembershipRequestConfiguration : IEntityTypeConfiguration<MembershipRequest>
{
    public void Configure(EntityTypeBuilder<MembershipRequest> builder)
    {
        builder.ToTable("MembershipRequests");

        builder.HasKey(mr => mr.Id);
        builder.Property(mr => mr.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(mr => mr.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(mr => mr.UserId)
            .HasColumnName("user_id")
            .IsRequired(false);

        builder.Property(mr => mr.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(mr => mr.Source)
            .HasColumnName("source")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(mr => mr.RoleName)
            .HasColumnName("role_name")
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(mr => mr.RoleId)
            .HasColumnName("role_id")
            .IsRequired(false);

        builder.Property(mr => mr.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired(false);

        builder.Property(mr => mr.WorkspaceRoleId)
            .HasColumnName("workspace_role_id")
            .IsRequired(false);

        builder.Property(mr => mr.DepartmentId)
            .HasColumnName("department_id")
            .IsRequired(false);

        builder.Property(mr => mr.Token)
            .HasColumnName("token")
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(mr => mr.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(mr => mr.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.Property(mr => mr.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(mr => mr.Token)
            .IsUnique()
            .HasDatabaseName("IX_MembershipRequests_Token")
            .HasFilter("[token] IS NOT NULL");

        builder.HasOne(mr => mr.Company)
            .WithMany()
            .HasForeignKey(mr => mr.CompanyId)
            .HasConstraintName("FK_MembershipRequests_Companies")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mr => mr.User)
            .WithMany()
            .HasForeignKey(mr => mr.UserId)
            .HasConstraintName("FK_MembershipRequests_Users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mr => mr.Department)
            .WithMany()
            .HasForeignKey(mr => mr.DepartmentId)
            .HasConstraintName("FK_MembershipRequests_Departments")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(mr => mr.Role)
            .WithMany()
            .HasForeignKey(mr => mr.RoleId)
            .HasConstraintName("FK_MembershipRequests_Role")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mr => mr.Workspace)
            .WithMany()
            .HasForeignKey(mr => mr.WorkspaceId)
            .HasConstraintName("FK_MembershipRequests_Workspaces")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mr => mr.WorkspaceRole)
            .WithMany()
            .HasForeignKey(mr => mr.WorkspaceRoleId)
            .HasConstraintName("FK_MembershipRequests_WorkspaceRole")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

