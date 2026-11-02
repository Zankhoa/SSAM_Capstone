using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class DocumentNodeConfiguration : IEntityTypeConfiguration<DocumentNode>
{
    public void Configure(EntityTypeBuilder<DocumentNode> builder)
    {
        builder.ToTable("DocumentNodes", t =>
        {
            t.HasCheckConstraint("CK_DocumentNodes_Kind", "[kind] IN ('FOLDER','PAGE')");
            t.HasCheckConstraint("CK_DocumentNodes_DocType",
                "([kind] = 'FOLDER' AND [doc_type] IS NULL) OR ([kind] = 'PAGE' AND [doc_type] IN ('URD','BRD','SRS','ADR','UI_SPEC','TEST_PLAN','OTHER'))");
        });

        builder.HasKey(d => d.Id).HasName("PK_DocumentNodes");
        builder.Property(d => d.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(d => d.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(d => d.ParentId).HasColumnName("parent_id");

        builder.Property(d => d.Kind).HasColumnName("kind").IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(d => d.Title).HasColumnName("title").IsRequired().HasMaxLength(150);
        builder.Property(d => d.DocType).HasColumnName("doc_type").HasMaxLength(20).IsUnicode(false);
        builder.Property(d => d.Code).HasColumnName("code").HasMaxLength(30).IsUnicode(false);
        builder.Property(d => d.SortOrder).HasColumnName("sort_order").IsRequired().HasDefaultValue(0);

        builder.Property(d => d.CurrentVersionId).HasColumnName("current_version_id");
        builder.Property(d => d.BaselineVersionId).HasColumnName("baseline_version_id");
        builder.Property(d => d.CreatedById).HasColumnName("created_by_id").IsRequired();
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(d => new { d.ProjectId, d.Code })
            .IsUnique()
            .HasFilter("[code] IS NOT NULL")
            .HasDatabaseName("UQ_DocumentNodes_Project_Code");

        builder.HasIndex(d => new { d.ProjectId, d.ParentId, d.SortOrder })
            .HasDatabaseName("IX_DocumentNodes_Project_Parent");

        builder.HasOne(d => d.Project)
            .WithMany()
            .HasForeignKey(d => d.ProjectId)
            .HasConstraintName("FK_DocumentNodes_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(d => d.ParentId)
            .HasConstraintName("FK_DocumentNodes_Parent")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(d => d.CurrentVersion)
            .WithMany()
            .HasForeignKey(d => d.CurrentVersionId)
            .HasConstraintName("FK_DocumentNodes_CurrentVersion")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(d => d.BaselineVersion)
            .WithMany()
            .HasForeignKey(d => d.BaselineVersionId)
            .HasConstraintName("FK_DocumentNodes_BaselineVersion")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(d => d.CreatedBy)
            .WithMany()
            .HasForeignKey(d => d.CreatedById)
            .HasConstraintName("FK_DocumentNodes_CreatedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersions", t =>
        {
            t.HasCheckConstraint(
                "CK_DocumentVersions_Status",
                "[status] IN ('DRAFT','IN_REVIEW','CHANGES_REQUESTED','APPROVED','SUPERSEDED')");
        });

        builder.HasKey(v => v.Id).HasName("PK_DocumentVersions");
        builder.Property(v => v.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(v => v.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(v => v.Label).HasColumnName("label").IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(v => v.Status).HasColumnName("status").IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(v => v.Subtitle).HasColumnName("subtitle").HasMaxLength(300);
        builder.Property(v => v.Content).HasColumnName("content").IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(v => v.ContentText).HasColumnName("content_text").IsRequired().HasColumnType("nvarchar(max)").HasDefaultValue(string.Empty);
        builder.Property(v => v.BaseVersionId).HasColumnName("base_version_id");
        builder.Property(v => v.RowVersion).HasColumnName("row_version").IsRequired().HasDefaultValue(1);

        builder.Property(v => v.UpdatedById).HasColumnName("updated_by_id").IsRequired();
        builder.Property(v => v.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(v => v.SubmittedById).HasColumnName("submitted_by_id");
        builder.Property(v => v.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(v => v.ReviewedById).HasColumnName("reviewed_by_id");
        builder.Property(v => v.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(v => v.ReviewNote).HasColumnName("review_note").HasMaxLength(1000);

        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(v => new { v.DocumentId, v.Label })
            .IsUnique()
            .HasDatabaseName("UQ_DocumentVersions_Doc_Label");

        builder.HasIndex(v => new { v.DocumentId, v.Status })
            .HasDatabaseName("IX_DocumentVersions_Doc_Status");

        builder.HasOne(v => v.Document)
            .WithMany(d => d.Versions)
            .HasForeignKey(v => v.DocumentId)
            .HasConstraintName("FK_DocumentVersions_Document")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.BaseVersion)
            .WithMany()
            .HasForeignKey(v => v.BaseVersionId)
            .HasConstraintName("FK_DocumentVersions_BaseVersion")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(v => v.UpdatedBy)
            .WithMany()
            .HasForeignKey(v => v.UpdatedById)
            .HasConstraintName("FK_DocumentVersions_UpdatedBy")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(v => v.Author)
            .WithMany()
            .HasForeignKey(v => v.AuthorId)
            .HasConstraintName("FK_DocumentVersions_Author")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(v => v.SubmittedBy)
            .WithMany()
            .HasForeignKey(v => v.SubmittedById)
            .HasConstraintName("FK_DocumentVersions_SubmittedBy")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(v => v.ReviewedBy)
            .WithMany()
            .HasForeignKey(v => v.ReviewedById)
            .HasConstraintName("FK_DocumentVersions_ReviewedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class RequirementItemConfiguration : IEntityTypeConfiguration<RequirementItem>
{
    public void Configure(EntityTypeBuilder<RequirementItem> builder)
    {
        builder.ToTable("RequirementItems");

        builder.HasKey(r => r.Id).HasName("PK_RequirementItems");
        builder.Property(r => r.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(r => r.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(r => r.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(r => r.ReqUid).HasColumnName("req_uid").IsRequired().HasMaxLength(30).IsUnicode(false);
        builder.Property(r => r.AnchorId).HasColumnName("anchor_id").IsRequired().HasMaxLength(36).IsUnicode(false);
        builder.Property(r => r.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        builder.Property(r => r.BodyPreview).HasColumnName("body_preview").IsRequired().HasMaxLength(400).HasDefaultValue(string.Empty);
        builder.Property(r => r.BodyLength).HasColumnName("body_length").IsRequired().HasDefaultValue(0);
        builder.Property(r => r.AcceptanceCriteria).HasColumnName("acceptance_criteria").IsRequired().HasColumnType("nvarchar(max)").HasDefaultValue("[]");
        builder.Property(r => r.HasTable).HasColumnName("has_table").IsRequired().HasDefaultValue(false);
        builder.Property(r => r.HasDiagram).HasColumnName("has_diagram").IsRequired().HasDefaultValue(false);
        builder.Property(r => r.HasFigma).HasColumnName("has_figma").IsRequired().HasDefaultValue(false);
        builder.Property(r => r.ContentHash).HasColumnName("content_hash").IsRequired().HasColumnType("char(64)");
        builder.Property(r => r.SortOrder).HasColumnName("sort_order").IsRequired().HasDefaultValue(0);

        builder.HasIndex(r => new { r.VersionId, r.ReqUid })
            .IsUnique()
            .HasDatabaseName("UQ_RequirementItems_Version_Uid");

        builder.HasIndex(r => new { r.DocumentId, r.ReqUid })
            .HasDatabaseName("IX_RequirementItems_Doc_Uid");

        builder.HasOne(r => r.Document)
            .WithMany()
            .HasForeignKey(r => r.DocumentId)
            .HasConstraintName("FK_RequirementItems_Document")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(r => r.Version)
            .WithMany(v => v.Requirements)
            .HasForeignKey(r => r.VersionId)
            .HasConstraintName("FK_RequirementItems_Version")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentCommentConfiguration : IEntityTypeConfiguration<DocumentComment>
{
    public void Configure(EntityTypeBuilder<DocumentComment> builder)
    {
        builder.ToTable("DocumentComments");

        builder.HasKey(c => c.Id).HasName("PK_DocumentComments");
        builder.Property(c => c.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(c => c.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(c => c.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(c => c.ParentId).HasColumnName("parent_id");
        builder.Property(c => c.AnchorId).HasColumnName("anchor_id").HasMaxLength(36).IsUnicode(false);
        builder.Property(c => c.Quote).HasColumnName("quote").HasMaxLength(300);
        builder.Property(c => c.Body).HasColumnName("body").IsRequired().HasMaxLength(2000);
        builder.Property(c => c.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(c => c.ResolvedById).HasColumnName("resolved_by_id");
        builder.Property(c => c.ResolvedAt).HasColumnName("resolved_at");

        builder.HasIndex(c => new { c.DocumentId, c.VersionId, c.ResolvedAt })
            .HasDatabaseName("IX_DocumentComments_Doc_Version");

        builder.HasIndex(c => c.ParentId)
            .HasDatabaseName("IX_DocumentComments_Parent");

        builder.HasOne(c => c.Document)
            .WithMany(d => d.Comments)
            .HasForeignKey(c => c.DocumentId)
            .HasConstraintName("FK_DocumentComments_Document")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(c => c.Version)
            .WithMany()
            .HasForeignKey(c => c.VersionId)
            .HasConstraintName("FK_DocumentComments_Version")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Parent)
            .WithMany(p => p.Replies)
            .HasForeignKey(c => c.ParentId)
            .HasConstraintName("FK_DocumentComments_Parent")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .HasConstraintName("FK_DocumentComments_Author")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(c => c.ResolvedBy)
            .WithMany()
            .HasForeignKey(c => c.ResolvedById)
            .HasConstraintName("FK_DocumentComments_ResolvedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class DocumentCommentMentionConfiguration : IEntityTypeConfiguration<DocumentCommentMention>
{
    public void Configure(EntityTypeBuilder<DocumentCommentMention> builder)
    {
        builder.ToTable("DocumentCommentMentions");

        builder.HasKey(m => new { m.CommentId, m.UserId })
            .HasName("PK_DocumentCommentMentions");

        builder.Property(m => m.CommentId).HasColumnName("comment_id");
        builder.Property(m => m.UserId).HasColumnName("user_id");

        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("IX_DocumentCommentMentions_User");

        builder.HasOne(m => m.Comment)
            .WithMany(c => c.Mentions)
            .HasForeignKey(m => m.CommentId)
            .HasConstraintName("FK_DocumentCommentMentions_Comment")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .HasConstraintName("FK_DocumentCommentMentions_User")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class DocumentActivityConfiguration : IEntityTypeConfiguration<DocumentActivity>
{
    public void Configure(EntityTypeBuilder<DocumentActivity> builder)
    {
        builder.ToTable("DocumentActivities", t =>
        {
            t.HasCheckConstraint(
                "CK_DocumentActivities_Action",
                "[action] IN ('CREATED','DELETED','GENERATED_FROM_NOTES','CONTENT_SAVED','SUBMITTED','APPROVED','CHANGES_REQUESTED','REVISION_CREATED','RENAMED','MOVED','REQ_CONFIRMED_FRESH','COMMENTED')");
        });

        builder.HasKey(a => a.Id).HasName("PK_DocumentActivities");
        builder.Property(a => a.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(a => a.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(a => a.VersionId).HasColumnName("version_id");
        builder.Property(a => a.Action).HasColumnName("action").IsRequired().HasMaxLength(30).IsUnicode(false);
        builder.Property(a => a.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(a => a.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(a => new { a.DocumentId, a.CreatedAt })
            .HasDatabaseName("IX_DocumentActivities_Doc_Time");

        builder.HasOne(a => a.Document)
            .WithMany(d => d.Activities)
            .HasForeignKey(a => a.DocumentId)
            .HasConstraintName("FK_DocumentActivities_Document")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Version)
            .WithMany()
            .HasForeignKey(a => a.VersionId)
            .HasConstraintName("FK_DocumentActivities_Version")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorId)
            .HasConstraintName("FK_DocumentActivities_Actor")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class DocumentGenerationConfiguration : IEntityTypeConfiguration<DocumentGeneration>
{
    public void Configure(EntityTypeBuilder<DocumentGeneration> builder)
    {
        builder.ToTable("DocumentGenerations");

        builder.HasKey(g => g.Id).HasName("PK_DocumentGenerations");
        builder.Property(g => g.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(g => g.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(g => g.DocumentId).HasColumnName("document_id");
        builder.Property(g => g.RawNotes).HasColumnName("raw_notes").IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(g => g.Fields).HasColumnName("fields").IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(g => g.Items).HasColumnName("items").IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(g => g.CreatedById).HasColumnName("created_by_id").IsRequired();
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(g => g.DocumentId)
            .IsUnique()
            .HasFilter("[document_id] IS NOT NULL")
            .HasDatabaseName("UQ_DocumentGenerations_Document");

        builder.HasOne(g => g.Project)
            .WithMany()
            .HasForeignKey(g => g.ProjectId)
            .HasConstraintName("FK_DocumentGenerations_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.Document)
            .WithMany()
            .HasForeignKey(g => g.DocumentId)
            .HasConstraintName("FK_DocumentGenerations_Document")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(g => g.CreatedBy)
            .WithMany()
            .HasForeignKey(g => g.CreatedById)
            .HasConstraintName("FK_DocumentGenerations_CreatedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
