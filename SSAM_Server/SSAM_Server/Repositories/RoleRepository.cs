using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Roles;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _db;

    public RoleRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CompanyRole>> ListRolesAsync(long companyId, string? scope, bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.CompanyRoles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => r.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(scope))
        {
            var normalizedScope = scope.Trim().ToUpperInvariant();
            query = query.Where(r => r.Scope == normalizedScope);
        }

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);
    }

    public Task<CompanyRole?> GetRoleByIdAsync(long companyId, long roleId, CancellationToken ct = default)
        => _db.CompanyRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.CompanyId == companyId, ct);

    public Task<bool> RoleNameExistsAsync(long companyId, string name, long? excludeRoleId = null, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return _db.CompanyRoles
            .AsNoTracking()
            .AnyAsync(r => r.CompanyId == companyId && r.Id != excludeRoleId && r.Name.ToLower() == normalized, ct);
    }

    public Task<bool> RoleCodeExistsAsync(long companyId, string code, long? excludeRoleId = null, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.CompanyRoles
            .AsNoTracking()
            .AnyAsync(r => r.CompanyId == companyId && r.Id != excludeRoleId && r.Code == normalized, ct);
    }

    public Task<List<Permission>> GetPermissionsByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var codeList = codes.Distinct().ToList();
        return _db.Permissions
            .AsNoTracking()
            .Where(p => p.IsActive && codeList.Contains(p.Code))
            .ToListAsync(ct);
    }

    public async Task<Dictionary<long, int>> GetMemberCountsAsync(long companyId, IEnumerable<CompanyRole> roles, CancellationToken ct = default)
    {
        var roleList = roles.ToList();
        var counts = roleList.ToDictionary(r => r.Id, _ => 0);

        var tenantRoleIds = roleList.Where(r => r.Scope == RoleScopes.Tenant).Select(r => r.Id).ToList();
        if (tenantRoleIds.Count > 0)
        {
            var tenantCounts = await _db.CompanyMembers
                .AsNoTracking()
                .Where(cm => cm.CompanyId == companyId && cm.IsActive && tenantRoleIds.Contains(cm.RoleId))
                .GroupBy(cm => cm.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var item in tenantCounts)
                counts[item.RoleId] = item.Count;
        }

        var wsRoleIds = roleList.Where(r => r.Scope == RoleScopes.Workspace).Select(r => r.Id).ToList();
        if (wsRoleIds.Count > 0)
        {
            var wsCounts = await _db.WorkspaceMembers
                .AsNoTracking()
                .Where(wm => wm.IsActive && wm.RoleId.HasValue && wsRoleIds.Contains(wm.RoleId.Value) && wm.Workspace.CompanyId == companyId)
                .GroupBy(wm => wm.RoleId!.Value)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var item in wsCounts)
                counts[item.RoleId] = item.Count;
        }

        var projRoleIds = roleList.Where(r => r.Scope == RoleScopes.Project).Select(r => r.Id).ToList();
        if (projRoleIds.Count > 0)
        {
            var projCounts = await _db.ProjectMembers
                .AsNoTracking()
                .Where(pm => pm.IsActive && projRoleIds.Contains(pm.RoleId))
                .GroupBy(pm => pm.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var item in projCounts)
                counts[item.RoleId] = item.Count;
        }

        return counts;
    }

    public async Task<int> GetSingleRoleMemberCountAsync(CompanyRole role, CancellationToken ct = default)
    {
        var dict = await GetMemberCountsAsync(role.CompanyId, new[] { role }, ct);
        return dict.GetValueOrDefault(role.Id, 0);
    }

    public async Task<List<RoleMemberDto>> GetRoleMembersAsync(CompanyRole role, CancellationToken ct = default)
    {
        if (role.Scope == RoleScopes.Tenant)
        {
            return await _db.CompanyMembers
                .AsNoTracking()
                .Where(cm => cm.RoleId == role.Id && cm.IsActive)
                .Select(cm => new RoleMemberDto
                {
                    UserId = cm.UserId,
                    FullName = cm.User.FullName,
                    Email = cm.User.Email,
                    Workspace = null
                })
                .ToListAsync(ct);
        }

        if (role.Scope == RoleScopes.Project)
        {
            return await _db.ProjectMembers
                .AsNoTracking()
                .Where(pm => pm.RoleId == role.Id && pm.IsActive)
                .Select(pm => new RoleMemberDto
                {
                    UserId = pm.UserId,
                    FullName = pm.User.FullName,
                    Email = pm.User.Email,
                    Workspace = new RoleMemberWorkspaceRefDto
                    {
                        WorkspaceId = pm.Project.WorkspaceId,
                        Name = pm.Project.Workspace.Name
                    },
                    Project = new RoleMemberProjectRefDto
                    {
                        ProjectId = pm.ProjectId,
                        Key = pm.Project.Key,
                        Name = pm.Project.Name
                    }
                })
                .ToListAsync(ct);
        }

        return await _db.WorkspaceMembers
            .AsNoTracking()
            .Where(wm => wm.RoleId == role.Id && wm.IsActive)
            .Select(wm => new RoleMemberDto
            {
                UserId = wm.UserId,
                FullName = wm.User.FullName,
                Email = wm.User.Email,
                Workspace = new RoleMemberWorkspaceRefDto
                {
                    WorkspaceId = wm.WorkspaceId,
                    Name = wm.Workspace.Name
                }
            })
            .ToListAsync(ct);
    }

    public async Task AddRoleAsync(CompanyRole role, CancellationToken ct = default)
        => await _db.CompanyRoles.AddAsync(role, ct);

    public async Task ReplaceRolePermissionsAsync(long roleId, IEnumerable<long> permissionIds, CancellationToken ct = default)
    {
        var existing = await _db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            _db.RolePermissions.RemoveRange(existing);
        }

        var distinctIds = permissionIds.Distinct().ToList();
        if (distinctIds.Count > 0)
        {
            var newRows = distinctIds.Select(pid => new RolePermission
            {
                RoleId = roleId,
                PermissionId = pid
            });
            await _db.RolePermissions.AddRangeAsync(newRows, ct);
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(work, ct);
}
