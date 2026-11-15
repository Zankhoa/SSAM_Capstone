using SSAM_Server.Common;

namespace SSAM_Server.Interfaces.Services;

/// <summary>
/// Centralizes authentication, project existence, and project/workspace/company permission checks.
/// </summary>
public interface IProjectAccessGuard
{
    /// <summary>
    /// Verifies that the current user is authenticated, the project exists, and the user holds <paramref name="permission"/>.
    /// </summary>
    Task<ServiceResult> EnsureProjectPermissionAsync(long projectId, string permission, CancellationToken ct = default);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="userId"/> is a workspace or company administrator for <paramref name="projectId"/>.
    /// </summary>
    Task<bool> IsCompanyOrWorkspaceAdminAsync(long userId, long projectId, CancellationToken ct = default);
}
