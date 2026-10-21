using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(w => w.CompanyId)
            .HasColumnName("company_id")
            .IsRequired(false);

        builder.Property(w => w.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(w => w.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.Description)
            .HasColumnName("description")
            .IsRequired(false);

        builder.Property(w => w.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(w => w.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(w => w.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(w => w.ArchivedAt)
            .HasColumnName("archived_at")
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.HasIndex(w => new { w.CompanyId, w.Code })
            .IsUnique()
            .HasDatabaseName("UQ_Workspaces_Company_Code")
            .HasFilter("[company_id] IS NOT NULL");

        builder.HasIndex(w => new { w.CompanyId, w.IsActive })
            .HasDatabaseName("IX_Workspaces_Company_Active");

        builder.HasOne(w => w.Company)
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .HasConstraintName("FK_Workspaces_Companies")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Creator)
            .WithMany()
            .HasForeignKey(w => w.CreatedBy)
            .HasConstraintName("FK_Workspaces_CreatedBy")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

