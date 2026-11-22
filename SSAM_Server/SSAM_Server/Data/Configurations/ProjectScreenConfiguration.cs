using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class ProjectScreenConfiguration : IEntityTypeConfiguration<ProjectScreen>
{
    public void Configure(EntityTypeBuilder<ProjectScreen> builder)
    {
        builder.ToTable("ProjectScreens", t =>
        {
            t.HasCheckConstraint("CK_ProjectScreens_Kind", "[kind] IN ('SCREEN','LAYOUT')");
        });

        builder.HasKey(s => s.Id).HasName("PK_ProjectScreens");
        builder.Property(s => s.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(s => s.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(s => s.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        builder.Property(s => s.Kind).HasColumnName("kind").IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(s => s.FigmaUrl).HasColumnName("figma_url").HasMaxLength(1000);
        builder.Property(s => s.ImageFileName).HasColumnName("image_file_name").HasMaxLength(255);
        builder.Property(s => s.ImageStoredName).HasColumnName("image_stored_name").HasMaxLength(255);
        builder.Property(s => s.ImageContentType).HasColumnName("image_content_type").HasMaxLength(100);
        builder.Property(s => s.DocumentId).HasColumnName("document_id");
        builder.Property(s => s.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(s => s.CreatedById).HasColumnName("created_by_id").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(s => new { s.ProjectId, s.CreatedAt })
            .HasDatabaseName("IX_ProjectScreens_Project_Time");

        builder.HasOne(s => s.Project)
            .WithMany()
            .HasForeignKey(s => s.ProjectId)
            .HasConstraintName("FK_ProjectScreens_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Document)
            .WithMany()
            .HasForeignKey(s => s.DocumentId)
            .HasConstraintName("FK_ProjectScreens_Documents")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedById)
            .HasConstraintName("FK_ProjectScreens_CreatedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
