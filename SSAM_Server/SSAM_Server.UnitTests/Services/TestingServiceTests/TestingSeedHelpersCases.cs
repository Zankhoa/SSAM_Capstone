using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

/// <summary>Plan / module / section tuple produced by <see cref="TestingSeedHelpersCases.AddPlanTree"/>.</summary>
public sealed record CasesSeedTree(TestPlan Plan, TestModule Module, TestSection Section);

/// <summary>Seeding helpers used by the test-case related TestingService tests (Create/Update/Bulk/Delete/ReportBug/LinkBug).</summary>
public static class TestingSeedHelpersCases
{
    public const long ProjectId = 100;
    public static readonly DateTime OldDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static User AddUser(AppDbContext db, long id, string fullName = "Tester", string? email = null)
    {
        var user = new User { Id = id, FullName = fullName, Email = email ?? $"user{id}@ssam.vn", AccountId = id };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static TestPlan AddPlan(AppDbContext db, long projectId = ProjectId, string status = TestPlanStatuses.Open, string name = "Plan")
    {
        var plan = new TestPlan
        {
            ProjectId = projectId,
            Name = name,
            Status = status,
            CreatedById = 1,
            CreatedAt = OldDate,
            UpdatedAt = OldDate
        };
        db.TestPlans.Add(plan);
        db.SaveChanges();
        return plan;
    }

    public static TestModule AddModule(AppDbContext db, TestPlan plan, string shortCode = "LOG", string name = "Login", int caseSeq = 0)
    {
        var module = new TestModule { PlanId = plan.Id, Name = name, ShortCode = shortCode, CaseSeq = caseSeq };
        db.TestModules.Add(module);
        db.SaveChanges();
        return module;
    }

    public static TestSection AddSection(AppDbContext db, TestModule module, string title = "Login", long? parentId = null, int position = 1)
    {
        var section = new TestSection
        {
            ModuleId = module.Id,
            ParentId = parentId,
            Code = position.ToString(),
            Title = title,
            Position = position
        };
        db.TestSections.Add(section);
        db.SaveChanges();
        return section;
    }

    /// <summary>Creates a plan with one module (LOG) and one top-level section ("Login").</summary>
    public static CasesSeedTree AddPlanTree(AppDbContext db, long projectId = ProjectId, string planStatus = TestPlanStatuses.Open)
    {
        var plan = AddPlan(db, projectId, planStatus);
        var module = AddModule(db, plan);
        var section = AddSection(db, module);
        return new CasesSeedTree(plan, module, section);
    }

    public static TestCase AddCase(
        AppDbContext db,
        CasesSeedTree tree,
        string code = "TC_1",
        string title = "Existing case",
        int position = 1,
        string status = TestCaseStatuses.Active,
        TestSection? section = null,
        long? bugIssueId = null)
    {
        var sec = section ?? tree.Section;
        var tc = new TestCase
        {
            ProjectId = tree.Plan.ProjectId,
            PlanId = tree.Plan.Id,
            ModuleId = sec.ModuleId,
            SectionId = sec.Id,
            Code = code,
            Title = title,
            Preconditions = "pre",
            Steps = "[]",
            Actual = "actual",
            Priority = TestCasePriorities.P1,
            Category = "Functional",
            Browser = "Chrome",
            CustomFields = "{}",
            CellStyles = "{}",
            Status = status,
            Position = position,
            BugIssueId = bugIssueId,
            CreatedById = 1,
            CreatedAt = OldDate,
            UpdatedAt = OldDate
        };
        db.TestCases.Add(tc);
        db.SaveChanges();
        return tc;
    }

    /// <summary>Adds a document with one version containing one requirement item (REQ uid / hash).</summary>
    public static DocumentNode AddDocumentWithRequirement(
        AppDbContext db,
        long projectId = ProjectId,
        string docCode = "SRS-1",
        string reqUid = "REQ-001",
        string hash = "hash-1",
        string reqTitle = "Login requirement",
        bool isDeleted = false,
        bool useBaseline = false)
    {
        var doc = new DocumentNode
        {
            ProjectId = projectId,
            Code = docCode,
            Title = "Software Requirements",
            Kind = DocumentNodeKinds.Page,
            IsDeleted = isDeleted,
            CreatedById = 1
        };
        db.DocumentNodes.Add(doc);
        db.SaveChanges();

        var version = new DocumentVersion { DocumentId = doc.Id, Label = "1.0", AuthorId = 1, UpdatedById = 1 };
        db.DocumentVersions.Add(version);
        db.SaveChanges();

        db.RequirementItems.Add(new RequirementItem
        {
            DocumentId = doc.Id,
            VersionId = version.Id,
            ReqUid = reqUid,
            AnchorId = "anchor-" + reqUid,
            Title = reqTitle,
            ContentHash = hash
        });

        if (useBaseline) doc.BaselineVersionId = version.Id;
        else doc.CurrentVersionId = version.Id;
        db.SaveChanges();
        return doc;
    }

    /// <summary>Adds a document without any version (links to it get an empty hash).</summary>
    public static DocumentNode AddDocumentWithoutVersion(AppDbContext db, long projectId = ProjectId, string docCode = "SRS-2")
    {
        var doc = new DocumentNode { ProjectId = projectId, Code = docCode, Title = "Empty doc", CreatedById = 1 };
        db.DocumentNodes.Add(doc);
        db.SaveChanges();
        return doc;
    }

    public static Issue AddIssue(
        AppDbContext db,
        long projectId = ProjectId,
        string key = "BUG-1",
        string title = "Existing bug",
        bool isDeleted = false,
        string statusName = "To Do",
        string statusCategory = StatusCategories.Todo,
        string priority = IssuePriorities.High)
    {
        var status = new WorkflowStatus { ProjectId = projectId, PhaseId = 1, Name = statusName, Category = statusCategory };
        db.WorkflowStatuses.Add(status);
        db.SaveChanges();

        var issue = new Issue
        {
            ProjectId = projectId,
            Key = key,
            Title = title,
            Type = IssueTypes.Bug,
            StatusId = status.Id,
            Priority = priority,
            ReporterId = 1,
            IsDeleted = isDeleted
        };
        db.Issues.Add(issue);
        db.SaveChanges();
        return issue;
    }
}
