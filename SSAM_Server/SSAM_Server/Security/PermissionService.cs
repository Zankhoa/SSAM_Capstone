using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Models;

namespace SSAM_Server.Security;

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
}

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;

    public PermissionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HashSet<string>?> GetTenantPermissionsAsync(long userId, long companyId, CancellationToken ct = default)
    {
        var member = await _db.CompanyMembers
            .AsNoTracking()
            .Include(cm => cm.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(cm => cm.CompanyId == companyId && cm.UserId == userId && cm.IsActive, ct);

        if (member == null)
            return null;

        if (member.Role.Code == CompanyRoleCodes.TenantAdmin)
            return new HashSet<string>(PermissionKeys.AllTenantKeys, StringComparer.Ordinal);

        if (!member.Role.IsActive)
            return new HashSet<string>(StringComparer.Ordinal);

        return member.Role.RolePermissions
            .Where(rp => rp.Permission.IsActive)
            .Select(rp => rp.Permission.Code)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<WorkspaceAccessContext?> GetWorkspaceAccessAsync(long userId, long workspaceId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId, ct);

        if (ws == null)
            return null;

        return await EvaluateWorkspaceAccessAsync(userId, ws, ct);
    }

    private async Task<WorkspaceAccessContext?> EvaluateWorkspaceAccessAsync(long userId, Workspace ws, CancellationToken ct)
    {
        // Personal workspace: owner has all WORKSPACE permissions, others get 404
        if (ws.CompanyId == null)
        {
            if (ws.CreatedBy != userId)
                return null;

            return new WorkspaceAccessContext(
                ws,
                new HashSet<string>(PermissionKeys.AllWorkspaceKeys, StringComparer.Ordinal),
                null);
        }

        var companyId = ws.CompanyId.Value;
        var companyMember = await _db.CompanyMembers
            .AsNoTracking()
            .Include(cm => cm.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(cm => cm.CompanyId == companyId && cm.UserId == userId && cm.IsActive, ct);

        if (companyMember == null)
            return null;

        var wsMember = await _db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.Role)
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == ws.Id && wm.UserId == userId && wm.IsActive, ct);

        var myRoleRef = wsMember?.Role != null
            ? new RoleRefDto
            {
                RoleId = wsMember.Role.Id,
                Code = wsMember.Role.Code,
                Name = wsMember.Role.Name,
                Scope = wsMember.Role.Scope
            }
            : null;

        if (companyMember.Role.Code == CompanyRoleCodes.TenantAdmin)
        {
            return new WorkspaceAccessContext(
                ws,
                new HashSet<string>(PermissionKeys.AllWorkspaceKeys, StringComparer.Ordinal),
                myRoleRef);
        }

        if (wsMember != null)
        {
            var perms = wsMember.Role != null && wsMember.Role.IsActive
                ? wsMember.Role.RolePermissions
                    .Where(rp => rp.Permission.IsActive)
                    .Select(rp => rp.Permission.Code)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            return new WorkspaceAccessContext(ws, perms, myRoleRef);
        }

        var hasViewAll = companyMember.Role.IsActive &&
            companyMember.Role.RolePermissions.Any(rp => rp.Permission.IsActive && rp.Permission.Code == PermissionKeys.WorkspaceViewAll);

        if (hasViewAll)
        {
            return new WorkspaceAccessContext(
                ws,
                new HashSet<string>(new[] { PermissionKeys.WorkspaceMemberView, PermissionKeys.ProjectViewAll }, StringComparer.Ordinal),
                null);
        }

        return null;
    }

    public async Task<ProjectAccessContext?> GetProjectAccessAsync(long userId, long projectId, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            return null;

        var wsAccess = await EvaluateWorkspaceAccessAsync(userId, project.Workspace, ct);
        if (wsAccess == null)
            return null;

        var pm = await _db.ProjectMembers
            .AsNoTracking()
            .Include(m => m.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(m => m.ProjectId == project.Id && m.UserId == userId && m.IsActive, ct);

        var myRoleRef = pm?.Role != null
            ? new RoleRefDto
            {
                RoleId = pm.Role.Id,
                Code = pm.Role.Code,
                Name = pm.Role.Name,
                Scope = pm.Role.Scope
            }
            : null;

        bool isTenantAdmin = false;
        if (project.Workspace.CompanyId.HasValue)
        {
            isTenantAdmin = await _db.CompanyMembers
                .AsNoTracking()
                .AnyAsync(cm =>
                    cm.CompanyId == project.Workspace.CompanyId.Value &&
                    cm.UserId == userId &&
                    cm.IsActive &&
                    cm.Role.Code == CompanyRoleCodes.TenantAdmin, ct);
        }

        var isPersonalOwner = project.Workspace.CompanyId == null && project.Workspace.CreatedBy == userId;
        var isWorkspaceAdmin = wsAccess.MyRole?.Code == CompanyRoleCodes.WorkspaceAdmin;

        if (isPersonalOwner || isTenantAdmin || isWorkspaceAdmin)
        {
            return new ProjectAccessContext(
                project,
                project.Workspace,
                new HashSet<string>(PermissionKeys.AllProjectKeys, StringComparer.Ordinal),
                myRoleRef);
        }

        if (pm != null)
        {
            var perms = pm.Role.IsActive
                ? pm.Role.RolePermissions
                    .Where(rp => rp.Permission.IsActive)
                    .Select(rp => rp.Permission.Code)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            return new ProjectAccessContext(project, project.Workspace, perms, myRoleRef);
        }

        if (wsAccess.Permissions.Contains(PermissionKeys.ProjectViewAll))
        {
            return new ProjectAccessContext(
                project,
                project.Workspace,
                new HashSet<string>(new[] { PermissionKeys.ProjectMemberView }, StringComparer.Ordinal),
                null);
        }

        return null;
    }

    public async Task<ServiceResult<HashSet<string>>> RequireTenantAsync(
        long userId,
        long companyId,
        string? requiredPermission = null,
        CancellationToken ct = default)
    {
        var perms = await GetTenantPermissionsAsync(userId, companyId, ct);
        if (perms == null)
            return ServiceResult<HashSet<string>>.Forbidden("NOT_COMPANY_MEMBER", "Bạn không thuộc công ty này.");

        if (!string.IsNullOrEmpty(requiredPermission) && !perms.Contains(requiredPermission))
            return ServiceResult<HashSet<string>>.ForbiddenPermission(requiredPermission);

        return ServiceResult<HashSet<string>>.Ok(perms);
    }

    public async Task<ServiceResult<WorkspaceAccessContext>> RequireWorkspaceAsync(
        long userId,
        long workspaceId,
        string? requiredPermission = null,
        CancellationToken ct = default)
    {
        var access = await GetWorkspaceAccessAsync(userId, workspaceId, ct);
        if (access == null)
            return ServiceResult<WorkspaceAccessContext>.NotFound("Không tìm thấy dữ liệu.");

        if (!string.IsNullOrEmpty(requiredPermission) && !access.Permissions.Contains(requiredPermission))
            return ServiceResult<WorkspaceAccessContext>.ForbiddenPermission(requiredPermission);

        return ServiceResult<WorkspaceAccessContext>.Ok(access);
    }

    public async Task<ServiceResult<ProjectAccessContext>> RequireProjectAsync(
        long userId,
        long projectId,
        string? requiredPermission = null,
        CancellationToken ct = default)
    {
        var access = await GetProjectAccessAsync(userId, projectId, ct);
        if (access == null)
            return ServiceResult<ProjectAccessContext>.NotFound("Không tìm thấy dữ liệu.");

        if (!string.IsNullOrEmpty(requiredPermission) && !access.Permissions.Contains(requiredPermission))
            return ServiceResult<ProjectAccessContext>.ForbiddenPermission(requiredPermission);

        return ServiceResult<ProjectAccessContext>.Ok(access);
    }
}
