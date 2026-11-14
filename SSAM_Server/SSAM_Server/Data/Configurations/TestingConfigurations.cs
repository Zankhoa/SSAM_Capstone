using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class TestPlanConfiguration : IEntityTypeConfiguration<TestPlan>
{
    public void Configure(EntityTypeBuilder<TestPlan> builder)
    {
        builder.ToTable("TestPlans", t =>
        {
            t.HasCheckConstraint("CK_TestPlans_Status", "[status] IN (N'OPEN', N'HANDED_OVER')");
            t.HasCheckConstraint("CK_TestPlans_Layout_Json", "[sheet_layout] IS NULL OR ISJSON([sheet_layout]) = 1");
        });

        builder.HasKey(x => x.Id).HasName("PK_TestPlans");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(TestPlanStatuses.Open)
            .IsRequired();

        builder.Property(x => x.HandoverById)
            .HasColumnName("handover_by_id");

        builder.Property(x => x.HandoverAt)
            .HasColumnName("handover_at")
            .HasColumnType("datetime2(3)");

        builder.Property(x => x.HandoverNote)
            .HasColumnName("handover_note")
            .HasMaxLength(1000);

        builder.Property(x => x.SheetLayout)
            .HasColumnName("sheet_layout")
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_TestPlans_Project");

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestPlans_Project");

        builder.HasOne(x => x.HandoverBy)
            .WithMany()
            .HasForeignKey(x => x.HandoverById)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestPlans_HandoverBy");

        builder.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestPlans_CreatedBy");
    }
}

public class TestEnvironmentConfiguration : IEntityTypeConfiguration<TestEnvironment>
{
    public void Configure(EntityTypeBuilder<TestEnvironment> builder)
    {
        builder.ToTable("TestEnvironments");

        builder.HasKey(x => x.Id).HasName("PK_TestEnvironments");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Url)
            .HasColumnName("url")
            .HasMaxLength(500)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(300)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(x => x.Position)
            .HasColumnName("position")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => new { x.PlanId, x.Position })
            .HasDatabaseName("IX_TestEnvironments_Plan");

        builder.HasOne(x => x.Plan)
            .WithMany(p => p.Environments)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestEnvironments_Plan");
    }
}

public class TestBuildConfiguration : IEntityTypeConfiguration<TestBuild>
{
    public void Configure(EntityTypeBuilder<TestBuild> builder)
    {
        builder.ToTable("TestBuilds", t =>
        {
            t.HasCheckConstraint("CK_TestBuilds_FixedIssues_Json", "ISJSON([fixed_issue_ids]) = 1");
        });

        builder.HasKey(x => x.Id).HasName("PK_TestBuilds");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.EnvId)
            .HasColumnName("env_id")
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.DeployedAt)
            .HasColumnName("deployed_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.DeployedById)
            .HasColumnName("deployed_by_id")
            .IsRequired();

        builder.Property(x => x.FixedIssueIds)
            .HasColumnName("fixed_issue_ids")
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.HasIndex(x => new { x.EnvId, x.DeployedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_TestBuilds_Env");

        builder.HasOne(x => x.Environment)
            .WithMany(e => e.Builds)
            .HasForeignKey(x => x.EnvId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestBuilds_Env");

        builder.HasOne(x => x.DeployedBy)
            .WithMany()
            .HasForeignKey(x => x.DeployedById)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestBuilds_DeployedBy");
    }
}

public class TestModuleConfiguration : IEntityTypeConfiguration<TestModule>
{
    public void Configure(EntityTypeBuilder<TestModule> builder)
    {
        builder.ToTable("TestModules");

        builder.HasKey(x => x.Id).HasName("PK_TestModules");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id")
            .IsRequired();

        builder.Property(x => x.DocumentId)
            .HasColumnName("document_id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ShortCode)
            .HasColumnName("short_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Position)
            .HasColumnName("position")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.CaseSeq)
            .HasColumnName("case_seq")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(x => new { x.PlanId, x.Position })
            .HasDatabaseName("IX_TestModules_Plan");

        builder.HasIndex(x => x.DocumentId)
            .HasDatabaseName("IX_TestModules_Document")
            .HasFilter("[document_id] IS NOT NULL");

        builder.HasOne(x => x.Plan)
            .WithMany(p => p.Modules)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestModules_Plan");

        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestModules_Document");
    }
}

public class TestSectionConfiguration : IEntityTypeConfiguration<TestSection>
{
    public void Configure(EntityTypeBuilder<TestSection> builder)
    {
        builder.ToTable("TestSections");

        builder.HasKey(x => x.Id).HasName("PK_TestSections");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.ModuleId)
            .HasColumnName("module_id")
            .IsRequired();

        builder.Property(x => x.ParentId)
            .HasColumnName("parent_id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.AnchorId)
            .HasColumnName("anchor_id")
            .HasMaxLength(100);

        builder.Property(x => x.Position)
            .HasColumnName("position")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(x => new { x.ModuleId, x.ParentId, x.Position })
            .HasDatabaseName("IX_TestSections_Module");

        builder.HasOne(x => x.Module)
            .WithMany(m => m.Sections)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestSections_Module");

        builder.HasOne(x => x.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestSections_Parent");
    }
}

public class TestAssignmentConfiguration : IEntityTypeConfiguration<TestAssignment>
{
    public void Configure(EntityTypeBuilder<TestAssignment> builder)
    {
        builder.ToTable("TestAssignments");

        builder.HasKey(x => new { x.ModuleId, x.EnvId }).HasName("PK_TestAssignments");

        builder.Property(x => x.ModuleId)
            .HasColumnName("module_id");

        builder.Property(x => x.EnvId)
            .HasColumnName("env_id");

        builder.Property(x => x.AssigneeId)
            .HasColumnName("assignee_id")
            .IsRequired();

        builder.HasIndex(x => x.AssigneeId)
            .HasDatabaseName("IX_TestAssignments_Assignee");

        builder.HasOne(x => x.Module)
            .WithMany(m => m.Assignments)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestAssignments_Module");

        builder.HasOne(x => x.Environment)
            .WithMany(e => e.Assignments)
            .HasForeignKey(x => x.EnvId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestAssignments_Env");

        builder.HasOne(x => x.Assignee)
            .WithMany()
            .HasForeignKey(x => x.AssigneeId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestAssignments_Assignee");
    }
}

public class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.ToTable("TestCases", t =>
        {
            t.HasCheckConstraint("CK_TestCases_Steps_Json", "ISJSON([steps]) = 1");
            t.HasCheckConstraint("CK_TestCases_Custom_Json", "ISJSON([custom_fields]) = 1");
            t.HasCheckConstraint("CK_TestCases_Priority", "[priority] IN (N'P0', N'P1', N'P2', N'P3')");
            t.HasCheckConstraint("CK_TestCases_Category", "[category] IN (N'FUNCTIONAL', N'BOUNDARY', N'NEGATIVE', N'UI', N'SECURITY', N'PERFORMANCE')");
            t.HasCheckConstraint("CK_TestCases_Status", "[status] IN (N'ACTIVE', N'DELETED')");
        });

        builder.HasKey(x => x.Id).HasName("PK_TestCases");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id")
            .IsRequired();

        builder.Property(x => x.ModuleId)
            .HasColumnName("module_id")
            .IsRequired();

        builder.Property(x => x.SectionId)
            .HasColumnName("section_id")
            .IsRequired();

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(x => x.Preconditions)
            .HasColumnName("preconditions")
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(x => x.Steps)
            .HasColumnName("steps")
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.Actual)
            .HasColumnName("actual")
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(x => x.AiSuggestedReqs)
            .HasColumnName("ai_suggested_reqs")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .HasMaxLength(10)
            .HasDefaultValue(TestCasePriorities.P1)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(20)
            .HasDefaultValue(TestCaseCategories.Functional)
            .IsRequired();

        builder.Property(x => x.Browser)
            .HasColumnName("browser")
            .HasMaxLength(100)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(x => x.CustomFields)
            .HasColumnName("custom_fields")
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(TestCaseStatuses.Active)
            .IsRequired();

        builder.Property(x => x.Position)
            .HasColumnName("position")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.BugIssueId)
            .HasColumnName("bug_issue_id");

        builder.Property(x => x.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => new { x.PlanId, x.ModuleId, x.SectionId, x.Position })
            .HasDatabaseName("IX_TestCases_Plan_Module")
            .HasFilter("[status] = N'ACTIVE'");

        builder.HasIndex(x => x.BugIssueId)
            .HasDatabaseName("IX_TestCases_BugIssue")
            .HasFilter("[bug_issue_id] IS NOT NULL AND [status] = N'ACTIVE'");

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestCases_Project");

        builder.HasOne(x => x.Plan)
            .WithMany(p => p.TestCases)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestCases_Plan");

        builder.HasOne(x => x.Module)
            .WithMany(m => m.TestCases)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestCases_Module");

        builder.HasOne(x => x.Section)
            .WithMany(s => s.TestCases)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestCases_Section");

        builder.HasOne(x => x.BugIssue)
            .WithMany()
            .HasForeignKey(x => x.BugIssueId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestCases_BugIssue");

        builder.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestCases_CreatedBy");
    }
}

public class TestCaseRequirementLinkConfiguration : IEntityTypeConfiguration<TestCaseRequirementLink>
{
    public void Configure(EntityTypeBuilder<TestCaseRequirementLink> builder)
    {
        builder.ToTable("TestCaseRequirementLinks");

        builder.HasKey(x => new { x.TestCaseId, x.DocumentId, x.ReqUid })
            .HasName("PK_TestCaseRequirementLinks");

        builder.Property(x => x.TestCaseId)
            .HasColumnName("test_case_id");

        builder.Property(x => x.DocumentId)
            .HasColumnName("document_id");

        builder.Property(x => x.ReqUid)
            .HasColumnName("req_uid")
            .HasMaxLength(40);

        builder.Property(x => x.ReqHash)
            .HasColumnName("req_hash")
            .HasMaxLength(32)
            .HasDefaultValue("")
            .IsRequired();

        builder.Property(x => x.ChangedVersionLabel)
            .HasColumnName("changed_version_label")
            .HasMaxLength(30);

        builder.Property(x => x.LinkedAt)
            .HasColumnName("linked_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => new { x.DocumentId, x.ReqUid })
            .HasDatabaseName("IX_TestCaseReqLinks_Req");

        builder.HasOne(x => x.TestCase)
            .WithMany(tc => tc.RequirementLinks)
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestCaseReqLinks_TestCase");

        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestCaseReqLinks_Document");
    }
}

public class TestResultConfiguration : IEntityTypeConfiguration<TestResult>
{
    public void Configure(EntityTypeBuilder<TestResult> builder)
    {
        builder.ToTable("TestResults", t =>
        {
            t.HasCheckConstraint("CK_TestResults_Result", "[result] IN (N'PASS', N'FAIL', N'NA', N'UNTEST')");
        });

        builder.HasKey(x => x.Id).HasName("PK_TestResults");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.TestCaseId)
            .HasColumnName("test_case_id")
            .IsRequired();

        builder.Property(x => x.EnvId)
            .HasColumnName("env_id")
            .IsRequired();

        builder.Property(x => x.Build)
            .HasColumnName("build")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Result)
            .HasColumnName("result")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.ById)
            .HasColumnName("by_id")
            .IsRequired();

        builder.Property(x => x.At)
            .HasColumnName("at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.BehalfOfId)
            .HasColumnName("behalf_of_id");

        builder.HasIndex(x => new { x.TestCaseId, x.EnvId, x.At, x.Id })
            .IsDescending(false, false, true, true)
            .HasDatabaseName("IX_TestResults_Case_Env");

        builder.HasOne(x => x.TestCase)
            .WithMany(tc => tc.Results)
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestResults_Case");

        builder.HasOne(x => x.Environment)
            .WithMany(e => e.Results)
            .HasForeignKey(x => x.EnvId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestResults_Env");

        builder.HasOne(x => x.By)
            .WithMany()
            .HasForeignKey(x => x.ById)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestResults_By");

        builder.HasOne(x => x.BehalfOf)
            .WithMany()
            .HasForeignKey(x => x.BehalfOfId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestResults_BehalfOf");
    }
}

public class TestRetestFlagConfiguration : IEntityTypeConfiguration<TestRetestFlag>
{
    public void Configure(EntityTypeBuilder<TestRetestFlag> builder)
    {
        builder.ToTable("TestRetestFlags", t =>
        {
            t.HasCheckConstraint("CK_TestRetestFlags_Reason", "[reason] IN (N'BUG_FIXED', N'REQ_CHANGED', N'MANUAL')");
        });

        builder.HasKey(x => new { x.TestCaseId, x.EnvId })
            .HasName("PK_TestRetestFlags");

        builder.Property(x => x.TestCaseId)
            .HasColumnName("test_case_id");

        builder.Property(x => x.EnvId)
            .HasColumnName("env_id");

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.BugIssueId)
            .HasColumnName("bug_issue_id");

        builder.Property(x => x.Build)
            .HasColumnName("build")
            .HasMaxLength(80);

        builder.Property(x => x.ReqKey)
            .HasColumnName("req_key")
            .HasMaxLength(120);

        builder.Property(x => x.At)
            .HasColumnName("at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.TestCase)
            .WithMany(tc => tc.RetestFlags)
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestRetestFlags_Case");

        builder.HasOne(x => x.Environment)
            .WithMany(e => e.RetestFlags)
            .HasForeignKey(x => x.EnvId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestRetestFlags_Env");

        builder.HasOne(x => x.BugIssue)
            .WithMany()
            .HasForeignKey(x => x.BugIssueId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestRetestFlags_Bug");
    }
}

public class TestActivityConfiguration : IEntityTypeConfiguration<TestActivity>
{
    public void Configure(EntityTypeBuilder<TestActivity> builder)
    {
        builder.ToTable("TestActivities", t =>
        {
            t.HasCheckConstraint("CK_TestActivities_Type", "[type] IN (N'RESULT', N'DEPLOY', N'BUILD', N'ASSIGN', N'REQ', N'HANDOVER', N'CASE', N'TESTCASE', N'BUG', N'SETUP')");
        });

        builder.HasKey(x => x.Id).HasName("PK_TestActivities");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.At)
            .HasColumnName("at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.ActorId)
            .HasColumnName("actor_id");

        builder.Property(x => x.EnvId)
            .HasColumnName("env_id");

        builder.Property(x => x.ModuleId)
            .HasColumnName("module_id");

        builder.Property(x => x.TestCaseId)
            .HasColumnName("test_case_id");

        builder.Property(x => x.FromStatus)
            .HasColumnName("from_status")
            .HasMaxLength(20);

        builder.Property(x => x.ToResult)
            .HasColumnName("to_result")
            .HasMaxLength(20);

        builder.Property(x => x.Build)
            .HasColumnName("build")
            .HasMaxLength(80);

        builder.Property(x => x.BehalfOfId)
            .HasColumnName("behalf_of_id");

        builder.Property(x => x.Text)
            .HasColumnName("text")
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.PlanId, x.At, x.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("IX_TestActivities_Plan_At");

        builder.HasOne(x => x.Plan)
            .WithMany(p => p.Activities)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestActivities_Plan");

        builder.HasOne(x => x.Actor)
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestActivities_Actor");

        builder.HasOne(x => x.Environment)
            .WithMany()
            .HasForeignKey(x => x.EnvId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestActivities_Env");

        builder.HasOne(x => x.Module)
            .WithMany()
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestActivities_Module");

        builder.HasOne(x => x.TestCase)
            .WithMany()
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestActivities_Case");

        builder.HasOne(x => x.BehalfOf)
            .WithMany()
            .HasForeignKey(x => x.BehalfOfId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestActivities_BehalfOf");
    }
}
