using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects", t =>
        {
            t.HasCheckConstraint("CK_Projects_Sdlc", "[sdlc_method] IN ('SCRUM', 'KANBAN', 'CUSTOM')");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(p => p.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(p => p.Key)
            .HasColumnName("key")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(p => p.SdlcMethod)
            .HasColumnName("sdlc_method")
            .HasMaxLength(20)
            .HasDefaultValue(SdlcMethods.Custom)
            .IsRequired();

        builder.Property(p => p.AiEnabled)
            .HasColumnName("ai_enabled")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(p => p.ArchivedAt)
            .HasColumnName("archived_at")
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.HasIndex(p => new { p.WorkspaceId, p.Key })
            .IsUnique()
            .HasDatabaseName("UQ_Projects_Workspace_Key");

        builder.HasIndex(p => new { p.WorkspaceId, p.IsActive, p.UpdatedAt })
            .HasDatabaseName("IX_Projects_Workspace_Active");

        builder.HasOne(p => p.Workspace)
            .WithMany(w => w.Projects)
            .HasForeignKey(p => p.WorkspaceId)
            .HasConstraintName("FK_Projects_Workspace")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Creator)
            .WithMany()
            .HasForeignKey(p => p.CreatedBy)
            .HasConstraintName("FK_Projects_CreatedBy")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
