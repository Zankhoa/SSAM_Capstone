using Moq;
using SSAM_Server.Common;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

/// <summary>Shared guard/user setup for the test-case related TestingService test classes.</summary>
public abstract class TestingCasesTestBase : TestingServiceTestBase
{
    protected const string NowIso = "2026-11-21T10:00:00.000Z";

    protected DateTime Now => Clock.GetUtcNow().UtcDateTime;

    protected void SetupCurrentUser(long userId = 1)
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(userId);
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(userId);
        CurrentUserMock.Setup(u => u.IsAuthenticated).Returns(true);
    }

    /// <summary>Reads the case straight from the store (bypasses the change tracker).</summary>
    protected Task<SSAM_Server.Models.TestCase> LoadCaseAsync(long id)
        => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(Db.TestCases),
            c => c.Id == id,
            TestContext.Current.CancellationToken);

    protected Task<SSAM_Server.Models.TestPlan> LoadPlanAsync(long id)
        => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(Db.TestPlans),
            p => p.Id == id,
            TestContext.Current.CancellationToken);

    protected void SetupGuardAllows(long projectId, string permission)
    {
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());
    }

    protected void SetupGuardDenies(long projectId, string permission)
    {
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.ForbiddenPermission(permission));
    }

    protected void SetupGuardUnauthenticated(long projectId, string permission)
    {
        AccessGuardMock
            .Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Unauthorized("Your session is invalid or has expired."));
    }

    protected void VerifyGuardNeverCalled()
    {
        AccessGuardMock.Verify(
            g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
