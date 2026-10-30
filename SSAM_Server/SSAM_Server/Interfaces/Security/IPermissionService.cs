using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Security;

public record WorkspaceAccessContext(
    Workspace Workspace,
    HashSet<string> Permissions,
    RoleRefDto? MyRole);

public record ProjectAccessContext(
    Project Project,
    Workspace Workspace,
    HashSet<string> Permissions,
    RoleRefDto? MyRole);

public interface IPermissionService
{
    Task<HashSet<string>?> GetTenantPermissionsAsync(long userId, long companyId, CancellationToken ct = default);
    Task<WorkspaceAccessContext?> GetWorkspaceAccessAsync(long userId, long workspaceId, CancellationToken ct = default);
    Task<ProjectAccessContext?> GetProjectAccessAsync(long userId, long projectId, CancellationToken ct = default);

    Task<ServiceResult<HashSet<string>>> RequireTenantAsync(long userId, long companyId, string? requiredPermission = null, CancellationToken ct = default);
    Task<ServiceResult<WorkspaceAccessContext>> RequireWorkspaceAsync(long userId, long workspaceId, string? requiredPermission = null, CancellationToken ct = default);
    Task<ServiceResult<ProjectAccessContext>> RequireProjectAsync(long userId, long projectId, string? requiredPermission = null, CancellationToken ct = default);

    Task<bool> HasProjectPermissionAsync(long userId, long projectId, string permission, CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> GetProjectPermissionsAsync(long userId, long projectId, CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> GetWorkspacePermissionsAsync(long userId, long workspaceId, CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> GetCompanyPermissionsAsync(long userId, CancellationToken ct = default);
}
