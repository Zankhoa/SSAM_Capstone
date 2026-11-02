using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class TestFolderConfiguration : IEntityTypeConfiguration<TestFolder>
{
    public void Configure(EntityTypeBuilder<TestFolder> builder)
    {
        builder.ToTable("TestFolders");

        builder.HasKey(f => f.Id).HasName("PK_TestFolders");
        builder.Property(f => f.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(f => f.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(f => f.ParentId).HasColumnName("parent_id");
        builder.Property(f => f.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
        builder.Property(f => f.SortOrder).HasColumnName("sort_order").IsRequired().HasDefaultValue(0);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(f => new { f.ProjectId, f.ParentId, f.SortOrder })
            .HasDatabaseName("IX_TestFolders_Project_Parent");

        builder.HasOne(f => f.Project)
            .WithMany()
            .HasForeignKey(f => f.ProjectId)
            .HasConstraintName("FK_TestFolders_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(f => f.ParentId)
            .HasConstraintName("FK_TestFolders_Parent")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.ToTable("TestCases", t =>
        {
            t.HasCheckConstraint(
                "CK_TestCases_Type",
                "[type] IN ('FUNCTIONAL','INTEGRATION','SECURITY','E2E','UNIT','REGRESSION')");
            t.HasCheckConstraint(
                "CK_TestCases_Priority",
                "[priority] IN ('HIGH','MEDIUM','LOW')");
            t.HasCheckConstraint(
                "CK_TestCases_LastResult",
                "[last_result] IS NULL OR [last_result] IN ('NOT_RUN','PASS','FAIL','BLOCKED')");
        });

        builder.HasKey(tc => tc.Id).HasName("PK_TestCases");
        builder.Property(tc => tc.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(tc => tc.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(tc => tc.Key).HasColumnName("key").IsRequired().HasMaxLength(25).IsUnicode(false);
        builder.Property(tc => tc.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        builder.Property(tc => tc.Type).HasColumnName("type").IsRequired().HasMaxLength(15).IsUnicode(false);
        builder.Property(tc => tc.Priority).HasColumnName("priority").IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(tc => tc.Preconditions).HasColumnName("preconditions").IsRequired().HasColumnType("nvarchar(max)").HasDefaultValue(string.Empty);
        builder.Property(tc => tc.Steps).HasColumnName("steps").IsRequired().HasColumnType("nvarchar(max)").HasDefaultValue("[]");
        builder.Property(tc => tc.StepCount).HasColumnName("step_count").IsRequired().HasDefaultValue(0);
        builder.Property(tc => tc.FolderId).HasColumnName("folder_id");
        builder.Property(tc => tc.NeedsReview).HasColumnName("needs_review").IsRequired().HasDefaultValue(false);
        builder.Property(tc => tc.LastResult).HasColumnName("last_result").HasMaxLength(10).IsUnicode(false);
        builder.Property(tc => tc.LastRunAt).HasColumnName("last_run_at");
        builder.Property(tc => tc.LastRunById).HasColumnName("last_run_by_id");
        builder.Property(tc => tc.CreatedById).HasColumnName("created_by_id").IsRequired();
        builder.Property(tc => tc.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(tc => tc.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(tc => tc.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);

        builder.HasIndex(tc => new { tc.ProjectId, tc.Key })
            .IsUnique()
            .HasDatabaseName("UQ_TestCases_Project_Key");

        builder.HasIndex(tc => new { tc.ProjectId, tc.FolderId, tc.IsDeleted })
            .HasDatabaseName("IX_TestCases_Project_Folder");

        builder.HasOne(tc => tc.Project)
            .WithMany()
            .HasForeignKey(tc => tc.ProjectId)
            .HasConstraintName("FK_TestCases_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tc => tc.Folder)
            .WithMany(f => f.TestCases)
            .HasForeignKey(tc => tc.FolderId)
            .HasConstraintName("FK_TestCases_Folder")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(tc => tc.LastRunBy)
            .WithMany()
            .HasForeignKey(tc => tc.LastRunById)
            .HasConstraintName("FK_TestCases_LastRunBy")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(tc => tc.CreatedBy)
            .WithMany()
            .HasForeignKey(tc => tc.CreatedById)
            .HasConstraintName("FK_TestCases_CreatedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class TestCaseRequirementLinkConfiguration : IEntityTypeConfiguration<TestCaseRequirementLink>
{
    public void Configure(EntityTypeBuilder<TestCaseRequirementLink> builder)
    {
        builder.ToTable("TestCaseRequirementLinks");

        builder.HasKey(l => new { l.TestCaseId, l.DocumentId, l.ReqUid })
            .HasName("PK_TestCaseRequirementLinks");

        builder.Property(l => l.TestCaseId).HasColumnName("test_case_id");
        builder.Property(l => l.DocumentId).HasColumnName("document_id");
        builder.Property(l => l.ReqUid).HasColumnName("req_uid").IsRequired().HasMaxLength(30).IsUnicode(false);
        builder.Property(l => l.ReqHash).HasColumnName("req_hash").IsRequired().HasColumnType("char(64)");
        builder.Property(l => l.ChangedVersionLabel).HasColumnName("changed_version_label").HasMaxLength(10).IsUnicode(false);
        builder.Property(l => l.LinkedAt).HasColumnName("linked_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(l => new { l.DocumentId, l.ReqUid })
            .HasDatabaseName("IX_TestCaseRequirementLinks_Doc_Req");

        builder.HasOne(l => l.TestCase)
            .WithMany(tc => tc.RequirementLinks)
            .HasForeignKey(l => l.TestCaseId)
            .HasConstraintName("FK_TestCaseRequirementLinks_TestCase")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Document)
            .WithMany()
            .HasForeignKey(l => l.DocumentId)
            .HasConstraintName("FK_TestCaseRequirementLinks_Document")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class TestCaseIssueLinkConfiguration : IEntityTypeConfiguration<TestCaseIssueLink>
{
    public void Configure(EntityTypeBuilder<TestCaseIssueLink> builder)
    {
        builder.ToTable("TestCaseIssueLinks");

        builder.HasKey(l => new { l.TestCaseId, l.IssueId })
            .HasName("PK_TestCaseIssueLinks");

        builder.Property(l => l.TestCaseId).HasColumnName("test_case_id");
        builder.Property(l => l.IssueId).HasColumnName("issue_id");
        builder.Property(l => l.LinkedAt).HasColumnName("linked_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(l => l.IssueId)
            .HasDatabaseName("IX_TestCaseIssueLinks_Issue");

        builder.HasOne(l => l.TestCase)
            .WithMany(tc => tc.IssueLinks)
            .HasForeignKey(l => l.TestCaseId)
            .HasConstraintName("FK_TestCaseIssueLinks_TestCase")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Issue)
            .WithMany()
            .HasForeignKey(l => l.IssueId)
            .HasConstraintName("FK_TestCaseIssueLinks_Issue")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class TestCycleConfiguration : IEntityTypeConfiguration<TestCycle>
{
    public void Configure(EntityTypeBuilder<TestCycle> builder)
    {
        builder.ToTable("TestCycles", t =>
        {
            t.HasCheckConstraint("CK_TestCycles_Status", "[status] IN ('OPEN','CLOSED')");
        });

        builder.HasKey(c => c.Id).HasName("PK_TestCycles");
        builder.Property(c => c.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(c => c.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").IsRequired().HasMaxLength(150);
        builder.Property(c => c.SprintId).HasColumnName("sprint_id");
        builder.Property(c => c.Status).HasColumnName("status").IsRequired().HasMaxLength(10).IsUnicode(false).HasDefaultValue("OPEN");
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(c => new { c.ProjectId, c.Status })
            .HasDatabaseName("IX_TestCycles_Project_Status");

        builder.HasOne(c => c.Project)
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .HasConstraintName("FK_TestCycles_Projects")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Sprint)
            .WithMany()
            .HasForeignKey(c => c.SprintId)
            .HasConstraintName("FK_TestCycles_Sprint")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .HasConstraintName("FK_TestCycles_CreatedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class TestRunConfiguration : IEntityTypeConfiguration<TestRun>
{
    public void Configure(EntityTypeBuilder<TestRun> builder)
    {
        builder.ToTable("TestRuns", t =>
        {
            t.HasCheckConstraint("CK_TestRuns_Result", "[result] IN ('NOT_RUN','PASS','FAIL','BLOCKED')");
        });

        builder.HasKey(r => r.Id).HasName("PK_TestRuns");
        builder.Property(r => r.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(r => r.CycleId).HasColumnName("cycle_id").IsRequired();
        builder.Property(r => r.TestCaseId).HasColumnName("test_case_id").IsRequired();
        builder.Property(r => r.AssigneeId).HasColumnName("assignee_id");
        builder.Property(r => r.Result).HasColumnName("result").IsRequired().HasMaxLength(10).IsUnicode(false).HasDefaultValue("NOT_RUN");
        builder.Property(r => r.Note).HasColumnName("note").HasMaxLength(2000);
        builder.Property(r => r.ExecutedAt).HasColumnName("executed_at");
        builder.Property(r => r.ExecutedById).HasColumnName("executed_by_id");
        builder.Property(r => r.NeedsRerun).HasColumnName("needs_rerun").IsRequired().HasDefaultValue(false);

        builder.Property(r => r.RowVer)
            .HasColumnName("row_ver")
            .IsRowVersion()
            .IsRequired();

        builder.HasIndex(r => new { r.CycleId, r.TestCaseId })
            .IsUnique()
            .HasDatabaseName("UQ_TestRuns_Cycle_TestCase");

        builder.HasIndex(r => new { r.CycleId, r.Result })
            .HasDatabaseName("IX_TestRuns_Cycle_Result");

        builder.HasIndex(r => new { r.AssigneeId, r.Result })
            .HasDatabaseName("IX_TestRuns_Assignee");

        builder.HasIndex(r => r.TestCaseId)
            .HasDatabaseName("IX_TestRuns_TestCase");

        builder.HasOne(r => r.Cycle)
            .WithMany(c => c.Runs)
            .HasForeignKey(r => r.CycleId)
            .HasConstraintName("FK_TestRuns_Cycle")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.TestCase)
            .WithMany()
            .HasForeignKey(r => r.TestCaseId)
            .HasConstraintName("FK_TestRuns_TestCase")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(r => r.Assignee)
            .WithMany()
            .HasForeignKey(r => r.AssigneeId)
            .HasConstraintName("FK_TestRuns_Assignee")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(r => r.ExecutedBy)
            .WithMany()
            .HasForeignKey(r => r.ExecutedById)
            .HasConstraintName("FK_TestRuns_ExecutedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class TestRunDefectConfiguration : IEntityTypeConfiguration<TestRunDefect>
{
    public void Configure(EntityTypeBuilder<TestRunDefect> builder)
    {
        builder.ToTable("TestRunDefects");

        builder.HasKey(d => new { d.RunId, d.IssueId })
            .HasName("PK_TestRunDefects");

        builder.Property(d => d.RunId).HasColumnName("run_id");
        builder.Property(d => d.IssueId).HasColumnName("issue_id");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(d => d.IssueId)
            .HasDatabaseName("IX_TestRunDefects_Issue");

        builder.HasOne(d => d.Run)
            .WithMany(r => r.Defects)
            .HasForeignKey(d => d.RunId)
            .HasConstraintName("FK_TestRunDefects_Run")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Issue)
            .WithMany()
            .HasForeignKey(d => d.IssueId)
            .HasConstraintName("FK_TestRunDefects_Issue")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class TestRunHistoryConfiguration : IEntityTypeConfiguration<TestRunHistory>
{
    public void Configure(EntityTypeBuilder<TestRunHistory> builder)
    {
        builder.ToTable("TestRunHistory", t =>
        {
            t.HasCheckConstraint("CK_TestRunHistory_Result", "[result] IN ('PASS','FAIL','BLOCKED')");
        });

        builder.HasKey(h => h.Id).HasName("PK_TestRunHistory");
        builder.Property(h => h.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(h => h.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(h => h.TestCaseId).HasColumnName("test_case_id").IsRequired();
        builder.Property(h => h.Result).HasColumnName("result").IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(h => h.Note).HasColumnName("note").HasMaxLength(2000);
        builder.Property(h => h.DefectIssueId).HasColumnName("defect_issue_id");
        builder.Property(h => h.ExecutedById).HasColumnName("executed_by_id").IsRequired();
        builder.Property(h => h.ExecutedAt).HasColumnName("executed_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(h => new { h.TestCaseId, h.ExecutedAt })
            .HasDatabaseName("IX_TestRunHistory_TestCase_Time");

        builder.HasIndex(h => h.RunId)
            .HasDatabaseName("IX_TestRunHistory_Run");

        builder.HasOne(h => h.Run)
            .WithMany(r => r.History)
            .HasForeignKey(h => h.RunId)
            .HasConstraintName("FK_TestRunHistory_Run")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.TestCase)
            .WithMany()
            .HasForeignKey(h => h.TestCaseId)
            .HasConstraintName("FK_TestRunHistory_TestCase")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(h => h.DefectIssue)
            .WithMany()
            .HasForeignKey(h => h.DefectIssueId)
            .HasConstraintName("FK_TestRunHistory_DefectIssue")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(h => h.ExecutedBy)
            .WithMany()
            .HasForeignKey(h => h.ExecutedById)
            .HasConstraintName("FK_TestRunHistory_ExecutedBy")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
