using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class CompanyInvitationConfiguration : IEntityTypeConfiguration<CompanyInvitation>
{
    public void Configure(EntityTypeBuilder<CompanyInvitation> builder)
    {
        builder.ToTable("CompanyInvitations");

        builder.HasKey(ci => ci.Id);
        builder.Property(ci => ci.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(ci => ci.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(ci => ci.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ci => ci.RoleName)
            .HasColumnName("role_name")
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(ci => ci.RoleId)
            .HasColumnName("role_id")
            .IsRequired(false);

        builder.Property(ci => ci.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired(false);

        builder.Property(ci => ci.WorkspaceRoleId)
            .HasColumnName("workspace_role_id")
            .IsRequired(false);

        builder.Property(ci => ci.DepartmentId)
            .HasColumnName("department_id")
            .IsRequired(false);

        builder.Property(ci => ci.Token)
            .HasColumnName("token")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ci => ci.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(ci => ci.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.Property(ci => ci.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(ci => ci.Token)
            .IsUnique()
            .HasDatabaseName("IX_CompanyInvitations_Token");

        builder.HasOne(ci => ci.Company)
            .WithMany()
            .HasForeignKey(ci => ci.CompanyId)
            .HasConstraintName("FK_CompanyInvitations_Companies")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ci => ci.Department)
            .WithMany()
            .HasForeignKey(ci => ci.DepartmentId)
            .HasConstraintName("FK_CompanyInvitations_Departments")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(ci => ci.Role)
            .WithMany()
            .HasForeignKey(ci => ci.RoleId)
            .HasConstraintName("FK_CompanyInvitations_Role")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ci => ci.Workspace)
            .WithMany()
            .HasForeignKey(ci => ci.WorkspaceId)
            .HasConstraintName("FK_CompanyInvitations_Workspaces")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ci => ci.WorkspaceRole)
            .WithMany()
            .HasForeignKey(ci => ci.WorkspaceRoleId)
            .HasConstraintName("FK_CompanyInvitations_WorkspaceRole")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
