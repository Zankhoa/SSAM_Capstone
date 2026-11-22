using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Models;
using SSAM_Server.Security;
using TSH = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersCases;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class DeleteTestCaseAsyncTests : TestingCasesTestBase
{
    private (CasesSeedTree Tree, TestCase Case) SetupValidCase(string planStatus = TestPlanStatuses.Open, long userId = 1)
    {
        SetupCurrentUser(userId);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestCaseDelete);
        var tree = TSH.AddPlanTree(Db, TSH.ProjectId, planStatus);
        var tc = TSH.AddCase(Db, tree, "TC_1", "Login works", 1);
        return (tree, tc);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Delete an active case -> 204, soft-deleted (Status DELETED), timestamps and activity log written
    [Fact]
    public async Task DeleteTestCaseAsync_WithActiveCase_SoftDeletesAndLogsActivity()
    {
        var (tree, tc) = SetupValidCase(userId: 7);

        var result = await TestingService.DeleteTestCaseAsync(tc.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.Null(result.ErrorCode);

        var saved = await LoadCaseAsync(tc.Id);
        Assert.Equal(TestCaseStatuses.Deleted, saved.Status);
        Assert.Equal(Now, saved.UpdatedAt);
        Assert.Equal("Login works", saved.Title);
        Assert.Equal("TC_1", saved.Code);
        Assert.Equal(Now, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);

        var activity = await Db.TestActivities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(tree.Plan.Id, activity.PlanId);
        Assert.Equal(TestActivityTypes.TestCase, activity.Type);
        Assert.Equal(Now, activity.At);
        Assert.Equal(7, activity.ActorId);
        Assert.Equal(tree.Module.Id, activity.ModuleId);
        Assert.Equal(tc.Id, activity.TestCaseId);
        Assert.Equal("Deleted TC_1 · Login works", activity.Text);

        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(TSH.ProjectId, PermissionKeys.TestCaseDelete, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: The case row is kept (not physically removed) so history/results survive
    [Fact]
    public async Task DeleteTestCaseAsync_KeepsCaseRowInDatabase()
    {
        var (_, tc) = SetupValidCase();

        await TestingService.DeleteTestCaseAsync(tc.Id, TestContext.Current.CancellationToken);

        Assert.Equal(1, await Db.TestCases.AsNoTracking().CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC03: Only the targeted case is deleted; siblings stay active and untouched
    [Fact]
    public async Task DeleteTestCaseAsync_DoesNotAffectOtherCases()
    {
        var (tree, tc) = SetupValidCase();
        var sibling = TSH.AddCase(Db, tree, "TC_2", "Other", 2);

        await TestingService.DeleteTestCaseAsync(tc.Id, TestContext.Current.CancellationToken);

        var savedSibling = await LoadCaseAsync(sibling.Id);
        Assert.Equal(TestCaseStatuses.Active, savedSibling.Status);
        Assert.Equal(TSH.OldDate, savedSibling.UpdatedAt);
    }

    // UTC04: Deleting an already deleted case still succeeds (idempotent status, another activity row is logged)
    [Fact]
    public async Task DeleteTestCaseAsync_WithAlreadyDeletedCase_StillReturnsNoContent()
    {
        SetupCurrentUser(1);
        SetupGuardAllows(TSH.ProjectId, PermissionKeys.TestCaseDelete);
        var tree = TSH.AddPlanTree(Db);
        var tc = TSH.AddCase(Db, tree, "TC_1", "Gone", 1, TestCaseStatuses.Deleted);

        var result = await TestingService.DeleteTestCaseAsync(tc.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.Equal(TestCaseStatuses.Deleted, (await LoadCaseAsync(tc.Id)).Status);
        Assert.Equal(1, await Db.TestActivities.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC05: Plan already handed over -> delete still allowed (current behaviour: no handover check in DeleteTestCaseAsync)
    [Fact]
    public async Task DeleteTestCaseAsync_WhenPlanHandedOver_StillDeletes()
    {
        var (_, tc) = SetupValidCase(TestPlanStatuses.HandedOver);

        var result = await TestingService.DeleteTestCaseAsync(tc.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.Equal(TestCaseStatuses.Deleted, (await LoadCaseAsync(tc.Id)).Status);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC06: Case does not exist -> 404 TEST_CASE_NOT_FOUND, guard not called, no activity
    [Fact]
    public async Task DeleteTestCaseAsync_WhenCaseNotFound_ReturnsNotFound()
    {
        SetupCurrentUser(1);

        var result = await TestingService.DeleteTestCaseAsync(9999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Test case not found.", result.ErrorMessage);
        VerifyGuardNeverCalled();
        Assert.Empty(Db.TestActivities);
    }

    // UTC07: Unauthenticated -> 401, case stays active, no activity
    [Fact]
    public async Task DeleteTestCaseAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var tree = TSH.AddPlanTree(Db);
        var tc = TSH.AddCase(Db, tree);
        SetupGuardUnauthenticated(TSH.ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteTestCaseAsync(tc.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal(TestCaseStatuses.Active, (await LoadCaseAsync(tc.Id)).Status);
        Assert.Empty(Db.TestActivities);
    }

    // UTC08: Permission denied -> 403, case stays active, no activity
    [Fact]
    public async Task DeleteTestCaseAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var tree = TSH.AddPlanTree(Db);
        var tc = TSH.AddCase(Db, tree);
        SetupGuardDenies(TSH.ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteTestCaseAsync(tc.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseDelete, result.RequiredPermission);
        Assert.Equal(TestCaseStatuses.Active, (await LoadCaseAsync(tc.Id)).Status);
        Assert.Equal(TSH.OldDate, (await LoadPlanAsync(tree.Plan.Id)).UpdatedAt);
        Assert.Empty(Db.TestActivities);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC09: TestCaseId = 0 -> 404 TEST_CASE_NOT_FOUND
    [Fact]
    public async Task DeleteTestCaseAsync_WithIdZero_ReturnsNotFound()
    {
        var result = await TestingService.DeleteTestCaseAsync(0, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
    }

    // UTC10: Negative TestCaseId -> 404 TEST_CASE_NOT_FOUND
    [Fact]
    public async Task DeleteTestCaseAsync_WithNegativeId_ReturnsNotFound()
    {
        var result = await TestingService.DeleteTestCaseAsync(-1, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
    }

    // UTC11: long.MaxValue id -> 404 TEST_CASE_NOT_FOUND
    [Fact]
    public async Task DeleteTestCaseAsync_WithMaxLongId_ReturnsNotFound()
    {
        var result = await TestingService.DeleteTestCaseAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_CASE_NOT_FOUND", result.ErrorCode);
    }
}
