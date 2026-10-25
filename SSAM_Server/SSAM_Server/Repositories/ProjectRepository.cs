using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _db;

    public ProjectRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Project?> GetByIdAsync(long projectId, CancellationToken ct = default)
        => _db.Projects
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

    public async Task<List<Project>> ListByWorkspaceIdsAsync(IEnumerable<long> workspaceIds, CancellationToken ct = default)
    {
        var idList = workspaceIds.Distinct().ToList();
        if (idList.Count == 0)
            return new List<Project>();

        return await _db.Projects
            .AsNoTracking()
            .Include(p => p.Workspace)
            .Where(p => idList.Contains(p.WorkspaceId))
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
    }

    public Task<bool> KeyExistsAsync(long workspaceId, string key, CancellationToken ct = default)
    {
        var upper = key.Trim().ToUpperInvariant();
        return _db.Projects.AsNoTracking().AnyAsync(p => p.WorkspaceId == workspaceId && p.Key == upper, ct);
    }

    public async Task AddProjectAsync(Project project, CancellationToken ct = default)
        => await _db.Projects.AddAsync(project, ct);

    public Task<bool> IsActiveMemberAsync(long projectId, long userId, CancellationToken ct = default)
        => _db.ProjectMembers.AsNoTracking().AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive, ct);

    public Task<ProjectMember?> GetActiveMemberAsync(long projectId, long userId, CancellationToken ct = default)
        => _db.ProjectMembers
            .Include(pm => pm.Role)
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive, ct);

    public Task<ProjectMember?> GetMemberRecordAsync(long projectId, long userId, CancellationToken ct = default)
        => _db.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId, ct);

    public async Task AddProjectMemberAsync(ProjectMember member, CancellationToken ct = default)
        => await _db.ProjectMembers.AddAsync(member, ct);

    public Task<int> CountActiveProjectManagersAsync(long projectId, CancellationToken ct = default)
        => _db.ProjectMembers
            .AsNoTracking()
            .CountAsync(pm => pm.ProjectId == projectId && pm.IsActive && pm.Role.Code == CompanyRoleCodes.ProjectManager, ct);

    public async Task<List<ProjectMemberDto>> ListMembersAsync(long projectId, long workspaceId, CancellationToken ct = default)
    {
        var members = await _db.ProjectMembers
            .AsNoTracking()
            .Include(pm => pm.User)
            .Include(pm => pm.Role)
            .Where(pm => pm.ProjectId == projectId && pm.IsActive)
            .OrderBy(pm => pm.JoinedAt)
            .ToListAsync(ct);

        var userIds = members.Select(m => m.UserId).ToList();
        var wsRoles = await _db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.Role)
            .Where(wm => wm.WorkspaceId == workspaceId && wm.IsActive && userIds.Contains(wm.UserId))
            .ToDictionaryAsync(wm => wm.UserId, wm => wm.Role, ct);

        return members.Select(pm => MapMemberDto(pm, wsRoles.GetValueOrDefault(pm.UserId))).ToList();
    }

    public async Task<ProjectMemberDto?> GetMemberDtoAsync(long projectId, long workspaceId, long userId, CancellationToken ct = default)
    {
        var pm = await _db.ProjectMembers
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId && x.IsActive, ct);

        if (pm == null)
            return null;

        var wm = await _db.WorkspaceMembers
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.UserId == userId && x.IsActive, ct);

        return MapMemberDto(pm, wm?.Role);
    }

    public async Task<List<ProjectMemberCandidateDto>> ListMemberCandidatesAsync(
        long projectId,
        long workspaceId,
        string? search,
        CancellationToken ct = default)
    {
        var existingUserIds = _db.ProjectMembers
            .AsNoTracking()
            .Where(pm => pm.ProjectId == projectId && pm.IsActive)
            .Select(pm => pm.UserId);

        var query = _db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.User)
            .Include(wm => wm.Role)
            .Where(wm => wm.WorkspaceId == workspaceId && wm.IsActive && !existingUserIds.Contains(wm.UserId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(wm => wm.User.FullName.ToLower().Contains(term) || wm.User.Email.ToLower().Contains(term));
        }

        var rows = await query
            .OrderBy(wm => wm.User.FullName)
            .Take(20)
            .ToListAsync(ct);

        return rows.Select(wm => new ProjectMemberCandidateDto
        {
            UserId = wm.UserId,
            FullName = wm.User.FullName,
            Email = wm.User.Email,
            AvatarUrl = wm.User.AvatarUrl,
            WorkspaceRole = wm.Role != null
                ? new RoleRefDto
                {
                    RoleId = wm.Role.Id,
                    Code = wm.Role.Code,
                    Name = wm.Role.Name,
                    Scope = wm.Role.Scope
                }
                : new RoleRefDto { RoleId = 0, Code = null, Name = "—", Scope = RoleScopes.Workspace }
        }).ToList();
    }

    public Task<List<ProjectPhase>> ListPhasesByProjectAsync(long projectId, CancellationToken ct = default)
        => _db.ProjectPhases
            .Where(pp => pp.ProjectId == projectId)
            .OrderBy(pp => pp.Position)
            .ToListAsync(ct);

    public Task<ProjectPhase?> GetPhaseByIdAsync(long projectId, long phaseId, CancellationToken ct = default)
        => _db.ProjectPhases.FirstOrDefaultAsync(pp => pp.Id == phaseId && pp.ProjectId == projectId, ct);

    public async Task AddPhaseAsync(ProjectPhase phase, CancellationToken ct = default)
        => await _db.ProjectPhases.AddAsync(phase, ct);

    public Task<List<WorkflowStatus>> ListStatusesByPhaseAsync(long phaseId, CancellationToken ct = default)
        => _db.WorkflowStatuses
            .Where(ws => ws.PhaseId == phaseId)
            .orderByPosition()
            .ToListAsync(ct);

    public async Task WritePhaseWorkflowAsync(
        long projectId,
        long phaseId,
        IReadOnlyList<StatusInputDto> statuses,
        IReadOnlyList<TransitionInputDto> transitions,
        CancellationToken ct = default)
    {
        var existingTransitions = await _db.WorkflowTransitions
            .Where(wt => wt.PhaseId == phaseId)
            .ToListAsync(ct);

        if (existingTransitions.Count > 0)
        {
            _db.WorkflowTransitions.RemoveRange(existingTransitions);
            await _db.SaveChangesAsync(ct);
        }

        var existingStatuses = await _db.WorkflowStatuses
            .Where(ws => ws.PhaseId == phaseId)
            .ToListAsync(ct);

        var keepIds = statuses
            .Where(s => s.StatusId.HasValue)
            .Select(s => s.StatusId!.Value)
            .ToHashSet();

        var toDelete = existingStatuses.Where(s => !keepIds.Contains(s.Id)).ToList();
        if (toDelete.Count > 0)
        {
            _db.WorkflowStatuses.RemoveRange(toDelete);
            await _db.SaveChangesAsync(ct);
        }

        var idByKey = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int i = 0; i < statuses.Count; i++)
        {
            var s = statuses[i];
            var trimmedName = s.Name.Trim();
            var description = string.IsNullOrWhiteSpace(s.Description) ? null : s.Description.Trim();
            var isActive = s.IsActive != false;

            if (s.StatusId.HasValue)
            {
                var tracked = existingStatuses.First(x => x.Id == s.StatusId.Value);
                tracked.Name = trimmedName;
                tracked.Category = s.Category;
                tracked.Color = s.Color;
                tracked.Description = description;
                tracked.IsInitial = s.IsInitial;
                tracked.Position = i + 1;
                tracked.IsActive = isActive;
                await _db.SaveChangesAsync(ct);
                idByKey[tracked.Id.ToString()] = tracked.Id;
            }
            else
            {
                var created = new WorkflowStatus
                {
                    ProjectId = projectId,
                    PhaseId = phaseId,
                    Name = trimmedName,
                    Category = s.Category,
                    Color = s.Color,
                    Description = description,
                    IsInitial = s.IsInitial,
                    Position = i + 1,
                    IsActive = isActive
                };
                await _db.WorkflowStatuses.AddAsync(created, ct);
                await _db.SaveChangesAsync(ct);
                idByKey[s.EffectiveKey] = created.Id;
            }
        }

        foreach (var t in transitions)
        {
            var fromId = idByKey[t.FromKey];
            var toId = idByKey[t.ToKey];
            var transition = new WorkflowTransition
            {
                ProjectId = projectId,
                PhaseId = phaseId,
                FromStatusId = fromId,
                ToStatusId = toId,
                RequireComment = t.RequireComment,
                CommentHint = t.RequireComment && !string.IsNullOrWhiteSpace(t.CommentHint) ? t.CommentHint.Trim() : null,
                Condition = string.IsNullOrWhiteSpace(t.Condition) ? null : t.Condition.Trim()
            };
            await _db.WorkflowTransitions.AddAsync(transition, ct);
            await _db.SaveChangesAsync(ct);

            var roleIds = (t.AllowedRoleIds ?? new List<long>()).Distinct().ToList();
            if (roleIds.Count > 0)
            {
                var trRoles = roleIds.Select(rid => new WorkflowTransitionRole
                {
                    TransitionId = transition.Id,
                    RoleId = rid
                });
                await _db.WorkflowTransitionRoles.AddRangeAsync(trRoles, ct);
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    public Task<List<long>> GetActiveProjectRoleIdsAsync(long companyId, CancellationToken ct = default)
        => _db.CompanyRoles
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.Scope == RoleScopes.Project && r.IsActive)
            .Select(r => r.Id)
            .ToListAsync(ct);

    public async Task<ProjectDto> BuildProjectDtoAsync(
        Project project,
        Workspace ws,
        long viewerId,
        RoleRefDto? myRole,
        IEnumerable<string> myPermissions,
        CancellationToken ct = default)
    {
        var creator = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == project.CreatedBy)
            .Select(u => new UserRefDto
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl
            })
            .FirstOrDefaultAsync(ct) ?? new UserRefDto { UserId = project.CreatedBy, FullName = "Unknown", Email = "" };

        var pms = await _db.ProjectMembers
            .AsNoTracking()
            .Include(pm => pm.User)
            .Where(pm => pm.ProjectId == project.Id && pm.IsActive && pm.Role.Code == CompanyRoleCodes.ProjectManager)
            .OrderBy(pm => pm.JoinedAt)
            .ToListAsync(ct);

        var leadMember = pms.FirstOrDefault(pm => pm.UserId == project.CreatedBy) ?? pms.FirstOrDefault();
        UserRefDto? leadRef = leadMember != null
            ? new UserRefDto
            {
                UserId = leadMember.UserId,
                FullName = leadMember.User.FullName,
                Email = leadMember.User.Email,
                AvatarUrl = leadMember.User.AvatarUrl
            }
            : null;

        var memberCount = await _db.ProjectMembers
            .AsNoTracking()
            .CountAsync(pm => pm.ProjectId == project.Id && pm.IsActive, ct);

        var phaseCount = await _db.ProjectPhases
            .AsNoTracking()
            .CountAsync(pp => pp.ProjectId == project.Id && pp.IsActive, ct);

        var effectiveStatus = (!ws.IsActive || !project.IsActive) ? "ARCHIVED" : "ACTIVE";

        return new ProjectDto
        {
            ProjectId = project.Id,
            WorkspaceId = ws.Id,
            WorkspaceName = ws.Name,
            CompanyId = ws.CompanyId,
            Key = project.Key,
            Name = project.Name,
            Description = project.Description,
            SdlcMethod = project.SdlcMethod,
            AiEnabled = project.AiEnabled,
            Status = effectiveStatus,
            Lead = leadRef,
            MyRole = myRole,
            MyPermissions = myPermissions.ToList(),
            MemberCount = memberCount,
            PhaseCount = phaseCount,
            OpenIssueCount = 0,
            DoneIssueCount = 0,
            DocumentCount = 0,
            CreatedBy = creator,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            ArchivedAt = project.ArchivedAt
        };
    }

    public async Task<PhaseDto> BuildPhaseDtoAsync(long phaseId, CancellationToken ct = default)
    {
        var phase = await _db.ProjectPhases
            .AsNoTracking()
            .FirstAsync(pp => pp.Id == phaseId, ct);

        var statuses = await _db.WorkflowStatuses
            .AsNoTracking()
            .Where(ws => ws.PhaseId == phaseId)
            .OrderBy(ws => ws.Position)
            .Select(ws => new WorkflowStatusDto
            {
                StatusId = ws.Id,
                Name = ws.Name,
                Category = ws.Category,
                Color = ws.Color,
                Description = ws.Description,
                IsInitial = ws.IsInitial,
                Position = ws.Position,
                IsActive = ws.IsActive,
                IssueCount = 0
            })
            .ToListAsync(ct);

        var transitions = await _db.WorkflowTransitions
            .AsNoTracking()
            .Include(wt => wt.AllowedRoles)
                .ThenInclude(wtr => wtr.Role)
            .Where(wt => wt.PhaseId == phaseId)
            .ToListAsync(ct);

        var transitionDtos = transitions.Select(wt => new WorkflowTransitionDto
        {
            TransitionId = wt.Id,
            FromStatusId = wt.FromStatusId,
            ToStatusId = wt.ToStatusId,
            AllowedRoles = wt.AllowedRoles
                .Where(ar => ar.Role != null)
                .Select(ar => new RoleRefDto
                {
                    RoleId = ar.Role.Id,
                    Code = ar.Role.Code,
                    Name = ar.Role.Name,
                    Scope = ar.Role.Scope
                })
                .ToList(),
            RequireComment = wt.RequireComment,
            CommentHint = wt.CommentHint,
            Condition = wt.Condition
        }).ToList();

        return new PhaseDto
        {
            PhaseId = phase.Id,
            Name = phase.Name,
            Description = phase.Description,
            Position = phase.Position,
            IsActive = phase.IsActive,
            Statuses = statuses,
            Transitions = transitionDtos
        };
    }

    public async Task<WorkflowDto> BuildWorkflowDtoAsync(Project project, CancellationToken ct = default)
    {
        var phaseIds = await _db.ProjectPhases
            .AsNoTracking()
            .Where(pp => pp.ProjectId == project.Id)
            .OrderBy(pp => pp.Position)
            .Select(pp => pp.Id)
            .ToListAsync(ct);

        var phaseDtos = new List<PhaseDto>();
        foreach (var pid in phaseIds)
        {
            phaseDtos.Add(await BuildPhaseDtoAsync(pid, ct));
        }

        return new WorkflowDto
        {
            ProjectId = project.Id,
            SdlcMethod = project.SdlcMethod,
            PhasesEditable = true,
            Phases = phaseDtos
        };
    }

    private static ProjectMemberDto MapMemberDto(ProjectMember pm, CompanyRole? wsRole)
    {
        return new ProjectMemberDto
        {
            UserId = pm.UserId,
            FullName = pm.User.FullName,
            Email = pm.User.Email,
            AvatarUrl = pm.User.AvatarUrl,
            Role = new RoleRefDto
            {
                RoleId = pm.Role.Id,
                Code = pm.Role.Code,
                Name = pm.Role.Name,
                Scope = pm.Role.Scope
            },
            WorkspaceRole = wsRole != null
                ? new RoleRefDto
                {
                    RoleId = wsRole.Id,
                    Code = wsRole.Code,
                    Name = wsRole.Name,
                    Scope = wsRole.Scope
                }
                : new RoleRefDto { RoleId = 0, Code = null, Name = "—", Scope = RoleScopes.Workspace },
            JoinedAt = pm.JoinedAt
        };
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(work, ct);
}

internal static class WorkflowStatusQueryExtensions
{
    public static IOrderedQueryable<WorkflowStatus> orderByPosition(this IQueryable<WorkflowStatus> query)
        => query.OrderBy(ws => ws.Position);
}
