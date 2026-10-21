using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class ProjectPhaseConfiguration : IEntityTypeConfiguration<ProjectPhase>
{
    public void Configure(EntityTypeBuilder<ProjectPhase> builder)
    {
        builder.ToTable("ProjectPhases");

        builder.HasKey(pp => pp.Id);
        builder.Property(pp => pp.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(pp => pp.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(pp => pp.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(pp => pp.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(pp => pp.Position)
            .HasColumnName("position")
            .IsRequired();

        builder.Property(pp => pp.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(pp => pp.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(pp => pp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(pp => new { pp.ProjectId, pp.Name })
            .IsUnique()
            .HasDatabaseName("UQ_ProjectPhases_Project_Name")
            .HasFilter("[is_active] = 1");

        builder.HasIndex(pp => new { pp.ProjectId, pp.IsActive, pp.Position })
            .HasDatabaseName("IX_ProjectPhases_Project_Order");

        builder.HasOne(pp => pp.Project)
            .WithMany(p => p.Phases)
            .HasForeignKey(pp => pp.ProjectId)
            .HasConstraintName("FK_ProjectPhases_Project")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
