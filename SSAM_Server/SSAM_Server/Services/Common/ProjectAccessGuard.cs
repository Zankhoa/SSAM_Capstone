using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.Security;

namespace SSAM_Server.Services.Common;

/// <summary>
/// Shared implementation of project-level permission and admin checks.
/// </summary>
public class ProjectAccessGuard : IProjectAccessGuard
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;

    public ProjectAccessGuard(
        AppDbContext db,
        ICurrentUser currentUser,
        IPermissionService permissions)
    {
        _db = db;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<ServiceResult> EnsureProjectPermissionAsync(long projectId, string permission, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserIdOrNull is null)
            return ServiceResult.Unauthorized();

        var projectExists = await _db.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
            return ServiceResult.NotFound("PROJECT_NOT_FOUND", "Project not found.");

        var hasPerm = await _permissions.HasProjectPermissionAsync(_currentUser.UserId, projectId, permission, ct);
        if (!hasPerm)
            return ServiceResult.PermissionDenied(permission);

        return ServiceResult.Success();
    }

    public async Task<bool> IsCompanyOrWorkspaceAdminAsync(long userId, long projectId, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return false;

        var wsPerms = await _permissions.GetWorkspacePermissionsAsync(userId, project.WorkspaceId, ct);
        if (wsPerms.Contains(PermissionKeys.WorkspaceSettingsManage, StringComparer.OrdinalIgnoreCase))
            return true;

        if (project.Workspace.CompanyId.HasValue)
        {
            var companyPerms = await _permissions.GetCompanyPermissionsAsync(userId, ct);
            if (companyPerms.Contains(PermissionKeys.CompanyRolesManage, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
