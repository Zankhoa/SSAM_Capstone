using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

// NOTE: the success path of DeleteModuleAsync relies on EF Core ExecuteUpdateAsync / ExecuteDeleteAsync, which the
// InMemory provider does not support. Therefore only the lookup / guard branches (reached before any bulk operation)
// are covered here.
public class DeleteModuleAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // (No normal case is executable on the InMemory provider - see note above.)

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC01: Module does not exist -> 404 MODULE_NOT_FOUND, guard never consulted
    [Fact]
    public async Task DeleteModuleAsync_WhenModuleNotFound_ReturnsNotFound()
    {
        var result = await TestingService.DeleteModuleAsync(999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("MODULE_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC02: Caller is not authenticated -> 401, module with its sections and cases is kept
    [Fact]
    public async Task DeleteModuleAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsData()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        var section = H.AddSection(Db, module.Id);
        H.AddCase(Db, plan, module, section);
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteModuleAsync(module.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal(1, await Db.TestModules.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC03: Caller lacks testcase.manage -> 403 FORBIDDEN, module with its sections and cases is kept
    [Fact]
    public async Task DeleteModuleAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsData()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        var section = H.AddSection(Db, module.Id);
        H.AddCase(Db, plan, module, section);
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteModuleAsync(module.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseDelete, result.RequiredPermission);
        Assert.Equal(1, await Db.TestModules.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseDelete, It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC04: Module id 0 (lowest invalid id) -> 404 MODULE_NOT_FOUND even when other modules exist
    [Fact]
    public async Task DeleteModuleAsync_WhenIdIsZero_ReturnsNotFound()
    {
        var plan = H.AddPlan(Db, ProjectId);
        H.AddModule(Db, plan.Id);

        var result = await TestingService.DeleteModuleAsync(0, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("MODULE_NOT_FOUND", result.ErrorCode);
        Assert.Equal(1, await Db.TestModules.CountAsync(TestContext.Current.CancellationToken));
    }
}
