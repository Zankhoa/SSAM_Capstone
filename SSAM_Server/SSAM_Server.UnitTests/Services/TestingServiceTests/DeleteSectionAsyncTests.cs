using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

// NOTE: the success path of DeleteSectionAsync relies on EF Core ExecuteDeleteAsync, which the InMemory provider
// does not support. Therefore only the lookup / guard branches (reached before any bulk operation) are covered here.
public class DeleteSectionAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // (No normal case is executable on the InMemory provider - see note above.)

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC01: Section does not exist -> 404 SECTION_NOT_FOUND, guard never consulted
    [Fact]
    public async Task DeleteSectionAsync_WhenSectionNotFound_ReturnsNotFound()
    {
        var result = await TestingService.DeleteSectionAsync(999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC02: Caller is not authenticated -> 401, section, child and case are kept
    [Fact]
    public async Task DeleteSectionAsync_WhenUnauthenticated_ReturnsUnauthorizedAndKeepsData()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        var section = H.AddSection(Db, module.Id);
        var child = H.AddSection(Db, module.Id, "1.1", "Child", 1, section.Id);
        H.AddCase(Db, plan, module, child);
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteSectionAsync(section.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal(2, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC03: Caller lacks testcase.manage -> 403 FORBIDDEN, section, child and case are kept
    [Fact]
    public async Task DeleteSectionAsync_WhenPermissionDenied_ReturnsForbiddenAndKeepsData()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        var section = H.AddSection(Db, module.Id);
        var child = H.AddSection(Db, module.Id, "1.1", "Child", 1, section.Id);
        H.AddCase(Db, plan, module, child);
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseDelete);

        var result = await TestingService.DeleteSectionAsync(section.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseDelete, result.RequiredPermission);
        Assert.Equal(2, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestCases.CountAsync(TestContext.Current.CancellationToken));
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseDelete, It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC04: Section id 0 (lowest invalid id) -> 404 SECTION_NOT_FOUND even when other sections exist
    [Fact]
    public async Task DeleteSectionAsync_WhenIdIsZero_ReturnsNotFound()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        H.AddSection(Db, module.Id);

        var result = await TestingService.DeleteSectionAsync(0, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
        Assert.Equal(1, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
    }
}
