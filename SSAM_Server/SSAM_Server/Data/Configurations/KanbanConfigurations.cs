using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprints", t =>
        {
            t.HasCheckConstraint("CK_Sprints_Status", "[status] IN ('PLANNED','ACTIVE','COMPLETED')");
            t.HasCheckConstraint("CK_Sprints_Dates", "[end_date] IS NULL OR [start_date] IS NULL OR [end_date] >= [start_date]");
            t.HasCheckConstraint("CK_Sprints_Capacity", "[capacity_points] IS NULL OR [capacity_points] >= 0");
        });

        builder.HasKey(s => s.Id).HasName("PK_Sprints");
        builder.Property(s => s.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(s => s.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
        builder.Property(s => s.Goal).HasColumnName("goal").HasMaxLength(500);
        builder.Property(s => s.StartDate).HasColumnName("start_date").HasColumnType("date");
        builder.Property(s => s.EndDate).HasColumnName("end_date").HasColumnType("date");
        builder.Property(s => s.Status).HasColumnName("status").IsRequired().HasMaxLength(15).IsUnicode(false);
        builder.Property(s => s.CapacityPoints).HasColumnName("capacity_points");
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");
        builder.Property(s => s.CreatedById).HasColumnName("created_by_id").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(s => s.ProjectId)
            .IsUnique()
            .HasFilter("[status] = 'ACTIVE'")
            .HasDatabaseName("UQ_Sprints_OneActive");

        builder.HasIndex(s => new { s.ProjectId, s.Status })
            .HasDatabaseName("IX_Sprints_Project_Status");

        builder.HasOne(s => s.Project)
            .WithMany()
            .HasForeignKey(s => s.ProjectId)
            .HasConstraintName("FK_Sprints_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedById)
            .HasConstraintName("FK_Sprints_CreatedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("Issues", t =>
        {
            t.HasCheckConstraint("CK_Issues_Type", "[type] IN ('TASK','BUG','STORY','DESIGN')");
            t.HasCheckConstraint("CK_Issues_Priority", "[priority] IN ('HIGHEST','HIGH','MEDIUM','LOW')");
            t.HasCheckConstraint("CK_Issues_Points", "[points] IS NULL OR ([points] >= 0 AND [points] <= 100)");
        });

        builder.HasKey(i => i.Id).HasName("PK_Issues");
        builder.Property(i => i.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(i => i.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(i => i.Key).HasColumnName("key").IsRequired().HasMaxLength(25).IsUnicode(false);
        builder.Property(i => i.Type).HasColumnName("type").IsRequired().HasMaxLength(15).IsUnicode(false);
        builder.Property(i => i.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        builder.Property(i => i.Description).HasColumnName("description").IsRequired().HasColumnType("nvarchar(max)").HasDefaultValue(string.Empty);
        builder.Property(i => i.StatusId).HasColumnName("status_id").IsRequired();
        builder.Property(i => i.Priority).HasColumnName("priority").IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(i => i.Points).HasColumnName("points");
        builder.Property(i => i.DepartmentId).HasColumnName("department_id");
        builder.Property(i => i.AssigneeId).HasColumnName("assignee_id");
        builder.Property(i => i.ReporterId).HasColumnName("reporter_id").IsRequired();
        builder.Property(i => i.SprintId).HasColumnName("sprint_id");
        builder.Property(i => i.ParentId).HasColumnName("parent_id");

        builder.Property(i => i.Rank)
            .HasColumnName("rank")
            .IsRequired()
            .HasMaxLength(64)
            .IsUnicode(false)
            .UseCollation("Latin1_General_BIN2");

        builder.Property(i => i.Labels).HasColumnName("labels").IsRequired().HasMaxLength(1000).HasDefaultValue("[]");
        builder.Property(i => i.DueDate).HasColumnName("due_date").HasColumnType("date");
        builder.Property(i => i.ReqChanged).HasColumnName("req_changed").IsRequired().HasDefaultValue(false);
        builder.Property(i => i.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);

        builder.Property(i => i.RowVer)
            .HasColumnName("row_ver")
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(i => new { i.ProjectId, i.Key })
            .IsUnique()
            .HasDatabaseName("UQ_Issues_Project_Key");

        builder.HasIndex(i => new { i.ProjectId, i.SprintId, i.StatusId, i.Rank })
            .HasDatabaseName("IX_Issues_Project_Sprint_Status_Rank");

        builder.HasIndex(i => new { i.ProjectId, i.AssigneeId, i.IsDeleted })
            .HasDatabaseName("IX_Issues_Project_Assignee");

        builder.HasIndex(i => i.ParentId)
            .HasDatabaseName("IX_Issues_Parent");

        builder.HasOne(i => i.Project)
            .WithMany()
            .HasForeignKey(i => i.ProjectId)
            .HasConstraintName("FK_Issues_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Status)
            .WithMany()
            .HasForeignKey(i => i.StatusId)
            .HasConstraintName("FK_Issues_Status")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(i => i.Assignee)
            .WithMany()
            .HasForeignKey(i => i.AssigneeId)
            .HasConstraintName("FK_Issues_Assignee")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(i => i.Department)
            .WithMany()
            .HasForeignKey(i => i.DepartmentId)
            .HasConstraintName("FK_Issues_Department")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(i => i.Reporter)
            .WithMany()
            .HasForeignKey(i => i.ReporterId)
            .HasConstraintName("FK_Issues_Reporter")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(i => i.Sprint)
            .WithMany(s => s.Issues)
            .HasForeignKey(i => i.SprintId)
            .HasConstraintName("FK_Issues_Sprint")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(i => i.Parent)
            .WithMany(p => p.Subtasks)
            .HasForeignKey(i => i.ParentId)
            .HasConstraintName("FK_Issues_Parent")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class IssueRequirementLinkConfiguration : IEntityTypeConfiguration<IssueRequirementLink>
{
    public void Configure(EntityTypeBuilder<IssueRequirementLink> builder)
    {
        builder.ToTable("IssueRequirementLinks");

        builder.HasKey(l => new { l.IssueId, l.DocumentId, l.ReqUid })
            .HasName("PK_IssueRequirementLinks");

        builder.Property(l => l.IssueId).HasColumnName("issue_id");
        builder.Property(l => l.DocumentId).HasColumnName("document_id");
        builder.Property(l => l.ReqUid).HasColumnName("req_uid").IsRequired().HasMaxLength(30).IsUnicode(false);
        builder.Property(l => l.ReqHash).HasColumnName("req_hash").IsRequired().HasColumnType("char(64)");
        builder.Property(l => l.ChangedVersionLabel).HasColumnName("changed_version_label").HasMaxLength(10).IsUnicode(false);
        builder.Property(l => l.LinkedAt).HasColumnName("linked_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(l => l.LinkedById).HasColumnName("linked_by_id").IsRequired();

        builder.HasIndex(l => new { l.DocumentId, l.ReqUid })
            .HasDatabaseName("IX_IssueRequirementLinks_Doc_Req");

        builder.HasOne(l => l.Issue)
            .WithMany(i => i.RequirementLinks)
            .HasForeignKey(l => l.IssueId)
            .HasConstraintName("FK_IssueRequirementLinks_Issue")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Document)
            .WithMany()
            .HasForeignKey(l => l.DocumentId)
            .HasConstraintName("FK_IssueRequirementLinks_Document")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(l => l.LinkedBy)
            .WithMany()
            .HasForeignKey(l => l.LinkedById)
            .HasConstraintName("FK_IssueRequirementLinks_LinkedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class BoardWipLimitConfiguration : IEntityTypeConfiguration<BoardWipLimit>
{
    public void Configure(EntityTypeBuilder<BoardWipLimit> builder)
    {
        builder.ToTable("BoardWipLimits", t =>
        {
            t.HasCheckConstraint("CK_BoardWipLimits_Limit", "[wip_limit] >= 1");
        });

        builder.HasKey(w => new { w.ProjectId, w.StatusId })
            .HasName("PK_BoardWipLimits");

        builder.Property(w => w.ProjectId).HasColumnName("project_id");
        builder.Property(w => w.StatusId).HasColumnName("status_id");
        builder.Property(w => w.WipLimit).HasColumnName("wip_limit").IsRequired();

        builder.HasOne(w => w.Project)
            .WithMany()
            .HasForeignKey(w => w.ProjectId)
            .HasConstraintName("FK_BoardWipLimits_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Status)
            .WithMany()
            .HasForeignKey(w => w.StatusId)
            .HasConstraintName("FK_BoardWipLimits_Status")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class IssueCommentConfiguration : IEntityTypeConfiguration<IssueComment>
{
    public void Configure(EntityTypeBuilder<IssueComment> builder)
    {
        builder.ToTable("IssueComments");

        builder.HasKey(c => c.Id).HasName("PK_IssueComments");
        builder.Property(c => c.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(c => c.IssueId).HasColumnName("issue_id").IsRequired();
        builder.Property(c => c.ParentId).HasColumnName("parent_id");
        builder.Property(c => c.Body).HasColumnName("body").IsRequired().HasMaxLength(2000);
        builder.Property(c => c.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(c => new { c.IssueId, c.CreatedAt })
            .HasDatabaseName("IX_IssueComments_Issue_Time");

        builder.HasIndex(c => c.ParentId)
            .HasDatabaseName("IX_IssueComments_Parent");

        builder.HasOne(c => c.Issue)
            .WithMany(i => i.Comments)
            .HasForeignKey(c => c.IssueId)
            .HasConstraintName("FK_IssueComments_Issue")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Parent)
            .WithMany(p => p.Replies)
            .HasForeignKey(c => c.ParentId)
            .HasConstraintName("FK_IssueComments_Parent")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .HasConstraintName("FK_IssueComments_Author")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class IssueCommentMentionConfiguration : IEntityTypeConfiguration<IssueCommentMention>
{
    public void Configure(EntityTypeBuilder<IssueCommentMention> builder)
    {
        builder.ToTable("IssueCommentMentions");

        builder.HasKey(m => new { m.CommentId, m.UserId })
            .HasName("PK_IssueCommentMentions");

        builder.Property(m => m.CommentId).HasColumnName("comment_id");
        builder.Property(m => m.UserId).HasColumnName("user_id");

        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("IX_IssueCommentMentions_User");

        builder.HasOne(m => m.Comment)
            .WithMany(c => c.Mentions)
            .HasForeignKey(m => m.CommentId)
            .HasConstraintName("FK_IssueCommentMentions_Comment")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .HasConstraintName("FK_IssueCommentMentions_User")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class IssueActivityConfiguration : IEntityTypeConfiguration<IssueActivity>
{
    public void Configure(EntityTypeBuilder<IssueActivity> builder)
    {
        builder.ToTable("IssueActivities", t =>
        {
            t.HasCheckConstraint(
                "CK_IssueActivities_Action",
                "[action] IN ('CREATED','UPDATED','TRANSITIONED','ASSIGNED','SPRINT_CHANGED','REQ_LINKED','REQ_ACKED','REQ_CHANGED','COMMENTED','DELETED')");
        });

        builder.HasKey(a => a.Id).HasName("PK_IssueActivities");
        builder.Property(a => a.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(a => a.IssueId).HasColumnName("issue_id").IsRequired();
        builder.Property(a => a.Action).HasColumnName("action").IsRequired().HasMaxLength(25).IsUnicode(false);
        builder.Property(a => a.ActorId).HasColumnName("actor_id");
        builder.Property(a => a.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(a => new { a.IssueId, a.CreatedAt })
            .HasDatabaseName("IX_IssueActivities_Issue_Time");

        builder.HasOne(a => a.Issue)
            .WithMany(i => i.Activities)
            .HasForeignKey(a => a.IssueId)
            .HasConstraintName("FK_IssueActivities_Issue")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorId)
            .HasConstraintName("FK_IssueActivities_Actor")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class IssueAttachmentConfiguration : IEntityTypeConfiguration<IssueAttachment>
{
    public void Configure(EntityTypeBuilder<IssueAttachment> builder)
    {
        builder.ToTable("IssueAttachments");

        builder.HasKey(a => a.Id).HasName("PK_IssueAttachments");
        builder.Property(a => a.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(a => a.IssueId).HasColumnName("issue_id").IsRequired();
        builder.Property(a => a.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(255);
        builder.Property(a => a.StoredFileName).HasColumnName("stored_file_name").IsRequired().HasMaxLength(255);
        builder.Property(a => a.ContentType).HasColumnName("content_type").IsRequired().HasMaxLength(150);
        builder.Property(a => a.FileSize).HasColumnName("file_size").IsRequired();
        builder.Property(a => a.UploadedById).HasColumnName("uploaded_by_id").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(a => new { a.IssueId, a.CreatedAt })
            .HasDatabaseName("IX_IssueAttachments_Issue_Time");

        builder.HasOne(a => a.Issue)
            .WithMany()
            .HasForeignKey(a => a.IssueId)
            .HasConstraintName("FK_IssueAttachments_Issue")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .HasConstraintName("FK_IssueAttachments_UploadedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
