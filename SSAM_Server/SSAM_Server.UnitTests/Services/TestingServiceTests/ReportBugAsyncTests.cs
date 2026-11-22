using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using TSH = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersCases;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class ReportBugAsyncTests : TestingCasesTestBase
{
    private CreateIssueRequest? CapturedIssueRequest;

    private (CasesSeedTree Tree, Issue Issue) SetupValidPlanWithKanbanIssue(string planStatus = TestPlanStatuses.Open)
    {
        SetupCurrentUser(5);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestRunExecute);
        var tree = TSH.AddPlanTree(Db, TSH.ProjectId, planStatus);
        var issue = TSH.AddIssue(Db, TSH.ProjectId, "BUG-42", "Login crashes", statusName: "To Do", statusCategory: StatusCategories.Todo, priority: IssuePriorities.High);

        KanbanServiceMock
            .Setup(k => k.CreateIssueAsync(TSH.ProjectId, It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()))
            .Callback<long, CreateIssueRequest, CancellationToken>((_, req, _) => CapturedIssueRequest = req)
            .ReturnsAsync(ServiceResult<IssueDetailDto>.Created(new IssueDetailDto { IssueId = issue.Id, Key = issue.Key }));
        return (tree, issue);
    }

    private static ReportTestBugRequest Req(IEnumerable<long> ids, string title = "Bug title", string? priority = null, string? description = null, long envId = 0)
        => new() { TestCaseIds = ids.ToList(), Title = title, Priority = priority, Description = description, EnvId = envId };

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Report a bug for one case -> 201, Kanban issue requested, case linked to the issue, activity logged, DTO returned
    [Fact]
    public async Task ReportBugAsync_WithSingleCase_ReturnsCreatedAndLinksIssue()
    {
        var (tree, issue) = SetupValidPlanWithKanbanIssue();
        var tc = TSH.AddCase(Db, tree, "TC_1", "Login works", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { tc.Id }, "  Login crashes  ", "highest", "Steps to reproduce", envId: 3), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(issue.Id, result.Data.Issue.IssueId);
        Assert.Equal("BUG-42", result.Data.Issue.Key);
        var dto = Assert.Single(result.Data.Items);
        Assert.Equal(tc.Id, dto.TestCaseId);
        Assert.NotNull(dto.Bug);
        Assert.Equal(issue.Id, dto.Bug.IssueId);
        Assert.Equal("BUG-42", dto.Bug.Key);
        Assert.Equal("Login crashes", dto.Bug.Title);
        Assert.Equal("To Do", dto.Bug.StatusName);
        Assert.Equal(StatusCategories.Todo, dto.Bug.StatusCategory);

        Assert.NotNull(CapturedIssueRequest);
        Assert.Equal(IssueTypes.Bug, CapturedIssueRequest.Type);
        Assert.Equal("Login crashes", CapturedIssueRequest.Title);
        Assert.Equal("Steps to reproduce", CapturedIssueRequest.Description);
        Assert.Equal(IssuePriorities.Highest, CapturedIssueRequest.Priority);
        Assert.NotNull(CapturedIssueRequest.RequirementRefs);
        Assert.Empty(CapturedIssueRequest.RequirementRefs);

        var saved = await LoadCaseAsync(tc.Id);
        Assert.Equal(issue.Id, saved.BugIssueId);
        Assert.Equal(Now, saved.UpdatedAt);

        var activity = await Db.TestActivities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(tree.Plan.Id, activity.PlanId);
        Assert.Equal(TestActivityTypes.Bug, activity.Type);
        Assert.Equal(Now, activity.At);
        Assert.Equal(5, activity.ActorId);
        Assert.Equal(3, activity.EnvId);
        Assert.Equal(tree.Module.Id, activity.ModuleId);
        Assert.Equal(tc.Id, activity.TestCaseId);
        Assert.Equal("Created BUG-42 on Kanban", activity.Text);

        Assert.Equal(Now, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
        KanbanServiceMock.Verify(k => k.CreateIssueAsync(TSH.ProjectId, It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(TSH.ProjectId, PermissionKeys.TestRunExecute, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Several cases share one bug -> all linked, one activity per case, items returned in the requested-id order of DB
    [Fact]
    public async Task ReportBugAsync_WithMultipleCases_LinksAllToSameIssue()
    {
        var (tree, issue) = SetupValidPlanWithKanbanIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        var b = TSH.AddCase(Db, tree, "TC_2", "B", 2);
        var untouched = TSH.AddCase(Db, tree, "TC_3", "C", 3);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id, b.Id }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.All(result.Data.Items, i => Assert.Equal(issue.Id, i.Bug!.IssueId));
        Assert.Equal(issue.Id, (await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Equal(issue.Id, (await LoadCaseAsync(b.Id)).BugIssueId);
        Assert.Null((await LoadCaseAsync(untouched.Id)).BugIssueId);
        Assert.Equal(2, await Db.TestActivities.CountAsync(a2 => a2.Type == TestActivityTypes.Bug, TestContext.Current.CancellationToken));
        KanbanServiceMock.Verify(k => k.CreateIssueAsync(It.IsAny<long>(), It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC03: Blank title -> default "[Bug] <code>: <title>" of the first case
    [Theory]
    [InlineData("")]        // UTC03a: empty
    [InlineData("   ")]     // UTC03b: whitespace
    public async Task ReportBugAsync_WithBlankTitle_UsesDefaultTitleFromFirstCase(string title)
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var tc = TSH.AddCase(Db, tree, "TC_1", "Login works", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { tc.Id }, title), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("[Bug] TC_1: Login works", CapturedIssueRequest!.Title);
    }

    // UTC04: Priority mapping to Kanban issue priority
    [Theory]
    [InlineData("HIGHEST", IssuePriorities.Highest)]     // UTC04a
    [InlineData("highest", IssuePriorities.Highest)]     // UTC04b: case-insensitive
    [InlineData("  High  ", IssuePriorities.High)]       // UTC04c: trimmed
    [InlineData("LOW", IssuePriorities.Low)]             // UTC04d
    [InlineData("lowest", IssuePriorities.Low)]          // UTC04e: LOWEST -> LOW
    [InlineData("MEDIUM", IssuePriorities.High)]         // UTC04f: unmapped -> HIGH
    [InlineData("whatever", IssuePriorities.High)]       // UTC04g: unknown -> HIGH
    [InlineData("", IssuePriorities.High)]               // UTC04h: empty -> HIGH
    [InlineData(null, IssuePriorities.High)]             // UTC04i: null -> HIGH
    public async Task ReportBugAsync_MapsPriorityToIssuePriority(string? priority, string expected)
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var tc = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { tc.Id }, priority: priority), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, CapturedIssueRequest!.Priority);
    }

    // UTC05: Requirement links of all reported cases are forwarded to Kanban (distinct per document + upper-cased uid)
    [Fact]
    public async Task ReportBugAsync_ForwardsDistinctRequirementRefsOfCases()
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var docA = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-1", "REQ-001", "h1");
        var docB = TSH.AddDocumentWithRequirement(Db, TSH.ProjectId, "SRS-2", "REQ-002", "h2");
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        var b = TSH.AddCase(Db, tree, "TC_2", "B", 2);
        Db.TestCaseRequirementLinks.AddRange(
            new TestCaseRequirementLink { TestCaseId = a.Id, DocumentId = docA.Id, ReqUid = "REQ-001", LinkedAt = TSH.OldDate },
            new TestCaseRequirementLink { TestCaseId = b.Id, DocumentId = docA.Id, ReqUid = "req-001", LinkedAt = TSH.OldDate },
            new TestCaseRequirementLink { TestCaseId = b.Id, DocumentId = docB.Id, ReqUid = "REQ-002", LinkedAt = TSH.OldDate });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id, b.Id }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var refs = CapturedIssueRequest!.RequirementRefs!;
        Assert.Equal(2, refs.Count);
        Assert.Contains(refs, r => r.DocumentId == docA.Id && r.ReqUid == "REQ-001");
        Assert.Contains(refs, r => r.DocumentId == docB.Id && r.ReqUid == "REQ-002");
    }

    // UTC06: Duplicate test-case ids are de-duplicated (one activity, one link)
    [Fact]
    public async Task ReportBugAsync_WithDuplicateCaseIds_ProcessesCaseOnce()
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id, a.Id, a.Id }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(1, await Db.TestActivities.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC07: EnvId <= 0 -> activity EnvId stays null
    [Theory]
    [InlineData(0)]     // UTC07a
    [InlineData(-4)]    // UTC07b
    public async Task ReportBugAsync_WithNonPositiveEnvId_LeavesActivityEnvNull(long envId)
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }, envId: envId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null((await Db.TestActivities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).EnvId);
    }

    // UTC08: Some ids belong to other plans / do not exist -> only the plan's cases are linked
    [Fact]
    public async Task ReportBugAsync_WithMixedValidAndForeignIds_LinksOnlyPlanCases()
    {
        var (tree, issue) = SetupValidPlanWithKanbanIssue();
        var mine = TSH.AddCase(Db, tree, "TC_1", "Mine", 1);
        var other = TSH.AddPlanTree(Db);
        var foreign = TSH.AddCase(Db, other, "TC_1", "Foreign", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { mine.Id, foreign.Id, 9999 }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal(mine.Id, dto.TestCaseId);
        Assert.Equal(issue.Id, (await LoadCaseAsync(mine.Id)).BugIssueId);
        Assert.Null((await LoadCaseAsync(foreign.Id)).BugIssueId);
    }

    // UTC09: Case already linked to another bug is re-linked to the new bug
    [Fact]
    public async Task ReportBugAsync_WhenCaseAlreadyHasBug_ReplacesBugLink()
    {
        var (tree, issue) = SetupValidPlanWithKanbanIssue();
        var oldIssue = TSH.AddIssue(Db, TSH.ProjectId, "BUG-1", "Old bug");
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1, bugIssueId: oldIssue.Id);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(issue.Id, (await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Equal("BUG-42", result.Data!.Items.Single().Bug!.Key);
    }

    // UTC10: Plan already handed over -> still allowed (current behaviour: no handover check)
    [Fact]
    public async Task ReportBugAsync_WhenPlanHandedOver_StillCreatesBug()
    {
        var (tree, issue) = SetupValidPlanWithKanbanIssue(TestPlanStatuses.HandedOver);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(issue.Id, (await LoadCaseAsync(a.Id)).BugIssueId);
    }

    // UTC11: Soft-deleted cases are not filtered out (current behaviour) -> they get linked too
    [Fact]
    public async Task ReportBugAsync_WithDeletedCase_StillLinksCase()
    {
        var (tree, issue) = SetupValidPlanWithKanbanIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1, TestCaseStatuses.Deleted);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(issue.Id, (await LoadCaseAsync(a.Id)).BugIssueId);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC12: Plan not found -> 404 TEST_PLAN_NOT_FOUND, Kanban not called
    [Fact]
    public async Task ReportBugAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        SetupCurrentUser(5);

        var result = await TestingService.ReportBugAsync(9999, Req(new long[] { 1 }), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        VerifyGuardNeverCalled();
        KanbanServiceMock.Verify(k => k.CreateIssueAsync(It.IsAny<long>(), It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC13: Unauthenticated -> 401, Kanban not called, no changes
    [Fact]
    public async Task ReportBugAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var tree = TSH.AddPlanTree(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        SetupGuardUnauthenticated(TSH.ProjectId, PermissionKeys.TestRunExecute);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        KanbanServiceMock.Verify(k => k.CreateIssueAsync(It.IsAny<long>(), It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Empty(Db.TestActivities);
    }

    // UTC14: Permission denied -> 403 with required permission, Kanban not called
    [Fact]
    public async Task ReportBugAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var tree = TSH.AddPlanTree(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        SetupGuardDenies(TSH.ProjectId, PermissionKeys.TestRunExecute);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestRunExecute, result.RequiredPermission);
        KanbanServiceMock.Verify(k => k.CreateIssueAsync(It.IsAny<long>(), It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
    }

    // UTC15: Empty TestCaseIds -> 400 VALIDATION_ERROR on "testCaseIds", Kanban not called
    [Fact]
    public async Task ReportBugAsync_WithEmptyTestCaseIds_ReturnsValidationError()
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(Array.Empty<long>()), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.NotNull(result.Errors);
        Assert.Equal(new[] { "At least 1 test case is required." }, result.Errors["testCaseIds"]);
        KanbanServiceMock.Verify(k => k.CreateIssueAsync(It.IsAny<long>(), It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC16: Null TestCaseIds -> 400 VALIDATION_ERROR
    [Fact]
    public async Task ReportBugAsync_WithNullTestCaseIds_ReturnsValidationError()
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var request = new ReportTestBugRequest { TestCaseIds = null!, Title = "t" };

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, request, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("testCaseIds"));
    }

    // UTC17: None of the ids belong to the plan -> 404 TEST_CASE_NOT_FOUND, Kanban not called
    [Fact]
    public async Task ReportBugAsync_WhenNoCaseFoundInPlan_ReturnsNotFound()
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var other = TSH.AddPlanTree(Db);
        var foreign = TSH.AddCase(Db, other, "TC_1", "Foreign", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { foreign.Id, 9999 }), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Test cases not found.", result.ErrorMessage);
        KanbanServiceMock.Verify(k => k.CreateIssueAsync(It.IsAny<long>(), It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null((await LoadCaseAsync(foreign.Id)).BugIssueId);
    }

    // UTC18: Kanban rejects creation (403 from Kanban) -> error propagated, no case linked, no activity, plan untouched
    [Fact]
    public async Task ReportBugAsync_WhenKanbanCreateIssueFails_PropagatesErrorAndPersistsNothing()
    {
        SetupCurrentUser(5);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestRunExecute);
        var tree = TSH.AddPlanTree(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        KanbanServiceMock
            .Setup(k => k.CreateIssueAsync(TSH.ProjectId, It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IssueDetailDto>.ForbiddenPermission(PermissionKeys.IssueCreate));

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.IssueCreate, result.RequiredPermission);
        Assert.Null(result.Data);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Empty(Db.TestActivities);
        Assert.Equal(TSH.OldDate, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC19: Kanban validation error is propagated with its errors dictionary
    [Fact]
    public async Task ReportBugAsync_WhenKanbanReturnsValidationError_PropagatesValidationErrors()
    {
        SetupCurrentUser(5);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestRunExecute);
        var tree = TSH.AddPlanTree(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        KanbanServiceMock
            .Setup(k => k.CreateIssueAsync(TSH.ProjectId, It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IssueDetailDto>.Validation(new Dictionary<string, string[]> { ["title"] = new[] { "Too long" } }));

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(new[] { "Too long" }, result.Errors!["title"]);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
    }

    // UTC20: Kanban reports success but returns no data -> error result with the Kanban status code (200) and nothing persisted (current behaviour)
    [Fact]
    public async Task ReportBugAsync_WhenKanbanSucceedsWithoutData_ReturnsFailureAndPersistsNothing()
    {
        SetupCurrentUser(5);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestRunExecute);
        var tree = TSH.AddPlanTree(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        KanbanServiceMock
            .Setup(k => k.CreateIssueAsync(TSH.ProjectId, It.IsAny<CreateIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IssueDetailDto>.Ok(null!));

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Data);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Empty(Db.TestActivities);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC21: PlanId = 0 -> 404 TEST_PLAN_NOT_FOUND
    [Fact]
    public async Task ReportBugAsync_WithPlanIdZero_ReturnsNotFound()
    {
        var result = await TestingService.ReportBugAsync(0, Req(new long[] { 1 }), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
    }

    // UTC22: Negative PlanId -> 404 TEST_PLAN_NOT_FOUND
    [Fact]
    public async Task ReportBugAsync_WithNegativePlanId_ReturnsNotFound()
    {
        var result = await TestingService.ReportBugAsync(-1, Req(new long[] { 1 }), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
    }

    // UTC23: Exactly one case id (minimum) -> accepted
    [Fact]
    public async Task ReportBugAsync_WithExactlyOneCaseId_IsAccepted()
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
    }

    // UTC24: Case ids that are all zero/negative collapse to ids not found -> 404 TEST_CASE_NOT_FOUND
    [Fact]
    public async Task ReportBugAsync_WithNonPositiveCaseIds_ReturnsCaseNotFound()
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new long[] { 0, -1 }), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
    }

    // UTC25: Very long title (10 000 chars) is forwarded untouched to Kanban (service enforces no limit)
    [Fact]
    public async Task ReportBugAsync_WithVeryLongTitle_ForwardsTitleToKanban()
    {
        var (tree, _) = SetupValidPlanWithKanbanIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        var title = new string('b', 10_000);

        var result = await TestingService.ReportBugAsync(tree.Plan.Id, Req(new[] { a.Id }, title), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(10_000, CapturedIssueRequest!.Title.Length);
    }
}
