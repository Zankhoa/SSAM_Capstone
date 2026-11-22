using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

// NOTE: the success path of DeleteTestPlanAsync relies on EF Core ExecuteDeleteAsync, which the InMemory provider
// does not support. Therefore only the guard / lookup branches (reached before any bulk delete) are covered here.
public class DeleteTestPlanAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // (No normal case is executable on the InMemory provider - see note above.)

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC01: Plan does not exist -> 404 TEST_PLAN_NOT_FOUND, guard never consulted
    [Fact]
    public async Task DeleteTestPlanAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        var result = await TestingService.DeleteTestPlanAsync(999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC02: Caller is not authenticated -> 401 and the plan with all its children is kept
    [Fact]
    public async Task DeleteTestPlanAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsData()
    {
        var plan = H.AddPlan(Db, ProjectId);
        H.AddEnv(Db, plan.Id);
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteTestPlanAsync(plan.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal(1, await Db.TestPlans.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestEnvironments.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC03: Caller lacks testcase.manage -> 403 FORBIDDEN and the plan with all its children is kept
    [Fact]
    public async Task DeleteTestPlanAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsData()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id);
        var module = H.AddModule(Db, plan.Id);
        var section = H.AddSection(Db, module.Id);
        H.AddCase(Db, plan, module, section);
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteTestPlanAsync(plan.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseDelete, result.RequiredPermission);
        Assert.Equal(1, await Db.TestPlans.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestEnvironments.CountAsync(e => e.Id == env.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestModules.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseDelete, It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC04: Plan id 0 (lowest invalid id) -> 404 TEST_PLAN_NOT_FOUND even when other plans exist
    [Fact]
    public async Task DeleteTestPlanAsync_WhenIdIsZero_ReturnsNotFound()
    {
        H.AddPlan(Db, ProjectId);

        var result = await TestingService.DeleteTestPlanAsync(0, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        Assert.Equal(1, await Db.TestPlans.CountAsync(TestContext.Current.CancellationToken));
    }
}
