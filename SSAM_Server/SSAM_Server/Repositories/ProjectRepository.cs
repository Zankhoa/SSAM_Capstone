using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;
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

        var companyId = await _db.Workspaces
            .AsNoTracking()
            .Where(w => w.Id == workspaceId)
            .Select(w => w.CompanyId)
            .FirstOrDefaultAsync(ct);

        var depts = companyId.HasValue
            ? await _db.CompanyMembers
                .AsNoTracking()
                .Include(cm => cm.Department)
                .Where(cm => cm.CompanyId == companyId.Value && userIds.Contains(cm.UserId) && cm.Department != null)
                .ToDictionaryAsync(cm => cm.UserId, cm => cm.Department, ct)
            : new Dictionary<long, Department?>();

        return members.Select(pm => MapMemberDto(pm, wsRoles.GetValueOrDefault(pm.UserId), depts.GetValueOrDefault(pm.UserId))).ToList();
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

        var companyId = await _db.Workspaces
            .AsNoTracking()
            .Where(w => w.Id == workspaceId)
            .Select(w => w.CompanyId)
            .FirstOrDefaultAsync(ct);

        var department = companyId.HasValue
            ? await _db.CompanyMembers
                .AsNoTracking()
                .Include(cm => cm.Department)
                .Where(cm => cm.CompanyId == companyId.Value && cm.UserId == userId)
                .Select(cm => cm.Department)
                .FirstOrDefaultAsync(ct)
            : null;

        return MapMemberDto(pm, wm?.Role, department);
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
                    Scope = wm.Role.Scope,
                    Kind = wm.Role.Kind
                }
                : new RoleRefDto { RoleId = 0, Code = null, Name = "—", Scope = RoleScopes.Workspace, Kind = RoleKinds.Other }
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

    public async Task AddPhaseLogAsync(ProjectPhaseLog log, CancellationToken ct = default)
        => await _db.ProjectPhaseLogs.AddAsync(log, ct);

    public async Task<List<PhaseUnmetCriterionDto>> EvaluatePhaseGatesAsync(
        long projectId,
        long phaseId,
        IReadOnlyList<string> exitCriteria,
        CancellationToken ct = default)
    {
        var unmet = new List<PhaseUnmetCriterionDto>();
        if (exitCriteria.Count == 0)
            return unmet;

        foreach (var criterion in exitCriteria)
        {
            switch (criterion)
            {
                case "DOC_APPROVED":
                {
                    var approvedCount = await _db.DocumentNodes
                        .AsNoTracking()
                        .CountAsync(d => d.ProjectId == projectId &&
                                         !d.IsDeleted &&
                                         d.Kind == DocumentNodeKinds.Page &&
                                         d.BaselineVersionId != null, ct);
                    if (approvedCount == 0)
                    {
                        unmet.Add(new PhaseUnmetCriterionDto
                        {
                            Criterion = criterion,
                            Detail = "Chưa có tài liệu nào được phê duyệt baseline"
                        });
                    }
                    break;
                }
                case "REQ_SEGMENTED":
                {
                    var docs = await _db.DocumentNodes
                        .AsNoTracking()
                        .Where(d => d.ProjectId == projectId && !d.IsDeleted && d.Kind == DocumentNodeKinds.Page)
                        .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .Distinct()
                        .ToListAsync(ct);

                    var reqCount = docs.Count == 0
                        ? 0
                        : await _db.RequirementItems
                            .AsNoTracking()
                            .CountAsync(r => docs.Contains(r.VersionId), ct);

                    if (reqCount == 0)
                    {
                        unmet.Add(new PhaseUnmetCriterionDto
                        {
                            Criterion = criterion,
                            Detail = "Chưa có khối REQ-* nào trong tài liệu dự án"
                        });
                    }
                    break;
                }
                case "REQ_LINKED_ISSUES":
                {
                    var docs = await _db.DocumentNodes
                        .AsNoTracking()
                        .Where(d => d.ProjectId == projectId && !d.IsDeleted && d.Kind == DocumentNodeKinds.Page)
                        .Select(d => new { d.Id, VerId = d.BaselineVersionId ?? d.CurrentVersionId })
                        .Where(x => x.VerId.HasValue)
                        .ToListAsync(ct);

                    var verIds = docs.Select(x => x.VerId!.Value).Distinct().ToList();
                    var activeReqs = verIds.Count == 0
                        ? new List<(long DocId, string ReqUid)>()
                        : (await _db.RequirementItems
                            .AsNoTracking()
                            .Where(r => verIds.Contains(r.VersionId))
                            .Select(r => new { r.DocumentId, r.ReqUid })
                            .ToListAsync(ct))
                            .Select(r => (r.DocumentId, r.ReqUid.ToUpperInvariant()))
                            .Distinct()
                            .ToList();

                    if (activeReqs.Count == 0)
                    {
                        unmet.Add(new PhaseUnmetCriterionDto
                        {
                            Criterion = criterion,
                            Detail = "Chưa có yêu cầu nào để đối chiếu"
                        });
                        break;
                    }

                    var linkedIssueReqs = await _db.IssueRequirementLinks
                        .AsNoTracking()
                        .Where(l => !l.Issue.IsDeleted && l.Issue.ProjectId == projectId)
                        .Select(l => new { l.DocumentId, l.ReqUid })
                        .ToListAsync(ct);
                    var linkedSet = linkedIssueReqs
                        .Select(l => (l.DocumentId, l.ReqUid.ToUpperInvariant()))
                        .ToHashSet();

                    var linkedCount = activeReqs.Count(r => linkedSet.Contains(r));
                    if (linkedCount < activeReqs.Count)
                    {
                        unmet.Add(new PhaseUnmetCriterionDto
                        {
                            Criterion = criterion,
                            Detail = $"Còn {activeReqs.Count - linkedCount}/{activeReqs.Count} yêu cầu chưa gắn công việc"
                        });
                    }
                    break;
                }
                case "ISSUES_DONE":
                {
                    var phaseIssues = await _db.Issues
                        .AsNoTracking()
                        .Include(i => i.Status)
                        .Where(i => i.ProjectId == projectId &&
                                    !i.IsDeleted &&
                                    i.ParentId == null &&
                                    i.Status.PhaseId == phaseId)
                        .Select(i => new { IsDone = i.Status.Category == StatusCategories.Done })
                        .ToListAsync(ct);

                    var openCount = phaseIssues.Count(i => !i.IsDone);
                    if (openCount > 0)
                    {
                        unmet.Add(new PhaseUnmetCriterionDto
                        {
                            Criterion = criterion,
                            Detail = $"Còn {openCount}/{phaseIssues.Count} công việc trong giai đoạn chưa hoàn tất"
                        });
                    }
                    break;
                }
                case "TESTS_PASSED":
                {
                    var activeCases = await _db.TestCases
                        .AsNoTracking()
                        .Include(tc => tc.Results)
                        .Include(tc => tc.RetestFlags)
                        .Include(tc => tc.Plan)
                            .ThenInclude(p => p.Environments.Where(e => !e.IsDeleted))
                        .Where(tc => tc.ProjectId == projectId && tc.Status == TestCaseStatuses.Active)
                        .ToListAsync(ct);

                    if (activeCases.Count == 0)
                    {
                        unmet.Add(new PhaseUnmetCriterionDto
                        {
                            Criterion = criterion,
                            Detail = "Chưa có test case nào trong dự án"
                        });
                        break;
                    }

                    int passed = 0, failed = 0, pending = 0;
                    foreach (var tc in activeCases)
                    {
                        var latestEnv = tc.Plan?.Environments
                            .Where(e => !e.IsDeleted)
                            .OrderBy(e => e.Position)
                            .ThenBy(e => e.Id)
                            .LastOrDefault();

                        if (latestEnv == null)
                        {
                            pending++;
                            continue;
                        }

                        var hasFlag = tc.RetestFlags.Any(f => f.EnvId == latestEnv.Id);
                        var latestRes = tc.Results
                            .Where(r => r.EnvId == latestEnv.Id)
                            .OrderByDescending(r => r.At)
                            .ThenByDescending(r => r.Id)
                            .FirstOrDefault()?.Result ?? TestResultValues.Untest;

                        var st = hasFlag ? "RETEST" : latestRes;
                        if (st is TestResultValues.Pass or TestResultValues.Na) passed++;
                        else if (st == TestResultValues.Fail) failed++;
                        else pending++;
                    }

                    if (failed > 0 || pending > 0 || passed == 0)
                    {
                        unmet.Add(new PhaseUnmetCriterionDto
                        {
                            Criterion = criterion,
                            Detail = failed > 0
                                ? $"Còn {failed} test case FAIL cần xử lý"
                                : $"Còn {pending} test case chưa chạy / cần test lại"
                        });
                    }
                    break;
                }
                case "RTM_ZERO_STALE":
                {
                    var staleIssues = await _db.IssueRequirementLinks
                        .AsNoTracking()
                        .Where(l => !l.Issue.IsDeleted && l.Issue.ProjectId == projectId && l.ChangedVersionLabel != null)
                        .Select(l => new { l.DocumentId, l.ReqUid })
                        .ToListAsync(ct);

                    var staleCases = await _db.TestCaseRequirementLinks
                        .AsNoTracking()
                        .Where(l => l.TestCase.ProjectId == projectId &&
                                    l.TestCase.Status == TestCaseStatuses.Active &&
                                    l.ChangedVersionLabel != null)
                        .Select(l => new { l.DocumentId, l.ReqUid })
                        .ToListAsync(ct);

                    var staleSet = new HashSet<(long, string)>();
                    foreach (var x in staleIssues) staleSet.Add((x.DocumentId, x.ReqUid.ToUpperInvariant()));
                    foreach (var x in staleCases) staleSet.Add((x.DocumentId, x.ReqUid.ToUpperInvariant()));

                    if (staleSet.Count > 0)
                    {
                        unmet.Add(new PhaseUnmetCriterionDto
                        {
                            Criterion = criterion,
                            Detail = $"Còn {staleSet.Count} yêu cầu thay đổi sau baseline chưa đồng bộ"
                        });
                    }
                    break;
                }
            }
        }

        return unmet;
    }

    public Task<List<WorkflowStatus>> ListStatusesByPhaseAsync(long phaseId, CancellationToken ct = default)
        => _db.WorkflowStatuses
            .Where(ws => ws.PhaseId == phaseId)
            .orderByPosition()
            .ToListAsync(ct);

    public async Task<List<long>> GetStatusIdsWithIssuesAsync(IEnumerable<long> statusIds, CancellationToken ct = default)
    {
        var ids = statusIds.Distinct().ToList();
        if (ids.Count == 0)
            return new List<long>();

        return await _db.Issues
            .AsNoTracking()
            .Where(i => !i.IsDeleted && ids.Contains(i.StatusId))
            .Select(i => i.StatusId)
            .Distinct()
            .ToListAsync(ct);
    }

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
            var toDeleteIds = toDelete.Select(s => s.Id).ToList();
            var wipToDelete = await _db.BoardWipLimits
                .Where(w => toDeleteIds.Contains(w.StatusId))
                .ToListAsync(ct);
            if (wipToDelete.Count > 0)
            {
                _db.BoardWipLimits.RemoveRange(wipToDelete);
            }

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

        var openIssueCount = await _db.Issues
            .AsNoTracking()
            .CountAsync(i => i.ProjectId == project.Id && !i.IsDeleted && i.Status.Category != StatusCategories.Done, ct);

        var doneIssueCount = await _db.Issues
            .AsNoTracking()
            .CountAsync(i => i.ProjectId == project.Id && !i.IsDeleted && i.Status.Category == StatusCategories.Done, ct);

        var documentCount = await _db.DocumentNodes
            .AsNoTracking()
            .CountAsync(n => n.ProjectId == project.Id && !n.IsDeleted && n.Kind == DocumentNodeKinds.Page, ct);

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
            OpenIssueCount = openIssueCount,
            DoneIssueCount = doneIssueCount,
            DocumentCount = documentCount,
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
            .Include(pp => pp.CompletedBy)
            .FirstAsync(pp => pp.Id == phaseId, ct);

        List<string> exitCriteria;
        try
        {
            exitCriteria = System.Text.Json.JsonSerializer.Deserialize<List<string>>(phase.ExitCriteria) ?? new List<string>();
        }
        catch
        {
            exitCriteria = new List<string>();
        }

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

        var statusIds = statuses.Select(s => s.StatusId).ToList();
        if (statusIds.Count > 0)
        {
            var counts = await _db.Issues
                .AsNoTracking()
                .Where(i => !i.IsDeleted && statusIds.Contains(i.StatusId))
                .GroupBy(i => i.StatusId)
                .Select(g => new { StatusId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StatusId, x => x.Count, ct);

            foreach (var s in statuses)
            {
                s.IssueCount = counts.GetValueOrDefault(s.StatusId, 0);
            }
        }

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
                    Scope = ar.Role.Scope,
                    Kind = ar.Role.Kind
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
            ExitCriteria = exitCriteria,
            LifecycleStatus = phase.LifecycleStatus,
            CompletedAt = phase.CompletedAt,
            CompletedBy = phase.CompletedBy != null
                ? new UserRefDto
                {
                    UserId = phase.CompletedBy.Id,
                    FullName = phase.CompletedBy.FullName,
                    Email = phase.CompletedBy.Email,
                    AvatarUrl = phase.CompletedBy.AvatarUrl
                }
                : null,
            OverrideReason = phase.OverrideReason,
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

    private static ProjectMemberDto MapMemberDto(ProjectMember pm, CompanyRole? wsRole, Department? department = null)
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
                Scope = pm.Role.Scope,
                Kind = pm.Role.Kind
            },
            WorkspaceRole = wsRole != null
                ? new RoleRefDto
                {
                    RoleId = wsRole.Id,
                    Code = wsRole.Code,
                    Name = wsRole.Name,
                    Scope = wsRole.Scope,
                    Kind = wsRole.Kind
                }
                : new RoleRefDto { RoleId = 0, Code = null, Name = "—", Scope = RoleScopes.Workspace, Kind = RoleKinds.Other },
            Department = department != null
                ? new DepartmentRefDto
                {
                    DepartmentId = department.Id,
                    Name = department.Name,
                    Code = department.Code
                }
                : null,
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
