using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using TSH = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersCases;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class LinkBugAsyncTests : TestingCasesTestBase
{
    private (CasesSeedTree Tree, Issue Issue) SetupValidPlanAndIssue(string planStatus = TestPlanStatuses.Open)
    {
        SetupCurrentUser(5);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestRunExecute);
        var tree = TSH.AddPlanTree(Db, TSH.ProjectId, planStatus);
        var issue = TSH.AddIssue(Db, TSH.ProjectId, "BUG-9", "Existing crash", statusName: "In Progress", statusCategory: StatusCategories.InProgress, priority: IssuePriorities.Low);
        return (tree, issue);
    }

    private static LinkTestBugRequest Req(params long[] ids) => new() { TestCaseIds = ids.ToList() };

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Link an existing bug to two cases -> 200, cases linked, activity per case, DTO bug filled, plan touched
    [Fact]
    public async Task LinkBugAsync_WithValidCases_LinksBugAndLogsActivities()
    {
        var (tree, issue) = SetupValidPlanAndIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        var b = TSH.AddCase(Db, tree, "TC_2", "B", 2);
        var untouched = TSH.AddCase(Db, tree, "TC_3", "C", 3);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(a.Id, b.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);
        foreach (var dto in result.Data.Items)
        {
            Assert.NotNull(dto.Bug);
            Assert.Equal(issue.Id, dto.Bug.IssueId);
            Assert.Equal("BUG-9", dto.Bug.Key);
            Assert.Equal("Existing crash", dto.Bug.Title);
            Assert.Equal("LOW", dto.Bug.Priority);
            Assert.Equal("In Progress", dto.Bug.StatusName);
            Assert.Equal(StatusCategories.InProgress, dto.Bug.StatusCategory);
        }

        var savedA = await LoadCaseAsync(a.Id);
        var savedB = await LoadCaseAsync(b.Id);
        Assert.Equal(issue.Id, savedA.BugIssueId);
        Assert.Equal(issue.Id, savedB.BugIssueId);
        Assert.Equal(Now, savedA.UpdatedAt);
        Assert.Equal(Now, savedB.UpdatedAt);
        Assert.Null((await LoadCaseAsync(untouched.Id)).BugIssueId);
        Assert.Equal(TSH.OldDate, (await LoadCaseAsync(untouched.Id)).UpdatedAt);

        var activities = await Db.TestActivities.AsNoTracking().OrderBy(x => x.TestCaseId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, activities.Count);
        Assert.All(activities, act =>
        {
            Assert.Equal(tree.Plan.Id, act.PlanId);
            Assert.Equal(TestActivityTypes.Bug, act.Type);
            Assert.Equal(Now, act.At);
            Assert.Equal(5, act.ActorId);
            Assert.Null(act.EnvId);
            Assert.Equal(tree.Module.Id, act.ModuleId);
            Assert.Equal("Linked to existing bug BUG-9", act.Text);
        });
        Assert.Equal(new[] { a.Id, b.Id }, activities.Select(x => x.TestCaseId!.Value).ToArray());
        Assert.Equal(Now, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(TSH.ProjectId, PermissionKeys.TestRunExecute, It.IsAny<CancellationToken>()), Times.Once);
        KanbanServiceMock.VerifyNoOtherCalls();
    }

    // UTC02: Re-linking a case that already has another bug overwrites the previous link
    [Fact]
    public async Task LinkBugAsync_WhenCaseAlreadyLinkedToOtherBug_OverwritesLink()
    {
        var (tree, issue) = SetupValidPlanAndIssue();
        var oldIssue = TSH.AddIssue(Db, TSH.ProjectId, "BUG-1", "Old");
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1, bugIssueId: oldIssue.Id);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(issue.Id, (await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Equal("BUG-9", result.Data!.Items.Single().Bug!.Key);
    }

    // UTC03: Duplicate ids are de-duplicated -> one activity per case
    [Fact]
    public async Task LinkBugAsync_WithDuplicateCaseIds_ProcessesCaseOnce()
    {
        var (tree, issue) = SetupValidPlanAndIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(a.Id, a.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(1, await Db.TestActivities.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC04: Ids of cases from other plans or unknown are ignored; valid ones are linked
    [Fact]
    public async Task LinkBugAsync_WithMixedValidAndForeignIds_LinksOnlyPlanCases()
    {
        var (tree, issue) = SetupValidPlanAndIssue();
        var mine = TSH.AddCase(Db, tree, "TC_1", "Mine", 1);
        var other = TSH.AddPlanTree(Db);
        var foreign = TSH.AddCase(Db, other, "TC_1", "Foreign", 1);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(mine.Id, foreign.Id, 9999), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal(mine.Id, dto.TestCaseId);
        Assert.Null((await LoadCaseAsync(foreign.Id)).BugIssueId);
        Assert.Equal(1, await Db.TestActivities.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC05: Empty case list -> 200 with empty items, plan UpdatedAt still bumped, no activity (current behaviour: no validation)
    [Fact]
    public async Task LinkBugAsync_WithEmptyCaseList_ReturnsEmptyItemsAndTouchesPlan()
    {
        var (tree, issue) = SetupValidPlanAndIssue();

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Empty(result.Data!.Items);
        Assert.Empty(Db.TestActivities);
        Assert.Equal(Now, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC06: Null case list is treated as empty
    [Fact]
    public async Task LinkBugAsync_WithNullCaseList_ReturnsEmptyItems()
    {
        var (tree, issue) = SetupValidPlanAndIssue();

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, new LinkTestBugRequest { TestCaseIds = null! }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
    }

    // UTC07: Plan handed over -> still allowed (current behaviour: no handover check)
    [Fact]
    public async Task LinkBugAsync_WhenPlanHandedOver_StillLinks()
    {
        var (tree, issue) = SetupValidPlanAndIssue(TestPlanStatuses.HandedOver);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(issue.Id, (await LoadCaseAsync(a.Id)).BugIssueId);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC08: Plan not found -> 404 TEST_PLAN_NOT_FOUND, guard not called
    [Fact]
    public async Task LinkBugAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        SetupCurrentUser(5);

        var result = await TestingService.LinkBugAsync(9999, 1, Req(1), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Test plan not found.", result.ErrorMessage);
        VerifyGuardNeverCalled();
    }

    // UTC09: Unauthenticated -> 401, nothing linked
    [Fact]
    public async Task LinkBugAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var tree = TSH.AddPlanTree(Db);
        var issue = TSH.AddIssue(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        SetupGuardUnauthenticated(TSH.ProjectId, PermissionKeys.TestRunExecute);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Empty(Db.TestActivities);
    }

    // UTC10: Permission denied -> 403 with required permission, nothing linked
    [Fact]
    public async Task LinkBugAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var tree = TSH.AddPlanTree(Db);
        var issue = TSH.AddIssue(Db);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);
        SetupGuardDenies(TSH.ProjectId, PermissionKeys.TestRunExecute);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestRunExecute, result.RequiredPermission);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Equal(TSH.OldDate, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC11: Issue does not exist -> 404 ISSUE_NOT_FOUND, nothing linked
    [Fact]
    public async Task LinkBugAsync_WhenIssueNotFound_ReturnsNotFound()
    {
        var (tree, _) = SetupValidPlanAndIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, 9999, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Bug issue not found.", result.ErrorMessage);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
        Assert.Empty(Db.TestActivities);
        Assert.Equal(TSH.OldDate, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
    }

    // UTC12: Issue belongs to another project -> 404 ISSUE_NOT_FOUND
    [Fact]
    public async Task LinkBugAsync_WhenIssueBelongsToOtherProject_ReturnsNotFound()
    {
        var (tree, _) = SetupValidPlanAndIssue();
        var foreignIssue = TSH.AddIssue(Db, 200, "OTH-1", "Other project bug");
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, foreignIssue.Id, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
    }

    // UTC13: Issue is soft-deleted -> 404 ISSUE_NOT_FOUND
    [Fact]
    public async Task LinkBugAsync_WhenIssueIsDeleted_ReturnsNotFound()
    {
        var (tree, _) = SetupValidPlanAndIssue();
        var deleted = TSH.AddIssue(Db, TSH.ProjectId, "BUG-DEL", "Deleted bug", isDeleted: true);
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, deleted.Id, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
        Assert.Null((await LoadCaseAsync(a.Id)).BugIssueId);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC14: PlanId = 0 -> 404 TEST_PLAN_NOT_FOUND
    [Fact]
    public async Task LinkBugAsync_WithPlanIdZero_ReturnsNotFound()
    {
        var result = await TestingService.LinkBugAsync(0, 1, Req(1), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
    }

    // UTC15: IssueId = 0 -> 404 ISSUE_NOT_FOUND
    [Fact]
    public async Task LinkBugAsync_WithIssueIdZero_ReturnsNotFound()
    {
        var (tree, _) = SetupValidPlanAndIssue();

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, 0, Req(1), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC16: Negative IssueId -> 404 ISSUE_NOT_FOUND
    [Fact]
    public async Task LinkBugAsync_WithNegativeIssueId_ReturnsNotFound()
    {
        var (tree, _) = SetupValidPlanAndIssue();

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, -1, Req(1), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC17: Exactly one case (minimum) -> accepted
    [Fact]
    public async Task LinkBugAsync_WithExactlyOneCase_IsAccepted()
    {
        var (tree, issue) = SetupValidPlanAndIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
    }

    // UTC18: Soft-deleted cases are not filtered out (current behaviour) -> they get linked as well
    [Fact]
    public async Task LinkBugAsync_WithDeletedCase_StillLinksCase()
    {
        var (tree, issue) = SetupValidPlanAndIssue();
        var a = TSH.AddCase(Db, tree, "TC_1", "A", 1, TestCaseStatuses.Deleted);

        var result = await TestingService.LinkBugAsync(tree.Plan.Id, issue.Id, Req(a.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(issue.Id, (await LoadCaseAsync(a.Id)).BugIssueId);
    }
}
