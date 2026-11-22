using System.Text.Json;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

/// <summary>Shared seeding / guard helpers for the plan, environment, module and section test classes.</summary>
internal static class TestingSeedHelpersPlan
{
    public static TestPlan AddPlan(AppDbContext db, long projectId = 1, string name = "Plan A",
        string status = TestPlanStatuses.Open, DateOnly? start = null, DateOnly? end = null)
    {
        var plan = new TestPlan
        {
            ProjectId = projectId,
            Name = name,
            StartDate = start ?? new DateOnly(2026, 1, 1),
            EndDate = end ?? new DateOnly(2026, 12, 31),
            Status = status,
            CreatedById = 1,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.TestPlans.Add(plan);
        db.SaveChanges();
        return plan;
    }

    public static TestEnvironment AddEnv(AppDbContext db, long planId, string name = "SIT", int position = 1,
        bool isDeleted = false, string url = "", string note = "")
    {
        var env = new TestEnvironment
        {
            PlanId = planId, Name = name, Url = url, Note = note, Position = position, IsDeleted = isDeleted,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.TestEnvironments.Add(env);
        db.SaveChanges();
        return env;
    }

    public static TestModule AddModule(AppDbContext db, long planId, string name = "Module", string shortCode = "MOD",
        int position = 1, int caseSeq = 0, long? documentId = null)
    {
        var m = new TestModule
        {
            PlanId = planId, Name = name, ShortCode = shortCode, Position = position, CaseSeq = caseSeq, DocumentId = documentId
        };
        db.TestModules.Add(m);
        db.SaveChanges();
        return m;
    }

    public static TestSection AddSection(AppDbContext db, long moduleId, string code = "1.", string title = "Section",
        int position = 1, long? parentId = null, string? anchorId = null)
    {
        var s = new TestSection
        {
            ModuleId = moduleId, ParentId = parentId, Code = code, Title = title, Position = position, AnchorId = anchorId
        };
        db.TestSections.Add(s);
        db.SaveChanges();
        return s;
    }

    public static TestCase AddCase(AppDbContext db, TestPlan plan, TestModule module, TestSection section,
        string code = "TC_1", string title = "Case", string status = TestCaseStatuses.Active, int position = 1,
        long? projectId = null)
    {
        var tc = new TestCase
        {
            ProjectId = projectId ?? plan.ProjectId,
            PlanId = plan.Id,
            ModuleId = module.Id,
            SectionId = section.Id,
            Code = code,
            Title = title,
            Status = status,
            Position = position,
            CreatedById = 1,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.TestCases.Add(tc);
        db.SaveChanges();
        return tc;
    }

    public static string HeadingJson(params (string Id, string Title, int Level, bool IsReq, string? ReqUid)[] headings)
    {
        var content = headings.Select(h =>
        {
            var attrs = new Dictionary<string, object?>();
            if (h.IsReq)
            {
                attrs["req"] = true;
                attrs["reqUid"] = h.ReqUid;
            }
            else
            {
                attrs["id"] = h.Id;
                attrs["level"] = h.Level;
            }
            return new Dictionary<string, object?>
            {
                ["type"] = "heading",
                ["attrs"] = attrs,
                ["content"] = new[] { new Dictionary<string, object?> { ["type"] = "text", ["text"] = h.Title } }
            };
        }).ToList();
        return JsonSerializer.Serialize(new Dictionary<string, object?> { ["type"] = "doc", ["content"] = content });
    }

    public static DocumentNode AddDocument(AppDbContext db, long projectId = 1, string title = "Doc", string? code = "SRS-1",
        string? content = null, bool isDeleted = false, bool useBaseline = false)
    {
        var doc = new DocumentNode { ProjectId = projectId, Title = title, Code = code, IsDeleted = isDeleted, CreatedById = 1 };
        db.DocumentNodes.Add(doc);
        db.SaveChanges();
        if (content != null)
        {
            var ver = new DocumentVersion { DocumentId = doc.Id, Content = content, UpdatedById = 1, AuthorId = 1 };
            db.DocumentVersions.Add(ver);
            db.SaveChanges();
            if (useBaseline) doc.BaselineVersionId = ver.Id; else doc.CurrentVersionId = ver.Id;
            db.SaveChanges();
        }
        return doc;
    }

    public static void AllowGuard(Mock<IProjectAccessGuard> guard, long projectId, string permission) =>
        guard.Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

    public static void DenyGuard(Mock<IProjectAccessGuard> guard, long projectId, string permission) =>
        guard.Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.ForbiddenPermission(permission));

    public static void UnauthenticatedGuard(Mock<IProjectAccessGuard> guard, long projectId, string permission) =>
        guard.Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Unauthorized("Not authenticated"));
}
