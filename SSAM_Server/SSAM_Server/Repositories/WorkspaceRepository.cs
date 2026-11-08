using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Workspaces;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Repositories;

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly AppDbContext _db;

    public WorkspaceRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Workspace?> GetByIdAsync(long workspaceId, CancellationToken ct = default)
        => _db.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, ct);

    public async Task<List<Workspace>> ListByCompanyOrPersonalAsync(long? companyId, long userId, CancellationToken ct = default)
    {
        if (companyId == null)
        {
            return await _db.Workspaces
                .AsNoTracking()
                .Where(w => w.CompanyId == null && w.CreatedBy == userId)
                .OrderByDescending(w => w.UpdatedAt)
                .ToListAsync(ct);
        }

        return await _db.Workspaces
            .AsNoTracking()
            .Where(w => w.CompanyId == companyId.Value)
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync(ct);
    }

    public Task<bool> PersonalWorkspaceExistsAsync(long userId, CancellationToken ct = default)
        => _db.Workspaces.AsNoTracking().AnyAsync(w => w.CompanyId == null && w.CreatedBy == userId, ct);

    public Task<bool> CodeExistsAsync(long companyId, string code, long? excludeWorkspaceId = null, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToLowerInvariant();
        return _db.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.CompanyId == companyId && w.Id != excludeWorkspaceId && w.Code == normalized, ct);
    }

    public async Task AddWorkspaceAsync(Workspace workspace, CancellationToken ct = default)
        => await _db.Workspaces.AddAsync(workspace, ct);

    public Task<bool> IsMemberAsync(long workspaceId, long userId, CancellationToken ct = default)
        => _db.WorkspaceMembers.AsNoTracking().AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

    public Task<WorkspaceMember?> GetActiveMemberAsync(long workspaceId, long userId, CancellationToken ct = default)
        => _db.WorkspaceMembers
            .Include(wm => wm.Role)
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId && wm.IsActive, ct);

    public Task<WorkspaceMember?> GetMemberRecordAsync(long workspaceId, long userId, CancellationToken ct = default)
        => _db.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId, ct);

    public async Task AddMemberAsync(WorkspaceMember member, CancellationToken ct = default)
        => await _db.WorkspaceMembers.AddAsync(member, ct);

    public Task<int> CountActiveWorkspaceAdminsAsync(long workspaceId, CancellationToken ct = default)
        => _db.WorkspaceMembers
            .AsNoTracking()
            .CountAsync(wm => wm.WorkspaceId == workspaceId && wm.IsActive && wm.Role != null && wm.Role.Code == CompanyRoleCodes.WorkspaceAdmin, ct);

    public async Task<List<WorkspaceMemberDto>> ListMembersAsync(
        long workspaceId,
        long companyId,
        long? roleId,
        string? search,
        CancellationToken ct = default)
    {
        var query = _db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.User)
            .Include(wm => wm.Role)
            .Where(wm => wm.WorkspaceId == workspaceId && wm.IsActive);

        if (roleId.HasValue)
        {
            query = query.Where(wm => wm.RoleId == roleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(wm => wm.User.FullName.ToLower().Contains(term) || wm.User.Email.ToLower().Contains(term));
        }

        var members = await query.OrderBy(wm => wm.JoinedAt).ToListAsync(ct);
        var userIds = members.Select(m => m.UserId).ToList();

        var companyRoles = await _db.CompanyMembers
            .AsNoTracking()
            .Include(cm => cm.Role)
            .Where(cm => cm.CompanyId == companyId && userIds.Contains(cm.UserId))
            .ToDictionaryAsync(cm => cm.UserId, cm => cm.Role, ct);

        return members.Select(wm => MapMemberDto(wm, companyRoles.GetValueOrDefault(wm.UserId))).ToList();
    }

    public async Task<WorkspaceMemberDto?> GetMemberDtoAsync(long workspaceId, long companyId, long userId, CancellationToken ct = default)
    {
        var wm = await _db.WorkspaceMembers
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.UserId == userId && x.IsActive, ct);

        if (wm == null)
            return null;

        var cm = await _db.CompanyMembers
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == userId, ct);

        return MapMemberDto(wm, cm?.Role);
    }

    public async Task<List<UserRefDto>> ListMemberCandidatesAsync(
        long workspaceId,
        long companyId,
        string? search,
        CancellationToken ct = default)
    {
        var existingUserIds = _db.WorkspaceMembers
            .AsNoTracking()
            .Where(wm => wm.WorkspaceId == workspaceId && wm.IsActive)
            .Select(wm => wm.UserId);

        var query = _db.CompanyMembers
            .AsNoTracking()
            .Where(cm => cm.CompanyId == companyId && cm.IsActive && !existingUserIds.Contains(cm.UserId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(cm => cm.User.FullName.ToLower().Contains(term) || cm.User.Email.ToLower().Contains(term));
        }

        return await query
            .OrderBy(cm => cm.User.FullName)
            .Take(20)
            .Select(cm => new UserRefDto
            {
                UserId = cm.UserId,
                FullName = cm.User.FullName,
                Email = cm.User.Email,
                AvatarUrl = cm.User.AvatarUrl
            })
            .ToListAsync(ct);
    }

    public async Task<string?> FindSoleProjectManagerProjectKeyAsync(long workspaceId, long userId, CancellationToken ct = default)
    {
        var userPmProjectIds = await _db.ProjectMembers
            .AsNoTracking()
            .Where(pm =>
                pm.UserId == userId &&
                pm.IsActive &&
                pm.Project.WorkspaceId == workspaceId &&
                pm.Role.Code == CompanyRoleCodes.ProjectManager)
            .Select(pm => new { pm.ProjectId, pm.Project.Key })
            .ToListAsync(ct);

        foreach (var item in userPmProjectIds)
        {
            var pmCount = await _db.ProjectMembers
                .AsNoTracking()
                .CountAsync(pm =>
                    pm.ProjectId == item.ProjectId &&
                    pm.IsActive &&
                    pm.Role.Code == CompanyRoleCodes.ProjectManager, ct);

            if (pmCount <= 1)
                return item.Key;
        }

        return null;
    }

    public async Task DeactivateProjectMembershipsInWorkspaceAsync(long workspaceId, long userId, CancellationToken ct = default)
    {
        var memberships = await _db.ProjectMembers
            .Where(pm => pm.UserId == userId && pm.IsActive && pm.Project.WorkspaceId == workspaceId)
            .ToListAsync(ct);

        foreach (var pm in memberships)
        {
            pm.IsActive = false;
        }
    }

    public async Task<WorkspaceDto> BuildWorkspaceDtoAsync(
        Workspace ws,
        long viewerId,
        RoleRefDto? myRole,
        IEnumerable<string> myPermissions,
        CancellationToken ct = default)
    {
        var creator = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == ws.CreatedBy)
            .Select(u => new UserRefDto
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl
            })
            .FirstOrDefaultAsync(ct) ?? new UserRefDto { UserId = ws.CreatedBy, FullName = "Unknown", Email = "" };

        var projectCount = await _db.Projects
            .AsNoTracking()
            .CountAsync(p => p.WorkspaceId == ws.Id && p.IsActive, ct);

        var memberCount = ws.CompanyId == null
            ? 1
            : await _db.WorkspaceMembers
                .AsNoTracking()
                .CountAsync(wm => wm.WorkspaceId == ws.Id && wm.IsActive, ct);

        return new WorkspaceDto
        {
            WorkspaceId = ws.Id,
            WorkspaceName = ws.Name,
            Name = ws.Name,
            Code = ws.Code,
            Description = ws.Description,
            CompanyId = ws.CompanyId,
            Status = ws.IsActive ? "ACTIVE" : "ARCHIVED",
            MyRole = myRole,
            MyPermissions = myPermissions.ToList(),
            ProjectCount = projectCount,
            MemberCount = memberCount,
            CreatedBy = creator,
            CreatedAt = ws.CreatedAt,
            UpdatedAt = ws.UpdatedAt,
            ArchivedAt = ws.ArchivedAt
        };
    }

    private static WorkspaceMemberDto MapMemberDto(WorkspaceMember wm, CompanyRole? companyRole)
    {
        return new WorkspaceMemberDto
        {
            UserId = wm.UserId,
            FullName = wm.User.FullName,
            Email = wm.User.Email,
            AvatarUrl = wm.User.AvatarUrl,
            Role = wm.Role != null
                ? new RoleRefDto
                {
                    RoleId = wm.Role.Id,
                    Code = wm.Role.Code,
                    Name = wm.Role.Name,
                    Scope = wm.Role.Scope,
                    Kind = wm.Role.Kind
                }
                : new RoleRefDto { RoleId = 0, Code = null, Name = "—", Scope = RoleScopes.Workspace, Kind = RoleKinds.Other },
            CompanyRole = companyRole != null
                ? new RoleRefDto
                {
                    RoleId = companyRole.Id,
                    Code = companyRole.Code,
                    Name = companyRole.Name,
                    Scope = companyRole.Scope,
                    Kind = companyRole.Kind
                }
                : new RoleRefDto { RoleId = 0, Code = null, Name = "—", Scope = RoleScopes.Tenant, Kind = RoleKinds.Other },
            JoinedAt = wm.JoinedAt
        };
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(work, ct);
}
