using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Data.Configurations;

public class CompanyRoleConfiguration : IEntityTypeConfiguration<CompanyRole>
{
    public void Configure(EntityTypeBuilder<CompanyRole> builder)
    {
        builder.ToTable("CompanyRoles", t =>
        {
            t.HasCheckConstraint("CK_CompanyRoles_Scope", "[scope] IN ('TENANT', 'WORKSPACE', 'PROJECT')");
        });

        builder.HasKey(cr => cr.Id);
        builder.Property(cr => cr.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(cr => cr.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(cr => cr.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(cr => cr.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(cr => cr.Scope)
            .HasColumnName("scope")
            .HasMaxLength(20)
            .HasDefaultValue(RoleScopes.Tenant)
            .IsRequired();

        builder.Property(cr => cr.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(cr => cr.IsSystem)
            .HasColumnName("is_system")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(cr => cr.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(cr => cr.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(cr => cr.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(cr => new { cr.CompanyId, cr.Name })
            .IsUnique()
            .HasDatabaseName("UQ_CompanyRoles_Company_Name");

        builder.HasIndex(cr => new { cr.CompanyId, cr.Code })
            .IsUnique()
            .HasDatabaseName("UQ_CompanyRoles_Company_Code")
            .HasFilter("[code] IS NOT NULL");

        builder.HasIndex(cr => new { cr.CompanyId, cr.Scope, cr.IsActive })
            .HasDatabaseName("IX_CompanyRoles_Company_Scope");

        builder.HasOne(cr => cr.Company)
            .WithMany(c => c.Roles)
            .HasForeignKey(cr => cr.CompanyId)
            .HasConstraintName("FK_CompanyRoles_Companies")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

