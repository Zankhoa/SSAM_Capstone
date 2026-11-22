using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class AddTestCasesFromRepositoryAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;
    private const long UserId = 7;
    private const string RepoName = "__REPOSITORY__";

    private sealed record Fixture(TestPlan Repo, TestModule RepoModule, TestSection RepoSection, TestPlan Target);

    private Fixture SetupAuthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(UserId);
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseManage);
        var repo = H.AddPlan(Db, ProjectId, RepoName);
        var module = H.AddModule(Db, repo.Id, "Login Module", "LGN", 1, caseSeq: 5);
        var section = H.AddSection(Db, module.Id, "1.", "Authentication", 1, anchorId: "auth");
        var target = H.AddPlan(Db, ProjectId, "Release plan");
        return new Fixture(repo, module, section, target);
    }

    private TestCase AddRepoCase(Fixture f, string title, int position = 1, string status = TestCaseStatuses.Active, long? projectId = null)
    {
        var tc = H.AddCase(Db, f.Repo, f.RepoModule, f.RepoSection, $"TC_{position}", title, status, position, projectId);
        tc.Preconditions = "pre";
        tc.Steps = "[{\"step\":\"s1\",\"expected\":\"e1\"}]";
        tc.Priority = TestCasePriorities.P0;
        tc.Category = "Functional";
        tc.Browser = "Chrome";
        tc.CustomFields = "{\"k\":\"v\"}";
        tc.AiSuggestedReqs = true;
        Db.SaveChanges();
        return tc;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Import one repository case into an empty plan -> 201, module/section/case cloned, activity logged
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_IntoEmptyPlan_CreatesModuleSectionAndClonedCase()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "Login works");

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        var dto = Assert.Single(result.Data.Items);
        Assert.Equal(f.Target.Id, dto.PlanId);
        Assert.Equal("LGN_1", dto.Code);
        Assert.Equal("LGN-1", dto.ModuleCode);
        Assert.Equal("Login works", dto.Title);
        Assert.Equal("pre", dto.Preconditions);
        Assert.Single(dto.Steps);
        Assert.Null(dto.Actual);
        Assert.True(dto.AiSuggestedReqs);
        Assert.Equal(TestCasePriorities.P0, dto.Priority);
        Assert.Equal("Functional", dto.Category);
        Assert.Equal("Chrome", dto.Browser);
        Assert.Equal(TestCaseStatuses.Active, dto.Status);
        Assert.Equal(1, dto.Position);
        Assert.Empty(dto.Results);
        Assert.Empty(dto.Retest);
        Assert.Null(dto.Bug);

        var module = await Db.TestModules.SingleAsync(m => m.PlanId == f.Target.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Login Module", module.Name);
        Assert.Equal("LGN", module.ShortCode);
        Assert.Equal(1, module.Position);
        Assert.Equal(1, module.CaseSeq);
        var section = await Db.TestSections.SingleAsync(s => s.ModuleId == module.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Authentication", section.Title);
        Assert.Equal("1.", section.Code);
        Assert.Equal("auth", section.AnchorId);
        Assert.Equal(1, section.Position);

        var clone = await Db.TestCases.SingleAsync(c => c.PlanId == f.Target.Id, TestContext.Current.CancellationToken);
        Assert.Equal(dto.TestCaseId, clone.Id);
        Assert.Equal(ProjectId, clone.ProjectId);
        Assert.Equal(module.Id, clone.ModuleId);
        Assert.Equal(section.Id, clone.SectionId);
        Assert.Equal("LGN_1", clone.Code);
        Assert.Equal("Login works", clone.Title);
        Assert.Equal("pre", clone.Preconditions);
        Assert.Equal(src.Steps, clone.Steps);
        Assert.Equal(string.Empty, clone.Actual);
        Assert.True(clone.AiSuggestedReqs);
        Assert.Equal(TestCasePriorities.P0, clone.Priority);
        Assert.Equal("Functional", clone.Category);
        Assert.Equal("Chrome", clone.Browser);
        Assert.Equal("{\"k\":\"v\"}", clone.CustomFields);
        Assert.Equal(TestCaseStatuses.Active, clone.Status);
        Assert.Equal(1, clone.Position);
        Assert.Equal(UserId, clone.CreatedById);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, clone.CreatedAt);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, clone.UpdatedAt);

        var activity = await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(f.Target.Id, activity.PlanId);
        Assert.Equal(TestActivityTypes.TestCase, activity.Type);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Đã lấy 1 testcase từ Kho", activity.Text);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, activity.At);

        // source repository case untouched
        var srcSaved = await Db.TestCases.SingleAsync(c => c.Id == src.Id, TestContext.Current.CancellationToken);
        Assert.Equal(f.Repo.Id, srcSaved.PlanId);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseManage, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Import several cases from one module -> codes increment, positions ordered by source position, activity counts them
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WithMultipleCases_OrdersBySourcePositionAndNumbersCodes()
    {
        var f = SetupAuthorized();
        var c2 = AddRepoCase(f, "Second", position: 2);
        var c1 = AddRepoCase(f, "First", position: 1);
        var c3 = AddRepoCase(f, "Third", position: 3);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { c3.Id, c2.Id, c1.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "First", "Second", "Third" }, result.Data!.Items.Select(i => i.Title));
        Assert.Equal(new[] { "LGN_1", "LGN_2", "LGN_3" }, result.Data.Items.Select(i => i.Code));
        Assert.Equal(new[] { 1, 2, 3 }, result.Data.Items.Select(i => i.Position));
        Assert.Equal(new[] { "LGN-1", "LGN-2", "LGN-3" }, result.Data.Items.Select(i => i.ModuleCode));
        Assert.Equal(1, await Db.TestModules.CountAsync(m => m.PlanId == f.Target.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestSections.CountAsync(s => s.Module.PlanId == f.Target.Id, TestContext.Current.CancellationToken));
        Assert.Equal(3, (await Db.TestModules.SingleAsync(m => m.PlanId == f.Target.Id, TestContext.Current.CancellationToken)).CaseSeq);
        Assert.Equal("Đã lấy 3 testcase từ Kho", (await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken)).Text);
    }

    // UTC03: Duplicate ids in the request are collapsed -> case imported once
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WithDuplicateIds_ImportsOnce()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "Only");

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id, src.Id, src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(1, await Db.TestCases.CountAsync(c => c.PlanId == f.Target.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Đã lấy 1 testcase từ Kho", (await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken)).Text);
    }

    // UTC04: Target plan already has a module with the same ShortCode (case-insensitive) and a section with the same title -> reuses them and continues numbering
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenTargetHasMatchingModuleAndSection_ReusesThem()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "Reuse");
        var existingModule = H.AddModule(Db, f.Target.Id, "Different name", "lgn", 4, caseSeq: 2);
        var existingSection = H.AddSection(Db, existingModule.Id, "1.", "AUTHENTICATION", 1);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal("lgn_3", dto.Code);
        Assert.Equal(existingModule.Id, dto.ModuleId);
        Assert.Equal(existingSection.Id, dto.SectionId);
        Assert.Equal(3, dto.Position);
        Assert.Equal(1, await Db.TestModules.CountAsync(m => m.PlanId == f.Target.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestSections.CountAsync(s => s.ModuleId == existingModule.Id, TestContext.Current.CancellationToken));
        Db.ChangeTracker.Clear();
        Assert.Equal(3, (await Db.TestModules.SingleAsync(m => m.Id == existingModule.Id, TestContext.Current.CancellationToken)).CaseSeq);
    }

    // UTC05: Target plan has a module with the same Name (different code) -> matched by name
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenTargetModuleMatchesByName_ReusesIt()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "ByName");
        var existingModule = H.AddModule(Db, f.Target.Id, "login module", "OTHER", 1);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingModule.Id, result.Data!.Items[0].ModuleId);
        Assert.Equal("OTHER_1", result.Data.Items[0].Code);
        Assert.Equal(1, await Db.TestModules.CountAsync(m => m.PlanId == f.Target.Id, TestContext.Current.CancellationToken));
    }

    // UTC06: Matching module exists but has no section with that title -> a new section is appended after the last section position
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenSectionMissingInExistingModule_AppendsNewSection()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "NewSec");
        var existingModule = H.AddModule(Db, f.Target.Id, "Login Module", "LGN", 1);
        H.AddSection(Db, existingModule.Id, "1.", "Other section", 4);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var newSection = await Db.TestSections.SingleAsync(s => s.Id == result.Data!.Items[0].SectionId, TestContext.Current.CancellationToken);
        Assert.Equal("Authentication", newSection.Title);
        Assert.Equal("1.", newSection.Code);
        Assert.Equal("auth", newSection.AnchorId);
        Assert.Equal(5, newSection.Position);
        Assert.Equal(2, await Db.TestSections.CountAsync(s => s.ModuleId == existingModule.Id, TestContext.Current.CancellationToken));
    }

    // UTC07: Cases from two different repository modules -> two modules created with consecutive positions after existing ones
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WithCasesFromTwoModules_CreatesTwoTargetModules()
    {
        var f = SetupAuthorized();
        var src1 = AddRepoCase(f, "A");
        var module2 = H.AddModule(Db, f.Repo.Id, "Payment", "PAY", 2);
        var section2 = H.AddSection(Db, module2.Id, "1.", "Checkout", 1);
        var src2 = H.AddCase(Db, f.Repo, module2, section2, "TC_1", "B", position: 2);
        H.AddModule(Db, f.Target.Id, "Existing", "EXI", 6);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src1.Id, src2.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Items.Count);
        var modules = await Db.TestModules.Where(m => m.PlanId == f.Target.Id).OrderBy(m => m.Position).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "EXI", "LGN", "PAY" }, modules.Select(m => m.ShortCode));
        Assert.Equal(new[] { 6, 7, 8 }, modules.Select(m => m.Position));
        Assert.Equal(new[] { "LGN_1", "PAY_1" }, result.Data.Items.Select(i => i.Code));
    }

    // UTC08: Requirement links of the source case are copied to the clone with hash and changed label
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_CopiesRequirementLinks()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "WithReq");
        var doc = H.AddDocument(Db, ProjectId, "SRS", "SRS-1");
        Db.TestCaseRequirementLinks.Add(new TestCaseRequirementLink { TestCaseId = src.Id, DocumentId = doc.Id, ReqUid = "REQ-1", ReqHash = "hash1", ChangedVersionLabel = "2.0", LinkedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) });
        Db.TestCaseRequirementLinks.Add(new TestCaseRequirementLink { TestCaseId = src.Id, DocumentId = doc.Id, ReqUid = "REQ-2", ReqHash = "hash2", ChangedVersionLabel = null, LinkedAt = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc) });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var cloneId = result.Data!.Items[0].TestCaseId;
        var links = await Db.TestCaseRequirementLinks.Where(l => l.TestCaseId == cloneId).OrderBy(l => l.ReqUid).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, links.Count);
        Assert.Equal(doc.Id, links[0].DocumentId);
        Assert.Equal("REQ-1", links[0].ReqUid);
        Assert.Equal("hash1", links[0].ReqHash);
        Assert.Equal("2.0", links[0].ChangedVersionLabel);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, links[0].LinkedAt);
        Assert.Equal("REQ-2", links[1].ReqUid);
        Assert.Null(links[1].ChangedVersionLabel);
        Assert.Equal(2, await Db.TestCaseRequirementLinks.CountAsync(l => l.TestCaseId == src.Id, TestContext.Current.CancellationToken));
        var reqs = result.Data.Items[0].Requirements;
        Assert.Equal(new[] { "REQ-1", "REQ-2" }, reqs.Select(r => r.ReqUid).OrderBy(x => x));
        Assert.True(reqs.Single(r => r.ReqUid == "REQ-1").Changed);
        Assert.False(reqs.Single(r => r.ReqUid == "REQ-2").Changed);
    }

    // UTC09: Current user id is unknown (null) -> actor falls back to user 1
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenCurrentUserIdIsNull_UsesFallbackActorOne()
    {
        var f = SetupAuthorized();
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);
        var src = AddRepoCase(f, "Fallback");

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, (await Db.TestCases.SingleAsync(c => c.PlanId == f.Target.Id, TestContext.Current.CancellationToken)).CreatedById);
        Assert.Equal(1, (await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken)).ActorId);
    }

    // UTC10: Some ids are valid and some are missing/other-project/deleted -> only valid ones are imported
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WithMixedValidAndInvalidIds_ImportsOnlyValidOnes()
    {
        var f = SetupAuthorized();
        var valid = AddRepoCase(f, "Valid", 1);
        var deleted = AddRepoCase(f, "Deleted", 2, TestCaseStatuses.Deleted);
        var foreign = AddRepoCase(f, "Foreign", 3, projectId: 99);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { valid.Id, deleted.Id, foreign.Id, 9999 } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal("Valid", dto.Title);
        Assert.Equal(1, await Db.TestCases.CountAsync(c => c.PlanId == f.Target.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Đã lấy 1 testcase từ Kho", (await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken)).Text);
    }

    // UTC11: Source case may come from a normal (non-repository) plan of the same project -> still imported
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WithSourceFromAnotherNormalPlan_StillImports()
    {
        var f = SetupAuthorized();
        var otherPlan = H.AddPlan(Db, ProjectId, "Other plan");
        var m = H.AddModule(Db, otherPlan.Id, "Mod", "MD");
        var s = H.AddSection(Db, m.Id, "1.", "Sec");
        var src = H.AddCase(Db, otherPlan, m, s, "TC_1", "FromOther");

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("MD_1", result.Data!.Items[0].Code);
        Assert.Equal(f.Target.Id, result.Data.Items[0].PlanId);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC12: Plan does not exist -> 404 TEST_PLAN_NOT_FOUND, guard never consulted
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        var result = await TestingService.AddTestCasesFromRepositoryAsync(999,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { 1 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC13: Target plan is the repository itself -> 400 INVALID_PLAN, guard never consulted, nothing persisted
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenTargetIsRepositoryPlan_ReturnsInvalidPlan()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "X");

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Repo.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PLAN", result.ErrorCode);
        Assert.Equal(1, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC14: Caller is not authenticated -> 401, nothing persisted
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId, "Plan");
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseManage);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(plan.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { 1 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Empty(await Db.TestCases.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestModules.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC15: Caller lacks testcase.manage -> 403 FORBIDDEN, nothing persisted
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId, "Plan");
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseManage);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(plan.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { 1 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseManage, result.RequiredPermission);
        Assert.Empty(await Db.TestCases.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseManage, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC16: Target plan is already handed over -> 409 PLAN_HANDED_OVER, nothing persisted
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenPlanHandedOver_ReturnsConflict()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "X");
        var handed = H.AddPlan(Db, ProjectId, "Handed", TestPlanStatuses.HandedOver);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(handed.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("PLAN_HANDED_OVER", result.ErrorCode);
        Assert.Equal(1, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC17: TestCaseIds is empty or null -> 400 VALIDATION_ERROR on "testCaseIds"
    [Theory]
    [InlineData(false)] // UTC17a: empty list
    [InlineData(true)]  // UTC17b: null list
    public async Task AddTestCasesFromRepositoryAsync_WhenNoIdsProvided_ReturnsValidationError(bool useNull)
    {
        var f = SetupAuthorized();
        var request = new AddFromRepositoryRequest { TestCaseIds = useNull ? null! : new List<long>() };

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id, request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(new[] { "Pick at least 1 test case." }, result.Errors!["testCaseIds"]);
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC18: None of the ids match an active case of the project -> 404 TEST_CASE_NOT_FOUND, nothing persisted
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenNoCaseMatches_ReturnsNotFound()
    {
        var f = SetupAuthorized();
        var deleted = AddRepoCase(f, "Deleted", 1, TestCaseStatuses.Deleted);
        var foreign = AddRepoCase(f, "Foreign", 2, projectId: 99);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { deleted.Id, foreign.Id, 12345 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
        Assert.Equal(2, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestModules.Where(m => m.PlanId == f.Target.Id).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC19: Exactly one id (minimum valid list size) -> success with exactly one clone
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WithExactlyOneId_ImportsExactlyOneCase()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "Single");
        AddRepoCase(f, "Not requested", 2);

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(1, await Db.TestCases.CountAsync(c => c.PlanId == f.Target.Id, TestContext.Current.CancellationToken));
    }

    // UTC20: Plan id 0 -> 404 TEST_PLAN_NOT_FOUND
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenPlanIdIsZero_ReturnsNotFound()
    {
        var result = await TestingService.AddTestCasesFromRepositoryAsync(0,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { 1 } }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
    }

    // UTC21: Repository module has no matching module and no existing target modules (nextModPos boundary 0) -> first module gets position 1
    [Fact]
    public async Task AddTestCasesFromRepositoryAsync_WhenTargetHasNoModules_FirstModuleGetsPositionOne()
    {
        var f = SetupAuthorized();
        var src = AddRepoCase(f, "First");

        var result = await TestingService.AddTestCasesFromRepositoryAsync(f.Target.Id,
            new AddFromRepositoryRequest { TestCaseIds = new List<long> { src.Id } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, (await Db.TestModules.SingleAsync(m => m.PlanId == f.Target.Id, TestContext.Current.CancellationToken)).Position);
    }
}
