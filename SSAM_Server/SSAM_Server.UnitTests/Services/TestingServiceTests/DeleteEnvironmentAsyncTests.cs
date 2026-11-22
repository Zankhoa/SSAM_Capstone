using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class DeleteEnvironmentAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;
    private const long UserId = 7;

    private void SetupAuthorized()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Delete one of two environments -> 204, environment soft-deleted, plan touched, activity logged
    [Fact]
    public async Task DeleteEnvironmentAsync_WhenPlanHasMultipleEnvironments_SoftDeletesAndLogsActivity()
    {
        SetupAuthorized();
        var plan = H.AddPlan(Db, ProjectId);
        var env1 = H.AddEnv(Db, plan.Id, "SIT", 1);
        var env2 = H.AddEnv(Db, plan.Id, "UAT", 2);

        var result = await TestingService.DeleteEnvironmentAsync(env1.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);

        Db.ChangeTracker.Clear();
        var saved1 = await Db.TestEnvironments.SingleAsync(e => e.Id == env1.Id, TestContext.Current.CancellationToken);
        var saved2 = await Db.TestEnvironments.SingleAsync(e => e.Id == env2.Id, TestContext.Current.CancellationToken);
        Assert.True(saved1.IsDeleted);
        Assert.False(saved2.IsDeleted);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);

        var activity = await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(plan.Id, activity.PlanId);
        Assert.Equal(TestActivityTypes.Setup, activity.Type);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Deleted environment SIT", activity.Text);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, activity.At);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseDelete, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Environment has assignments and retest flags -> they are hard-deleted; those of other environments are kept
    [Fact]
    public async Task DeleteEnvironmentAsync_RemovesAssignmentsAndRetestFlagsOfThatEnvironmentOnly()
    {
        SetupAuthorized();
        var plan = H.AddPlan(Db, ProjectId);
        var env1 = H.AddEnv(Db, plan.Id, "SIT", 1);
        var env2 = H.AddEnv(Db, plan.Id, "UAT", 2);
        var module = H.AddModule(Db, plan.Id);
        var section = H.AddSection(Db, module.Id);
        var tc = H.AddCase(Db, plan, module, section);
        Db.TestAssignments.Add(new TestAssignment { ModuleId = module.Id, EnvId = env1.Id, AssigneeId = 1 });
        Db.TestAssignments.Add(new TestAssignment { ModuleId = module.Id, EnvId = env2.Id, AssigneeId = 1 });
        Db.TestRetestFlags.Add(new TestRetestFlag { TestCaseId = tc.Id, EnvId = env1.Id, Reason = TestRetestReasons.Manual, At = Clock.GetUtcNow().UtcDateTime });
        Db.TestRetestFlags.Add(new TestRetestFlag { TestCaseId = tc.Id, EnvId = env2.Id, Reason = TestRetestReasons.Manual, At = Clock.GetUtcNow().UtcDateTime });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.DeleteEnvironmentAsync(env1.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Db.ChangeTracker.Clear();
        var assignment = await Db.TestAssignments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(env2.Id, assignment.EnvId);
        var flag = await Db.TestRetestFlags.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(env2.Id, flag.EnvId);
    }

    // UTC03: Deleted environments of the plan do not count as active -> deleting one of 2 active (+1 already deleted) succeeds
    [Fact]
    public async Task DeleteEnvironmentAsync_WhenTwoActiveAndOneAlreadyDeleted_Succeeds()
    {
        SetupAuthorized();
        var plan = H.AddPlan(Db, ProjectId);
        H.AddEnv(Db, plan.Id, "OLD", 1, isDeleted: true);
        var env2 = H.AddEnv(Db, plan.Id, "SIT", 2);
        H.AddEnv(Db, plan.Id, "UAT", 3);

        var result = await TestingService.DeleteEnvironmentAsync(env2.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await Db.TestEnvironments.CountAsync(e => e.IsDeleted, TestContext.Current.CancellationToken));
    }

    // UTC04: Another plan's environments are not counted -> still conflict when this plan has only one active environment
    [Fact]
    public async Task DeleteEnvironmentAsync_DoesNotCountEnvironmentsOfOtherPlans()
    {
        SetupAuthorized();
        var plan = H.AddPlan(Db, ProjectId);
        var other = H.AddPlan(Db, ProjectId, "Other");
        var env = H.AddEnv(Db, plan.Id, "SIT", 1);
        H.AddEnv(Db, other.Id, "SIT", 1);
        H.AddEnv(Db, other.Id, "UAT", 2);

        var result = await TestingService.DeleteEnvironmentAsync(env.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("LAST_ENVIRONMENT", result.ErrorCode);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC05: Environment does not exist -> 404 ENVIRONMENT_NOT_FOUND, guard never consulted
    [Fact]
    public async Task DeleteEnvironmentAsync_WhenEnvironmentNotFound_ReturnsNotFound()
    {
        var result = await TestingService.DeleteEnvironmentAsync(999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ENVIRONMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC06: Environment already soft-deleted -> 404 ENVIRONMENT_NOT_FOUND, no activity
    [Fact]
    public async Task DeleteEnvironmentAsync_WhenEnvironmentAlreadyDeleted_ReturnsNotFound()
    {
        SetupAuthorized();
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id, "OLD", 1, isDeleted: true);
        H.AddEnv(Db, plan.Id, "SIT", 2);

        var result = await TestingService.DeleteEnvironmentAsync(env.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ENVIRONMENT_NOT_FOUND", result.ErrorCode);
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC07: Caller is not authenticated -> 401, environment kept
    [Fact]
    public async Task DeleteEnvironmentAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id, "SIT", 1);
        H.AddEnv(Db, plan.Id, "UAT", 2);
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteEnvironmentAsync(env.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal(0, await Db.TestEnvironments.CountAsync(e => e.IsDeleted, TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC08: Caller lacks testcase.manage -> 403 FORBIDDEN, environment kept
    [Fact]
    public async Task DeleteEnvironmentAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id, "SIT", 1);
        H.AddEnv(Db, plan.Id, "UAT", 2);
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteEnvironmentAsync(env.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseDelete, result.RequiredPermission);
        Assert.Equal(0, await Db.TestEnvironments.CountAsync(e => e.IsDeleted, TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC09: Only one active environment left -> 409 LAST_ENVIRONMENT, nothing changed
    [Fact]
    public async Task DeleteEnvironmentAsync_WhenLastActiveEnvironment_ReturnsConflict()
    {
        SetupAuthorized();
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id, "SIT", 1);
        H.AddEnv(Db, plan.Id, "OLD", 2, isDeleted: true);

        var result = await TestingService.DeleteEnvironmentAsync(env.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("LAST_ENVIRONMENT", result.ErrorCode);
        Db.ChangeTracker.Clear();
        Assert.False((await Db.TestEnvironments.SingleAsync(e => e.Id == env.Id, TestContext.Current.CancellationToken)).IsDeleted);
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC10: Exactly 2 active environments (boundary above the minimum of 1) -> first delete ok, second delete -> conflict
    [Fact]
    public async Task DeleteEnvironmentAsync_WithExactlyTwoActiveEnvironments_AllowsOnlyOneDeletion()
    {
        SetupAuthorized();
        var plan = H.AddPlan(Db, ProjectId);
        var env1 = H.AddEnv(Db, plan.Id, "SIT", 1);
        var env2 = H.AddEnv(Db, plan.Id, "UAT", 2);

        var first = await TestingService.DeleteEnvironmentAsync(env1.Id, TestContext.Current.CancellationToken);
        var second = await TestingService.DeleteEnvironmentAsync(env2.Id, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, first.StatusCode);
        Assert.False(second.IsSuccess);
        Assert.Equal("LAST_ENVIRONMENT", second.ErrorCode);
        Db.ChangeTracker.Clear();
        Assert.True((await Db.TestEnvironments.SingleAsync(e => e.Id == env1.Id, TestContext.Current.CancellationToken)).IsDeleted);
        Assert.False((await Db.TestEnvironments.SingleAsync(e => e.Id == env2.Id, TestContext.Current.CancellationToken)).IsDeleted);
    }

    // UTC11: Environment id 0 (lowest invalid id) -> 404 ENVIRONMENT_NOT_FOUND
    [Fact]
    public async Task DeleteEnvironmentAsync_WhenIdIsZero_ReturnsNotFound()
    {
        var result = await TestingService.DeleteEnvironmentAsync(0, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ENVIRONMENT_NOT_FOUND", result.ErrorCode);
    }
}
