using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class KanbanService : IKanbanService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;
    private readonly TimeProvider _clock;
    private readonly IProjectAccessGuard _accessGuard;

    public KanbanService(
        AppDbContext db,
        ICurrentUser currentUser,
        IPermissionService permissions,
        TimeProvider clock,
        IProjectAccessGuard accessGuard)
    {
        _db = db;
        _currentUser = currentUser;
        _permissions = permissions;
        _clock = clock;
        _accessGuard = accessGuard;
    }

    public async Task<ServiceResult<BoardDto>> GetBoardAsync(long projectId, CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.IssueView, ct);
        if (!auth.IsSuccess) return auth.Cast<BoardDto>();

        var statuses = await _db.WorkflowStatuses
            .AsNoTracking()
            .Include(s => s.Phase)
            .Where(s => s.ProjectId == projectId && s.IsActive && s.Phase.IsActive)
            .OrderBy(s => s.Phase.Position)
            .ThenBy(s => s.Position)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        var wipLimits = await _db.BoardWipLimits
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .ToDictionaryAsync(w => w.StatusId, w => (int?)w.WipLimit, ct);

        var columns = statuses.Select((s, idx) => new BoardColumnDto
        {
            StatusId = s.Id,
            Name = s.Name,
            Category = s.Category,
            Color = s.Color,
            PhaseId = s.PhaseId,
            PhaseName = s.Phase?.Name ?? "Quy trình",
            Position = idx,
            WipLimit = wipLimits.GetValueOrDefault(s.Id),
            IsInitial = s.IsInitial
        }).ToList();

        var activeSprintEntity = await _db.Sprints
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Status == SprintStatuses.Active, ct);

        SprintDto? activeSprintDto = null;
        if (activeSprintEntity != null)
        {
            var stats = await ComputeSprintStatsAsync(activeSprintEntity.Id, ct);
            activeSprintDto = MapSprintDto(activeSprintEntity, stats);
        }

        return ServiceResult<BoardDto>.Success(new BoardDto
        {
            Columns = columns,
            ActiveSprint = activeSprintDto
        });
    }

    public async Task<ServiceResult<BoardDto>> UpdateWipLimitsAsync(
        long projectId,
        UpdateWipLimitsRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.ProjectWorkflowManage, ct);
        if (!auth.IsSuccess) return auth.Cast<BoardDto>();

        if (request.Limits == null)
        {
            return ServiceResult<BoardDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["limits"] = new[] { "limits array is required." }
            });
        }

        var validStatusIds = await _db.WorkflowStatuses
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .Select(s => s.Id)
            .ToHashSetAsync(ct);

        foreach (var item in request.Limits)
        {
            if (!validStatusIds.Contains(item.StatusId))
                return ServiceResult<BoardDto>.NotFound("STATUS_NOT_FOUND", $"Status {item.StatusId} not found in project.");
            if (item.Limit.HasValue && item.Limit.Value < 1)
            {
                return ServiceResult<BoardDto>.ValidationFailure(new Dictionary<string, string[]>
                {
                    ["limits"] = new[] { "wipLimit must be >= 1 or null." }
                });
            }
        }

        var existingLimits = await _db.BoardWipLimits
            .Where(w => w.ProjectId == projectId)
            .ToDictionaryAsync(w => w.StatusId, ct);

        foreach (var item in request.Limits)
        {
            if (!item.Limit.HasValue)
            {
                if (existingLimits.TryGetValue(item.StatusId, out var existing))
                {
                    _db.BoardWipLimits.Remove(existing);
                }
            }
            else
            {
                if (existingLimits.TryGetValue(item.StatusId, out var existing))
                {
                    existing.WipLimit = item.Limit.Value;
                }
                else
                {
                    _db.BoardWipLimits.Add(new BoardWipLimit
                    {
                        ProjectId = projectId,
                        StatusId = item.StatusId,
                        WipLimit = item.Limit.Value
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetBoardAsync(projectId, ct);
    }

    public async Task<ServiceResult<ItemsDto<IssueSummaryDto>>> GetProjectIssuesAsync(
        long projectId,
        IssueListQuery query,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.IssueView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<IssueSummaryDto>>();

        var q = _db.Issues
            .AsNoTracking()
            .Include(i => i.Status)
            .Include(i => i.Assignee)
            .Include(i => i.Parent)
            .Include(i => i.RequirementLinks)
            .Where(i => i.ProjectId == projectId && !i.IsDeleted);

        // Filter sprintId
        if (!string.IsNullOrWhiteSpace(query.SprintId))
        {
            var sVal = query.SprintId.Trim();
            if (sVal.Equals("backlog", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(i => i.SprintId == null);
            }
            else if (sVal.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                var activeSprintId = await _db.Sprints
                    .AsNoTracking()
                    .Where(s => s.ProjectId == projectId && s.Status == SprintStatuses.Active)
                    .Select(s => (long?)s.Id)
                    .FirstOrDefaultAsync(ct);

                if (activeSprintId.HasValue)
                    q = q.Where(i => i.SprintId == activeSprintId.Value);
                else
                    return ServiceResult<ItemsDto<IssueSummaryDto>>.Success(new ItemsDto<IssueSummaryDto>());
            }
            else if (long.TryParse(sVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSprintId))
            {
                q = q.Where(i => i.SprintId == parsedSprintId);
            }
        }

        // Filter assigneeId
        if (!string.IsNullOrWhiteSpace(query.AssigneeId))
        {
            var aVal = query.AssigneeId.Trim();
            if (aVal.Equals("me", StringComparison.OrdinalIgnoreCase))
            {
                var myId = _currentUser.UserId;
                q = q.Where(i => i.AssigneeId == myId);
            }
            else if (long.TryParse(aVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAssigneeId))
            {
                q = q.Where(i => i.AssigneeId == parsedAssigneeId);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var tUpper = query.Type.Trim().ToUpperInvariant();
            q = q.Where(i => i.Type == tUpper);
        }

        if (query.StatusId.HasValue)
        {
            q = q.Where(i => i.StatusId == query.StatusId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ReqUid))
        {
            var reqUidUpper = query.ReqUid.Trim().ToUpperInvariant();
            q = q.Where(i => i.RequirementLinks.Any(rl => rl.ReqUid == reqUidUpper));
        }

        if (query.ReqChanged.HasValue)
        {
            q = q.Where(i => i.ReqChanged == query.ReqChanged.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ParentId))
        {
            var pVal = query.ParentId.Trim();
            if (pVal.Equals("root", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(i => i.ParentId == null);
            }
            else if (long.TryParse(pVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedParentId))
            {
                q = q.Where(i => i.ParentId == parsedParentId);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(i => i.Key.Contains(search) || i.Title.Contains(search) || i.Description.Contains(search));
        }

        var issues = await q.ToListAsync(ct);
        issues.Sort((a, b) =>
        {
            var cmp = string.CompareOrdinal(a.Rank, b.Rank);
            return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
        });

        var dtos = await BuildIssueSummaryDtosAsync(projectId, issues, ct);
        return ServiceResult<ItemsDto<IssueSummaryDto>>.Success(new ItemsDto<IssueSummaryDto>(dtos));
    }

    public async Task<ServiceResult<IssueDetailDto>> CreateIssueAsync(
        long projectId,
        CreateIssueRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.IssueCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<IssueDetailDto>();

        var errors = new Dictionary<string, string[]>();
        var type = request.Type?.Trim().ToUpperInvariant() ?? IssueTypes.Task;
        var title = request.Title?.Trim() ?? string.Empty;
        var priority = request.Priority?.Trim().ToUpperInvariant() ?? IssuePriorities.Medium;

        if (!IssueTypes.IsValid(type))
            errors["type"] = new[] { "Type must be TASK, BUG, STORY, or DESIGN." };
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            errors["title"] = new[] { "Title is required and must be at most 200 characters." };
        if (!IssuePriorities.IsValid(priority))
            errors["priority"] = new[] { "Priority must be HIGHEST, HIGH, MEDIUM, or LOW." };
        if (request.Points.HasValue && (request.Points.Value < 0 || request.Points.Value > 100))
            errors["points"] = new[] { "Points must be between 0 and 100." };
        if (request.Description != null && request.Description.Length > 10000)
            errors["description"] = new[] { "Description must be at most 10000 characters." };

        DateOnly? dueDate = null;
        if (!string.IsNullOrWhiteSpace(request.DueDate))
        {
            if (!DateOnly.TryParse(request.DueDate.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDue))
                errors["dueDate"] = new[] { "dueDate must be a valid YYYY-MM-DD date." };
            else
                dueDate = parsedDue;
        }

        if (errors.Count > 0)
            return ServiceResult<IssueDetailDto>.ValidationFailure(errors);

        var project = await _db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId, ct);

        long statusId;
        if (request.StatusId.HasValue)
        {
            var stExists = await _db.WorkflowStatuses
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.StatusId.Value && s.ProjectId == projectId && s.IsActive, ct);
            if (!stExists)
                return ServiceResult<IssueDetailDto>.NotFound("STATUS_NOT_FOUND", "Workflow status not found in project.");
            statusId = request.StatusId.Value;
        }
        else
        {
            var initialStatus = await _db.WorkflowStatuses
                .AsNoTracking()
                .Include(s => s.Phase)
                .Where(s => s.ProjectId == projectId && s.IsActive)
                .OrderByDescending(s => s.IsInitial)
                .ThenBy(s => s.Phase.Position)
                .ThenBy(s => s.Position)
                .FirstOrDefaultAsync(ct);

            if (initialStatus == null)
                return ServiceResult<IssueDetailDto>.NotFound("STATUS_NOT_FOUND", "Project has no active workflow statuses.");
            statusId = initialStatus.Id;
        }

        if (request.SprintId.HasValue)
        {
            var sprintExists = await _db.Sprints
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.SprintId.Value && s.ProjectId == projectId, ct);
            if (!sprintExists)
                return ServiceResult<IssueDetailDto>.NotFound("SPRINT_NOT_FOUND", "Sprint not found in project.");
        }

        if (request.ParentId.HasValue)
        {
            var parent = await _db.Issues
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.ParentId.Value && i.ProjectId == projectId && !i.IsDeleted, ct);
            if (parent == null)
                return ServiceResult<IssueDetailDto>.NotFound("ISSUE_NOT_FOUND", "Parent issue not found.");
            if (parent.ParentId.HasValue)
                return ServiceResult<IssueDetailDto>.BadRequest("SUBTASK_DEPTH_EXCEEDED", "Subtasks cannot have nested subtasks.");
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var key = await _db.AllocateIssueKeyAsync(projectId, project.Key, innerCt);
            var rank = await ComputeTailRankAsync(projectId, request.SprintId, statusId, innerCt);

            var targetStatus = await _db.WorkflowStatuses
                .AsNoTracking()
                .FirstAsync(s => s.Id == statusId, innerCt);

            var issue = new Issue
            {
                ProjectId = projectId,
                Key = key,
                Type = type,
                Title = title,
                Description = request.Description?.Trim() ?? string.Empty,
                StatusId = statusId,
                Priority = priority,
                Points = request.Points,
                AssigneeId = request.AssigneeId,
                ReporterId = userId,
                SprintId = request.SprintId,
                ParentId = request.ParentId,
                Rank = rank,
                Labels = JsonSerializer.Serialize(request.Labels ?? new List<string>()),
                DueDate = dueDate,
                ReqChanged = false,
                ResolvedAt = targetStatus.Category == "DONE" ? now : null,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Issues.Add(issue);
            await _db.SaveChangesAsync(innerCt);

            if (request.RequirementRefs != null && request.RequirementRefs.Count > 0)
            {
                await AttachRequirementRefsAsync(issue, request.RequirementRefs, userId, now, innerCt);
            }

            _db.IssueActivities.Add(new IssueActivity
            {
                IssueId = issue.Id,
                Action = IssueActivityActions.Created,
                ActorId = userId,
                Note = $"Tạo công việc {key}",
                CreatedAt = now
            });

            await _db.SaveChangesAsync(innerCt);

            var detailRes = await GetIssueDetailAsync(issue.Id, innerCt);
            return ServiceResult<IssueDetailDto>.Created(detailRes.Data!);
        }, ct);
    }

    public async Task<ServiceResult<IssueDetailDto>> GetIssueDetailAsync(long issueId, CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .AsNoTracking()
            .Include(i => i.Status)
            .Include(i => i.Assignee)
            .Include(i => i.Reporter)
            .Include(i => i.Parent)
            .Include(i => i.RequirementLinks)
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);

        if (issue == null)
            return ServiceResult<IssueDetailDto>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueView, ct);
        if (!auth.IsSuccess) return auth.Cast<IssueDetailDto>();

        var summaries = await BuildIssueSummaryDtosAsync(issue.ProjectId, new List<Issue> { issue }, ct);
        var summary = summaries[0];

        var subtasks = await _db.Issues
            .AsNoTracking()
            .Include(s => s.Status)
            .Include(s => s.Assignee)
            .Include(s => s.RequirementLinks)
            .Where(s => s.ParentId == issue.Id && !s.IsDeleted)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        var subtaskDtos = await BuildIssueSummaryDtosAsync(issue.ProjectId, subtasks, ct);

        var userId = _currentUser.UserId;
        var userPerms = await _permissions.GetProjectPermissionsAsync(userId, issue.ProjectId, ct);
        var permSet = new HashSet<string>(userPerms, StringComparer.OrdinalIgnoreCase);

        var myMember = await _db.ProjectMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(pm => pm.ProjectId == issue.ProjectId && pm.UserId == userId, ct);
        var myRoleId = myMember?.RoleId;
        var isAdmin = permSet.Contains(PermissionKeys.ProjectWorkflowManage);

        var transitions = await _db.WorkflowTransitions
            .AsNoTracking()
            .Include(t => t.ToStatus)
            .Include(t => t.AllowedRoles)
                .ThenInclude(ar => ar.Role)
            .Where(t => t.ProjectId == issue.ProjectId && t.FromStatusId == issue.StatusId)
            .ToListAsync(ct);

        var allowedTransitions = transitions
            .Where(t => isAdmin ||
                        t.AllowedRoles.Count == 0 ||
                        (myRoleId.HasValue && t.AllowedRoles.Any(ar => ar.RoleId == myRoleId.Value)))
            .Select(t => new IssueAllowedTransitionDto
            {
                TransitionId = t.Id,
                FromStatusId = t.FromStatusId,
                ToStatusId = t.ToStatusId,
                Name = t.ToStatus?.Name ?? string.Empty,
                AllowedRoles = t.AllowedRoles.Select(ar => new RoleRefDto
                {
                    RoleId = ar.Role.Id,
                    Code = ar.Role.Code,
                    Name = ar.Role.Name,
                    Scope = ar.Role.Scope,
                    Kind = ar.Role.Kind
                }).ToList(),
                RequireComment = t.RequireComment,
                CommentHint = t.CommentHint,
                Condition = t.Condition
            })
            .ToList();

        // Collect linked test cases for detail drawer (v2: via TestCases.BugIssueId)
        var linkedBugCases = await _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.Results)
            .Include(tc => tc.RetestFlags)
            .Include(tc => tc.Plan)
                .ThenInclude(p => p.Environments.Where(e => !e.IsDeleted))
            .Where(tc => tc.BugIssueId == issue.Id && tc.Status == TestCaseStatuses.Active)
            .OrderBy(tc => tc.Position)
            .ThenBy(tc => tc.Id)
            .ToListAsync(ct);

        var testCaseDtos = linkedBugCases
            .Select(tc =>
            {
                var (lastResult, envId, _) = TestStatusEvaluator.ComputeWorstTestCaseResult(tc);
                return new IssueLinkedTestCaseDto
                {
                    TestCaseId = tc.Id,
                    Key = tc.Code,
                    Title = tc.Title,
                    LastResult = lastResult,
                    PlanId = tc.PlanId,
                    EnvId = envId
                };
            })
            .ToList();

        var detail = new IssueDetailDto
        {
            IssueId = summary.IssueId,
            ProjectId = summary.ProjectId,
            Key = summary.Key,
            Type = summary.Type,
            Title = summary.Title,
            Description = issue.Description,
            StatusId = summary.StatusId,
            StatusName = summary.StatusName,
            StatusCategory = summary.StatusCategory,
            StatusColor = summary.StatusColor,
            Priority = summary.Priority,
            Points = summary.Points,
            Assignee = summary.Assignee,
            Reporter = MapUserRef(issue.Reporter),
            SprintId = summary.SprintId,
            ParentId = summary.ParentId,
            ParentKey = summary.ParentKey,
            Rank = summary.Rank,
            Labels = summary.Labels,
            DueDate = summary.DueDate,
            ReqChanged = summary.ReqChanged,
            Requirements = summary.Requirements,
            SubtaskCount = summary.SubtaskCount,
            SubtaskDone = summary.SubtaskDone,
            Subtasks = subtaskDtos,
            Tests = summary.Tests,
            TestSummary = summary.TestSummary,
            TestCases = testCaseDtos,
            AllowedTransitions = allowedTransitions,
            CreatedAt = FormatUtc(issue.CreatedAt),
            UpdatedAt = summary.UpdatedAt,
            ResolvedAt = issue.ResolvedAt.HasValue ? FormatUtc(issue.ResolvedAt.Value) : null,
            Can = new IssuePermissionsDto
            {
                Edit = permSet.Contains(PermissionKeys.IssueUpdate),
                Transition = permSet.Contains(PermissionKeys.IssueTransition),
                Assign = permSet.Contains(PermissionKeys.IssueAssign) || permSet.Contains(PermissionKeys.IssueUpdate),
                Delete = permSet.Contains(PermissionKeys.IssueDelete),
                Comment = permSet.Contains(PermissionKeys.IssueComment)
            }
        };

        return ServiceResult<IssueDetailDto>.Success(detail);
    }

    public async Task<ServiceResult<IssueDetailDto>> UpdateIssueAsync(
        long issueId,
        UpdateIssueRequest request,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .Include(i => i.Assignee)
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<IssueDetailDto>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        // Determine required permission
        bool onlyAssignee = request.AssigneeIdSpecified &&
                            request.Title == null && request.Description == null &&
                            request.Type == null && request.Priority == null &&
                            !request.PointsSpecified && !request.SprintIdSpecified &&
                            request.Labels == null && !request.DueDateSpecified;

        var requiredPerm = onlyAssignee ? PermissionKeys.IssueAssign : PermissionKeys.IssueUpdate;
        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, requiredPerm, ct);
        if (!auth.IsSuccess && onlyAssignee)
        {
            // Fallback to issue.update if user has issue.update
            auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueUpdate, ct);
        }
        if (!auth.IsSuccess) return auth.Cast<IssueDetailDto>();

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;
        var changedFields = new List<string>();

        if (request.Title != null)
        {
            var t = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(t) || t.Length > 200)
                return ServiceResult<IssueDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["title"] = new[] { "Title must be 1..200 chars." } });
            if (issue.Title != t) { issue.Title = t; changedFields.Add("tiêu đề"); }
        }

        if (request.Description != null)
        {
            if (request.Description.Length > 10000)
                return ServiceResult<IssueDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["description"] = new[] { "Description must be <= 10000 chars." } });
            if (issue.Description != request.Description) { issue.Description = request.Description; changedFields.Add("mô tả"); }
        }

        if (request.Type != null)
        {
            var t = request.Type.Trim().ToUpperInvariant();
            if (!IssueTypes.IsValid(t))
                return ServiceResult<IssueDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["type"] = new[] { "Invalid issue type." } });
            if (issue.Type != t) { issue.Type = t; changedFields.Add("loại"); }
        }

        if (request.Priority != null)
        {
            var p = request.Priority.Trim().ToUpperInvariant();
            if (!IssuePriorities.IsValid(p))
                return ServiceResult<IssueDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["priority"] = new[] { "Invalid priority." } });
            if (issue.Priority != p) { issue.Priority = p; changedFields.Add("độ ưu tiên"); }
        }

        if (request.PointsSpecified)
        {
            if (request.Points.HasValue && (request.Points.Value < 0 || request.Points.Value > 100))
                return ServiceResult<IssueDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["points"] = new[] { "Points must be 0..100." } });
            if (issue.Points != request.Points) { issue.Points = request.Points; changedFields.Add("story points"); }
        }

        if (request.Labels != null)
        {
            issue.Labels = JsonSerializer.Serialize(request.Labels);
            changedFields.Add("nhãn");
        }

        if (request.DueDateSpecified)
        {
            if (string.IsNullOrWhiteSpace(request.DueDate))
            {
                issue.DueDate = null;
            }
            else if (DateOnly.TryParse(request.DueDate.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDue))
            {
                issue.DueDate = parsedDue;
            }
            else
            {
                return ServiceResult<IssueDetailDto>.ValidationFailure(new Dictionary<string, string[]> { ["dueDate"] = new[] { "Invalid dueDate format." } });
            }
            changedFields.Add("hạn hoàn thành");
        }

        if (request.AssigneeIdSpecified && issue.AssigneeId != request.AssigneeId)
        {
            string assigneeNote;
            if (request.AssigneeId.HasValue)
            {
                var newAssignee = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.AssigneeId.Value, ct);
                if (newAssignee == null)
                    return ServiceResult<IssueDetailDto>.NotFound("USER_NOT_FOUND", "Assignee user not found.");
                assigneeNote = $"Giao cho {newAssignee.FullName}";
            }
            else
            {
                assigneeNote = "Bỏ phân công người thực hiện";
            }

            issue.AssigneeId = request.AssigneeId;
            _db.IssueActivities.Add(new IssueActivity
            {
                IssueId = issue.Id,
                Action = IssueActivityActions.Assigned,
                ActorId = userId,
                Note = assigneeNote,
                CreatedAt = now
            });
        }

        if (request.SprintIdSpecified && issue.SprintId != request.SprintId)
        {
            string sprintNote;
            if (request.SprintId.HasValue)
            {
                var sprint = await _db.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SprintId.Value && s.ProjectId == issue.ProjectId, ct);
                if (sprint == null)
                    return ServiceResult<IssueDetailDto>.NotFound("SPRINT_NOT_FOUND", "Sprint not found.");
                sprintNote = $"Chuyển vào {sprint.Name}";
            }
            else
            {
                sprintNote = "Chuyển về Backlog";
            }

            issue.SprintId = request.SprintId;
            _db.IssueActivities.Add(new IssueActivity
            {
                IssueId = issue.Id,
                Action = IssueActivityActions.SprintChanged,
                ActorId = userId,
                Note = sprintNote,
                CreatedAt = now
            });
        }

        if (changedFields.Count > 0)
        {
            _db.IssueActivities.Add(new IssueActivity
            {
                IssueId = issue.Id,
                Action = IssueActivityActions.Updated,
                ActorId = userId,
                Note = $"Cập nhật {string.Join(", ", changedFields)}",
                CreatedAt = now
            });
        }

        issue.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return await GetIssueDetailAsync(issue.Id, ct);
    }

    public async Task<ServiceResult> DeleteIssueAsync(long issueId, CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .Include(i => i.Subtasks)
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueDelete, ct);
        if (!auth.IsSuccess) return auth;

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        issue.IsDeleted = true;
        issue.UpdatedAt = now;

        foreach (var sub in issue.Subtasks.Where(s => !s.IsDeleted))
        {
            sub.IsDeleted = true;
            sub.UpdatedAt = now;
        }

        _db.IssueActivities.Add(new IssueActivity
        {
            IssueId = issue.Id,
            Action = IssueActivityActions.Deleted,
            ActorId = userId,
            Note = $"Xoá công việc {issue.Key}",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult<IssueDetailDto>> TransitionIssueAsync(
        long issueId,
        TransitionIssueRequest request,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .Include(i => i.Status)
            .Include(i => i.Subtasks)
                .ThenInclude(s => s.Status)
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<IssueDetailDto>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueTransition, ct);
        if (!auth.IsSuccess) return auth.Cast<IssueDetailDto>();

        var targetStatus = await _db.WorkflowStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ToStatusId && s.ProjectId == issue.ProjectId && s.IsActive, ct);
        if (targetStatus == null)
            return ServiceResult<IssueDetailDto>.NotFound("STATUS_NOT_FOUND", "Target status not found.");

        if (issue.StatusId == request.ToStatusId)
        {
            return await GetIssueDetailAsync(issueId, ct);
        }

        var transition = await _db.WorkflowTransitions
            .AsNoTracking()
            .Include(t => t.AllowedRoles)
            .FirstOrDefaultAsync(t => t.ProjectId == issue.ProjectId &&
                                      t.FromStatusId == issue.StatusId &&
                                      t.ToStatusId == request.ToStatusId, ct);

        if (transition == null)
        {
            return ServiceResult<IssueDetailDto>.Conflict(
                "TRANSITION_NOT_ALLOWED",
                $"Không có luồng chuyển trạng thái từ '{issue.Status.Name}' sang '{targetStatus.Name}'.");
        }

        var userId = _currentUser.UserId;
        if (transition.AllowedRoles.Count > 0)
        {
            var userPerms = await _permissions.GetProjectPermissionsAsync(userId, issue.ProjectId, ct);
            var isAdmin = userPerms.Contains(PermissionKeys.ProjectWorkflowManage, StringComparer.OrdinalIgnoreCase);
            if (!isAdmin)
            {
                var member = await _db.ProjectMembers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pm => pm.ProjectId == issue.ProjectId && pm.UserId == userId, ct);
                if (member == null || !transition.AllowedRoles.Any(ar => ar.RoleId == member.RoleId))
                {
                    return ServiceResult<IssueDetailDto>.Forbidden(
                        "TRANSITION_ROLE_FORBIDDEN",
                        "Vai trò hiện tại của bạn không được phép thực hiện bước chuyển trạng thái này.");
                }
            }
        }

        if (transition.RequireComment && string.IsNullOrWhiteSpace(request.Comment))
        {
            return ServiceResult<IssueDetailDto>.BadRequest(
                "TRANSITION_COMMENT_REQUIRED",
                transition.CommentHint ?? "Bước chuyển trạng thái này yêu cầu nhập ghi chú.");
        }

        if (!string.IsNullOrWhiteSpace(transition.Condition))
        {
            var cond = transition.Condition.Trim().ToLowerInvariant();
            if (cond == "assignee_required" && !issue.AssigneeId.HasValue)
            {
                return ServiceResult<IssueDetailDto>.Conflict(
                    "TRANSITION_CONDITION_FAILED",
                    "Cần phân công người thực hiện (assignee) trước khi chuyển trạng thái.");
            }
            if (cond == "points_required" && (!issue.Points.HasValue || issue.Points.Value <= 0))
            {
                return ServiceResult<IssueDetailDto>.Conflict(
                    "TRANSITION_CONDITION_FAILED",
                    "Cần ước lượng Story Points (> 0) trước khi chuyển trạng thái.");
            }
            if (cond == "subtasks_done" && issue.Subtasks.Any(s => !s.IsDeleted && s.Status.Category != "DONE"))
            {
                return ServiceResult<IssueDetailDto>.Conflict(
                    "TRANSITION_CONDITION_FAILED",
                    "Tất cả các công việc con (subtasks) phải hoàn thành trước khi chuyển trạng thái.");
            }
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var oldStatusName = issue.Status.Name;

        issue.StatusId = targetStatus.Id;
        issue.ResolvedAt = targetStatus.Category == "DONE" ? now : null;
        issue.UpdatedAt = now;

        if (request.Rank != null)
        {
            issue.Rank = await ComputePositionRankAsync(
                issue.ProjectId,
                issue.SprintId,
                targetStatus.Id,
                issue.Id,
                request.Rank.BeforeIssueId,
                request.Rank.AfterIssueId,
                ct);
        }
        else if (request.RankAfterIssueId.HasValue)
        {
            issue.Rank = await ComputePositionRankAsync(
                issue.ProjectId,
                issue.SprintId,
                targetStatus.Id,
                issue.Id,
                null,
                request.RankAfterIssueId.Value,
                ct);
        }
        else
        {
            issue.Rank = await ComputeTailRankAsync(issue.ProjectId, issue.SprintId, targetStatus.Id, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            _db.IssueComments.Add(new IssueComment
            {
                IssueId = issue.Id,
                Body = request.Comment.Trim(),
                AuthorId = userId,
                CreatedAt = now
            });
        }

        _db.IssueActivities.Add(new IssueActivity
        {
            IssueId = issue.Id,
            Action = IssueActivityActions.Transitioned,
            ActorId = userId,
            Note = $"{oldStatusName} → {targetStatus.Name}",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return await GetIssueDetailAsync(issue.Id, ct);
    }

    public async Task<ServiceResult<IssueSummaryDto>> RankIssueAsync(
        long issueId,
        RankIssueRequest request,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .Include(i => i.Status)
            .Include(i => i.Assignee)
            .Include(i => i.Parent)
            .Include(i => i.RequirementLinks)
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<IssueSummaryDto>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueUpdate, ct);
        if (!auth.IsSuccess)
        {
            auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueTransition, ct);
        }
        if (!auth.IsSuccess) return auth.Cast<IssueSummaryDto>();

        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;

        if (request.SprintIdSpecified && issue.SprintId != request.SprintId)
        {
            if (request.SprintId.HasValue)
            {
                var sprint = await _db.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SprintId.Value && s.ProjectId == issue.ProjectId, ct);
                if (sprint == null)
                    return ServiceResult<IssueSummaryDto>.NotFound("SPRINT_NOT_FOUND", "Sprint not found.");
                _db.IssueActivities.Add(new IssueActivity
                {
                    IssueId = issue.Id,
                    Action = IssueActivityActions.SprintChanged,
                    ActorId = userId,
                    Note = $"Chuyển vào {sprint.Name}",
                    CreatedAt = now
                });
            }
            else
            {
                _db.IssueActivities.Add(new IssueActivity
                {
                    IssueId = issue.Id,
                    Action = IssueActivityActions.SprintChanged,
                    ActorId = userId,
                    Note = "Chuyển về Backlog",
                    CreatedAt = now
                });
            }
            issue.SprintId = request.SprintId;
        }

        issue.Rank = await ComputePositionRankAsync(
            issue.ProjectId,
            issue.SprintId,
            issue.StatusId,
            issue.Id,
            request.BeforeIssueId,
            request.AfterIssueId,
            ct);

        issue.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var summaries = await BuildIssueSummaryDtosAsync(issue.ProjectId, new List<Issue> { issue }, ct);
        return ServiceResult<IssueSummaryDto>.Success(summaries[0]);
    }

    public async Task<ServiceResult<ItemsDto<IssueDetailDto>>> CreateIssuesFromRequirementsAsync(
        long projectId,
        CreateIssuesFromRequirementsRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.IssueCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<IssueDetailDto>>();

        if ((request.Items == null || request.Items.Count == 0) && request.Refs != null && request.Refs.Count > 0)
        {
            request.Items = request.Refs.Select(r => new CreateIssueFromRequirementItemDto
            {
                DocumentId = r.DocumentId,
                ReqUid = r.ReqUid,
                Type = request.Type,
                SprintId = request.SprintId
            }).ToList();
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return ServiceResult<ItemsDto<IssueDetailDto>>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["items"] = new[] { "At least 1 item is required." }
            });
        }

        var project = await _db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId, ct);
        var initialStatus = await _db.WorkflowStatuses
            .AsNoTracking()
            .Include(s => s.Phase)
            .Where(s => s.ProjectId == projectId && s.IsActive)
            .OrderByDescending(s => s.IsInitial)
            .ThenBy(s => s.Phase.Position)
            .ThenBy(s => s.Position)
            .FirstOrDefaultAsync(ct);

        if (initialStatus == null)
            return ServiceResult<ItemsDto<IssueDetailDto>>.NotFound("STATUS_NOT_FOUND", "Project has no active workflow statuses.");

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var createdIds = new List<long>();

            foreach (var item in request.Items)
            {
                var doc = await _db.DocumentNodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == item.DocumentId && d.ProjectId == projectId && !d.IsDeleted, innerCt);
                if (doc == null)
                    return ServiceResult<ItemsDto<IssueDetailDto>>.NotFound("DOCUMENT_NOT_FOUND", $"Document {item.DocumentId} not found.");

                var verId = doc.BaselineVersionId ?? doc.CurrentVersionId;
                var reqUidUpper = item.ReqUid.Trim().ToUpperInvariant();
                var req = verId.HasValue
                    ? await _db.RequirementItems.AsNoTracking().FirstOrDefaultAsync(r => r.VersionId == verId.Value && r.ReqUid == reqUidUpper, innerCt)
                    : null;

                if (req == null)
                    return ServiceResult<ItemsDto<IssueDetailDto>>.NotFound("REQUIREMENT_NOT_FOUND", $"Requirement {reqUidUpper} not found.");

                var key = await _db.AllocateIssueKeyAsync(projectId, project.Key, innerCt);
                var rank = await ComputeTailRankAsync(projectId, item.SprintId, initialStatus.Id, innerCt);
                var type = item.Type?.Trim().ToUpperInvariant() ?? IssueTypes.Story;
                if (!IssueTypes.IsValid(type)) type = IssueTypes.Story;
                var priority = item.Priority?.Trim().ToUpperInvariant() ?? IssuePriorities.Medium;
                if (!IssuePriorities.IsValid(priority)) priority = IssuePriorities.Medium;

                var issue = new Issue
                {
                    ProjectId = projectId,
                    Key = key,
                    Type = type,
                    Title = $"{req.ReqUid}: {req.Title}",
                    Description = req.BodyPreview,
                    StatusId = initialStatus.Id,
                    Priority = priority,
                    Points = item.Points,
                    AssigneeId = item.AssigneeId,
                    ReporterId = userId,
                    SprintId = item.SprintId,
                    Rank = rank,
                    Labels = "[]",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Issues.Add(issue);
                await _db.SaveChangesAsync(innerCt);

                _db.IssueRequirementLinks.Add(new IssueRequirementLink
                {
                    IssueId = issue.Id,
                    DocumentId = doc.Id,
                    ReqUid = req.ReqUid,
                    ReqHash = req.ContentHash,
                    LinkedAt = now,
                    LinkedById = userId
                });

                _db.IssueActivities.Add(new IssueActivity
                {
                    IssueId = issue.Id,
                    Action = IssueActivityActions.Created,
                    ActorId = userId,
                    Note = $"Tạo từ yêu cầu {req.ReqUid} ({doc.Code ?? doc.Title})",
                    CreatedAt = now
                });

                await _db.SaveChangesAsync(innerCt);

                if (item.CreateSubtasks == true)
                {
                    var acList = SafeDeserialize<List<string>>(req.AcceptanceCriteria) ?? new List<string>();
                    foreach (var ac in acList.Where(a => !string.IsNullOrWhiteSpace(a)))
                    {
                        var subKey = await _db.AllocateIssueKeyAsync(projectId, project.Key, innerCt);
                        var subRank = await ComputeTailRankAsync(projectId, item.SprintId, initialStatus.Id, innerCt);
                        var subTitle = ac.Trim().Length > 200 ? ac.Trim()[..200] : ac.Trim();

                        var subtask = new Issue
                        {
                            ProjectId = projectId,
                            Key = subKey,
                            Type = IssueTypes.Task,
                            Title = subTitle,
                            Description = string.Empty,
                            StatusId = initialStatus.Id,
                            Priority = priority,
                            AssigneeId = item.AssigneeId,
                            ReporterId = userId,
                            SprintId = item.SprintId,
                            ParentId = issue.Id,
                            Rank = subRank,
                            Labels = "[]",
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _db.Issues.Add(subtask);
                        await _db.SaveChangesAsync(innerCt);
                    }
                }

                createdIds.Add(issue.Id);
            }

            var detailList = new List<IssueDetailDto>();
            foreach (var id in createdIds)
            {
                var d = await GetIssueDetailAsync(id, innerCt);
                if (d.IsSuccess && d.Data != null) detailList.Add(d.Data);
            }

            return ServiceResult<ItemsDto<IssueDetailDto>>.Created(new ItemsDto<IssueDetailDto>(detailList));
        }, ct);
    }

    public async Task<ServiceResult<IssueDetailDto>> UpdateIssueRequirementsAsync(
        long issueId,
        UpdateIssueRequirementsRequest request,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .Include(i => i.RequirementLinks)
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<IssueDetailDto>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<IssueDetailDto>();

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        _db.IssueRequirementLinks.RemoveRange(issue.RequirementLinks);
        issue.RequirementLinks.Clear();

        if (request.Refs != null && request.Refs.Count > 0)
        {
            var err = await AttachRequirementRefsAsync(issue, request.Refs, userId, now, ct);
            if (!err.IsSuccess) return err.Cast<IssueDetailDto>();
        }

        issue.ReqChanged = false;
        issue.UpdatedAt = now;

        _db.IssueActivities.Add(new IssueActivity
        {
            IssueId = issue.Id,
            Action = IssueActivityActions.ReqLinked,
            ActorId = userId,
            Note = request.Refs?.Count > 0
                ? $"Liên kết {request.Refs.Count} yêu cầu ({string.Join(", ", request.Refs.Select(r => r.ReqUid.ToUpperInvariant()))})"
                : "Gỡ liên kết yêu cầu",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return await GetIssueDetailAsync(issueId, ct);
    }

    public async Task<ServiceResult<IssueDetailDto>> AcknowledgeIssueRequirementsAsync(
        long issueId,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .Include(i => i.RequirementLinks)
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<IssueDetailDto>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<IssueDetailDto>();

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var link in issue.RequirementLinks)
        {
            var doc = await _db.DocumentNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == link.DocumentId, ct);
            var verId = doc?.BaselineVersionId ?? doc?.CurrentVersionId;
            if (verId.HasValue)
            {
                var req = await _db.RequirementItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.VersionId == verId.Value && r.ReqUid == link.ReqUid, ct);
                if (req != null)
                {
                    link.ReqHash = req.ContentHash;
                }
            }
            link.ChangedVersionLabel = null;
        }

        issue.ReqChanged = false;
        issue.UpdatedAt = now;

        _db.IssueActivities.Add(new IssueActivity
        {
            IssueId = issue.Id,
            Action = IssueActivityActions.ReqAcked,
            ActorId = userId,
            Note = "Đã xác nhận cập nhật yêu cầu mới nhất",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return await GetIssueDetailAsync(issueId, ct);
    }

    public async Task<ServiceResult<ItemsDto<IssueCommentDto>>> GetIssueCommentsAsync(
        long issueId,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<ItemsDto<IssueCommentDto>>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<IssueCommentDto>>();

        var comments = await _db.IssueComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.Mentions)
                .ThenInclude(m => m.User)
            .Where(c => c.IssueId == issueId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        var repliesByParent = comments
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dtos = comments
            .Select(c => MapIssueCommentDto(
                c,
                c.ParentId == null ? (repliesByParent.GetValueOrDefault(c.Id) ?? new List<IssueComment>()) : new List<IssueComment>()))
            .ToList();
        return ServiceResult<ItemsDto<IssueCommentDto>>.Success(new ItemsDto<IssueCommentDto>(dtos));
    }

    public async Task<ServiceResult<IssueCommentDto>> CreateIssueCommentAsync(
        long issueId,
        CreateIssueCommentRequest request,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<IssueCommentDto>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueComment, ct);
        if (!auth.IsSuccess) return auth.Cast<IssueCommentDto>();

        var body = request.Body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000)
        {
            return ServiceResult<IssueCommentDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["body"] = new[] { "Comment body must be between 1 and 2000 characters." }
            });
        }

        if (request.ParentId.HasValue)
        {
            var parent = await _db.IssueComments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ParentId.Value && c.IssueId == issueId, ct);
            if (parent == null)
                return ServiceResult<IssueCommentDto>.NotFound("COMMENT_NOT_FOUND", "Parent comment not found.");
            if (parent.ParentId.HasValue)
                return ServiceResult<IssueCommentDto>.BadRequest("COMMENT_NESTING_LIMIT", "Replies can only be nested 1 level deep.");
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var comment = new IssueComment
        {
            IssueId = issueId,
            ParentId = request.ParentId,
            Body = body,
            AuthorId = userId,
            CreatedAt = now
        };
        _db.IssueComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        if (request.MentionUserIds != null && request.MentionUserIds.Count > 0)
        {
            var validUserIds = await _db.ProjectMembers
                .AsNoTracking()
                .Where(pm => pm.ProjectId == issue.ProjectId && request.MentionUserIds.Distinct().Contains(pm.UserId))
                .Select(pm => pm.UserId)
                .ToListAsync(ct);

            foreach (var uid in validUserIds)
            {
                _db.IssueCommentMentions.Add(new IssueCommentMention
                {
                    CommentId = comment.Id,
                    UserId = uid
                });
            }
        }

        _db.IssueActivities.Add(new IssueActivity
        {
            IssueId = issueId,
            Action = IssueActivityActions.Commented,
            ActorId = userId,
            Note = body.Length > 120 ? body[..120] + "…" : body,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        var loaded = await _db.IssueComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.Mentions)
                .ThenInclude(m => m.User)
            .FirstAsync(c => c.Id == comment.Id, ct);

        return ServiceResult<IssueCommentDto>.Created(MapIssueCommentDto(loaded, new List<IssueComment>()));
    }

    public async Task<ServiceResult<ItemsDto<IssueActivityDto>>> GetIssueActivitiesAsync(
        long issueId,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == issueId && !i.IsDeleted, ct);
        if (issue == null)
            return ServiceResult<ItemsDto<IssueActivityDto>>.NotFound("ISSUE_NOT_FOUND", "Issue not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(issue.ProjectId, PermissionKeys.IssueView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<IssueActivityDto>>();

        var activities = await _db.IssueActivities
            .AsNoTracking()
            .Include(a => a.Actor)
            .Where(a => a.IssueId == issueId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        var dtos = activities.Select(a => new IssueActivityDto
        {
            ActivityId = a.Id,
            IssueId = a.IssueId,
            Action = a.Action,
            Actor = a.Actor != null ? MapUserRef(a.Actor) : new UserRefDto { UserId = 0, FullName = "System" },
            Note = a.Note,
            CreatedAt = FormatUtc(a.CreatedAt)
        }).ToList();

        return ServiceResult<ItemsDto<IssueActivityDto>>.Success(new ItemsDto<IssueActivityDto>(dtos));
    }

    public async Task<ServiceResult<ItemsDto<SprintDto>>> GetProjectSprintsAsync(
        long projectId,
        string? status,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.IssueView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<SprintDto>>();

        var q = _db.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var stUpper = status.Trim().ToUpperInvariant();
            q = q.Where(s => s.Status == stUpper);
        }

        var sprints = await q
            .OrderByDescending(s => s.Status == SprintStatuses.Active)
            .ThenByDescending(s => s.Status == SprintStatuses.Planned)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        var dtos = new List<SprintDto>();
        foreach (var s in sprints)
        {
            var stats = await ComputeSprintStatsAsync(s.Id, ct);
            dtos.Add(MapSprintDto(s, stats));
        }

        return ServiceResult<ItemsDto<SprintDto>>.Success(new ItemsDto<SprintDto>(dtos));
    }

    public async Task<ServiceResult<SprintDto>> CreateSprintAsync(
        long projectId,
        CreateSprintRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.SprintManage, ct);
        if (!auth.IsSuccess) return auth.Cast<SprintDto>();

        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            errors["name"] = new[] { "Sprint name is required and must be at most 100 characters." };
        if (request.Goal != null && request.Goal.Length > 500)
            errors["goal"] = new[] { "Goal must be at most 500 characters." };
        if (request.CapacityPoints.HasValue && request.CapacityPoints.Value < 0)
            errors["capacityPoints"] = new[] { "capacityPoints must be >= 0." };

        DateOnly? startDate = null, endDate = null;
        if (!string.IsNullOrWhiteSpace(request.StartDate))
        {
            if (!DateOnly.TryParse(request.StartDate.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
                errors["startDate"] = new[] { "Invalid startDate format." };
            else startDate = sd;
        }
        if (!string.IsNullOrWhiteSpace(request.EndDate))
        {
            if (!DateOnly.TryParse(request.EndDate.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed))
                errors["endDate"] = new[] { "Invalid endDate format." };
            else endDate = ed;
        }
        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            errors["endDate"] = new[] { "endDate must be >= startDate." };

        if (errors.Count > 0)
            return ServiceResult<SprintDto>.ValidationFailure(errors);

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var sprint = new Sprint
        {
            ProjectId = projectId,
            Name = name,
            Goal = string.IsNullOrWhiteSpace(request.Goal) ? null : request.Goal.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            Status = SprintStatuses.Planned,
            CapacityPoints = request.CapacityPoints,
            CreatedById = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<SprintDto>.Created(MapSprintDto(sprint, new SprintStatsDto()));
    }

    public async Task<ServiceResult<SprintDto>> UpdateSprintAsync(
        long sprintId,
        UpdateSprintRequest request,
        CancellationToken ct = default)
    {
        var sprint = await _db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId, ct);
        if (sprint == null)
            return ServiceResult<SprintDto>.NotFound("SPRINT_NOT_FOUND", "Sprint not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(sprint.ProjectId, PermissionKeys.SprintManage, ct);
        if (!auth.IsSuccess) return auth.Cast<SprintDto>();

        if (string.Equals(request.Status?.Trim(), SprintStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            sprint.Status == SprintStatuses.Planned)
        {
            return await StartSprintAsync(sprintId, new StartSprintRequest
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Goal = request.Goal
            }, ct);
        }

        if (request.Name != null)
        {
            var n = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(n) || n.Length > 100)
                return ServiceResult<SprintDto>.ValidationFailure(new Dictionary<string, string[]> { ["name"] = new[] { "Name must be 1..100 chars." } });
            sprint.Name = n;
        }

        if (request.GoalSpecified)
        {
            if (request.Goal != null && request.Goal.Length > 500)
                return ServiceResult<SprintDto>.ValidationFailure(new Dictionary<string, string[]> { ["goal"] = new[] { "Goal must be <= 500 chars." } });
            sprint.Goal = string.IsNullOrWhiteSpace(request.Goal) ? null : request.Goal.Trim();
        }

        if (request.StartDateSpecified)
        {
            sprint.StartDate = string.IsNullOrWhiteSpace(request.StartDate)
                ? null
                : DateOnly.Parse(request.StartDate.Trim(), CultureInfo.InvariantCulture);
        }

        if (request.EndDateSpecified)
        {
            sprint.EndDate = string.IsNullOrWhiteSpace(request.EndDate)
                ? null
                : DateOnly.Parse(request.EndDate.Trim(), CultureInfo.InvariantCulture);
        }

        if (sprint.StartDate.HasValue && sprint.EndDate.HasValue && sprint.EndDate.Value < sprint.StartDate.Value)
        {
            return ServiceResult<SprintDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["endDate"] = new[] { "endDate must be >= startDate." }
            });
        }

        if (request.CapacityPointsSpecified)
        {
            if (request.CapacityPoints.HasValue && request.CapacityPoints.Value < 0)
                return ServiceResult<SprintDto>.ValidationFailure(new Dictionary<string, string[]> { ["capacityPoints"] = new[] { "capacityPoints must be >= 0." } });
            sprint.CapacityPoints = request.CapacityPoints;
        }

        sprint.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        var stats = await ComputeSprintStatsAsync(sprint.Id, ct);
        return ServiceResult<SprintDto>.Success(MapSprintDto(sprint, stats));
    }

    public async Task<ServiceResult<SprintDto>> StartSprintAsync(
        long sprintId,
        StartSprintRequest? request,
        CancellationToken ct = default)
    {
        var sprint = await _db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId, ct);
        if (sprint == null)
            return ServiceResult<SprintDto>.NotFound("SPRINT_NOT_FOUND", "Sprint not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(sprint.ProjectId, PermissionKeys.SprintManage, ct);
        if (!auth.IsSuccess) return auth.Cast<SprintDto>();

        if (sprint.Status != SprintStatuses.Planned)
        {
            return ServiceResult<SprintDto>.Conflict(
                "INVALID_SPRINT_STATE",
                "Chỉ có thể bắt đầu sprint đang ở trạng thái PLANNED.");
        }

        var hasActive = await _db.Sprints
            .AsNoTracking()
            .AnyAsync(s => s.ProjectId == sprint.ProjectId && s.Status == SprintStatuses.Active && s.Id != sprintId, ct);
        if (hasActive)
        {
            return ServiceResult<SprintDto>.Conflict(
                "ACTIVE_SPRINT_EXISTS",
                "Dự án đã có một sprint đang ACTIVE. Vui lòng hoàn thành sprint hiện tại trước khi bắt đầu sprint mới.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);

        if (request != null)
        {
            if (!string.IsNullOrWhiteSpace(request.StartDate) &&
                DateOnly.TryParse(request.StartDate.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
            {
                sprint.StartDate = sd;
            }
            if (!string.IsNullOrWhiteSpace(request.EndDate) &&
                DateOnly.TryParse(request.EndDate.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed))
            {
                sprint.EndDate = ed;
            }
            if (request.Goal != null)
            {
                sprint.Goal = string.IsNullOrWhiteSpace(request.Goal) ? null : request.Goal.Trim();
            }
        }

        sprint.StartDate ??= today;
        sprint.EndDate ??= sprint.StartDate.Value.AddDays(14);
        sprint.Status = SprintStatuses.Active;
        sprint.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        var stats = await ComputeSprintStatsAsync(sprint.Id, ct);
        return ServiceResult<SprintDto>.Success(MapSprintDto(sprint, stats));
    }

    public async Task<ServiceResult<SprintDto>> CompleteSprintAsync(
        long sprintId,
        CompleteSprintRequest? request,
        CancellationToken ct = default)
    {
        var sprint = await _db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId, ct);
        if (sprint == null)
            return ServiceResult<SprintDto>.NotFound("SPRINT_NOT_FOUND", "Sprint not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(sprint.ProjectId, PermissionKeys.SprintManage, ct);
        if (!auth.IsSuccess) return auth.Cast<SprintDto>();

        if (sprint.Status != SprintStatuses.Active)
        {
            return ServiceResult<SprintDto>.Conflict(
                "INVALID_SPRINT_STATE",
                "Chỉ có thể hoàn thành sprint đang ở trạng thái ACTIVE.");
        }

        long? targetSprintId = null;
        string targetSprintLabel = "Backlog";

        if (request?.MoveOpenIssuesTo != null)
        {
            var el = request.MoveOpenIssuesTo.Value;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var parsedId))
            {
                var targetSprint = await _db.Sprints
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == parsedId && s.ProjectId == sprint.ProjectId && s.Status == SprintStatuses.Planned, ct);
                if (targetSprint == null)
                    return ServiceResult<SprintDto>.BadRequest("INVALID_TARGET_SPRINT", "Target sprint must be a PLANNED sprint in the same project.");
                targetSprintId = targetSprint.Id;
                targetSprintLabel = targetSprint.Name;
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                var str = el.GetString()?.Trim() ?? "BACKLOG";
                if (!str.Equals("BACKLOG", StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFromStr))
                {
                    var targetSprint = await _db.Sprints
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == parsedFromStr && s.ProjectId == sprint.ProjectId && s.Status == SprintStatuses.Planned, ct);
                    if (targetSprint == null)
                        return ServiceResult<SprintDto>.BadRequest("INVALID_TARGET_SPRINT", "Target sprint must be a PLANNED sprint in the same project.");
                    targetSprintId = targetSprint.Id;
                    targetSprintLabel = targetSprint.Name;
                }
            }
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;

        var openIssues = await _db.Issues
            .Include(i => i.Status)
            .Where(i => i.SprintId == sprintId && !i.IsDeleted && i.Status.Category != "DONE")
            .ToListAsync(ct);

        foreach (var issue in openIssues)
        {
            issue.SprintId = targetSprintId;
            issue.UpdatedAt = now;
            _db.IssueActivities.Add(new IssueActivity
            {
                IssueId = issue.Id,
                Action = IssueActivityActions.SprintChanged,
                ActorId = userId,
                Note = $"Hoàn thành {sprint.Name} → chuyển sang {targetSprintLabel}",
                CreatedAt = now
            });
        }

        sprint.Status = SprintStatuses.Completed;
        sprint.CompletedAt = now;
        sprint.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        var stats = await ComputeSprintStatsAsync(sprint.Id, ct);
        return ServiceResult<SprintDto>.Success(MapSprintDto(sprint, stats));
    }

    // --- Helpers ---

    private async Task<ServiceResult> AttachRequirementRefsAsync(
        Issue issue,
        List<RequirementRefInputDto> refs,
        long userId,
        DateTime now,
        CancellationToken ct)
    {
        foreach (var r in refs.DistinctBy(x => (x.DocumentId, x.ReqUid.Trim().ToUpperInvariant())))
        {
            var doc = await _db.DocumentNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == r.DocumentId && d.ProjectId == issue.ProjectId && !d.IsDeleted, ct);
            if (doc == null)
                return ServiceResult.NotFound("DOCUMENT_NOT_FOUND", $"Document {r.DocumentId} not found.");

            var verId = doc.BaselineVersionId ?? doc.CurrentVersionId;
            var reqUidUpper = r.ReqUid.Trim().ToUpperInvariant();
            var req = verId.HasValue
                ? await _db.RequirementItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ri => ri.VersionId == verId.Value && ri.ReqUid == reqUidUpper, ct)
                : null;

            if (req == null)
                return ServiceResult.NotFound("REQUIREMENT_NOT_FOUND", $"Requirement {reqUidUpper} not found in document {r.DocumentId}.");

            _db.IssueRequirementLinks.Add(new IssueRequirementLink
            {
                IssueId = issue.Id,
                DocumentId = doc.Id,
                ReqUid = req.ReqUid,
                ReqHash = req.ContentHash,
                ChangedVersionLabel = null,
                LinkedAt = now,
                LinkedById = userId
            });
        }

        return ServiceResult.Success();
    }

    private async Task<List<IssueSummaryDto>> BuildIssueSummaryDtosAsync(
        long projectId,
        List<Issue> issues,
        CancellationToken ct)
    {
        if (issues.Count == 0) return new List<IssueSummaryDto>();

        var issueIds = issues.Select(i => i.Id).ToList();

        // Subtask counts
        var subtaskStats = await _db.Issues
            .AsNoTracking()
            .Include(s => s.Status)
            .Where(s => s.ParentId.HasValue && issueIds.Contains(s.ParentId.Value) && !s.IsDeleted)
            .GroupBy(s => s.ParentId!.Value)
            .Select(g => new
            {
                ParentId = g.Key,
                Total = g.Count(),
                Done = g.Count(s => s.Status.Category == "DONE")
            })
            .ToDictionaryAsync(x => x.ParentId, ct);

        // Requirement lookups
        var docIds = issues
            .SelectMany(i => i.RequirementLinks.Select(rl => rl.DocumentId))
            .Distinct()
            .ToList();

        var docs = docIds.Count == 0
            ? new Dictionary<long, DocumentNode>()
            : await _db.DocumentNodes
                .AsNoTracking()
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);

        var verIds = docs.Values
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var reqItems = verIds.Count == 0
            ? new List<RequirementItem>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => verIds.Contains(r.VersionId))
                .ToListAsync(ct);

        var reqLookup = reqItems.ToDictionary(r => (r.VersionId, r.ReqUid.ToUpperInvariant()));

        // Test summaries via TestCases.BugIssueId (v2)
        var bugTestCases = await _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.Results)
            .Include(tc => tc.RetestFlags)
            .Include(tc => tc.Plan)
                .ThenInclude(p => p.Environments.Where(e => !e.IsDeleted))
            .Where(tc => tc.BugIssueId.HasValue &&
                         issueIds.Contains(tc.BugIssueId.Value) &&
                         tc.Status == TestCaseStatuses.Active)
            .ToListAsync(ct);

        var bugCasesByIssue = bugTestCases
            .GroupBy(tc => tc.BugIssueId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dtos = new List<IssueSummaryDto>(issues.Count);
        foreach (var i in issues)
        {
            var reqRefs = new List<IssueRequirementRefDto>();
            foreach (var rl in i.RequirementLinks)
            {
                if (!docs.TryGetValue(rl.DocumentId, out var doc)) continue;
                var targetVerId = doc.BaselineVersionId ?? doc.CurrentVersionId;
                RequirementItem? reqItem = null;
                if (targetVerId.HasValue)
                {
                    reqLookup.TryGetValue((targetVerId.Value, rl.ReqUid.ToUpperInvariant()), out reqItem);
                }

                reqRefs.Add(new IssueRequirementRefDto
                {
                    DocumentId = rl.DocumentId,
                    DocCode = doc.Code,
                    DocTitle = doc.Title,
                    ReqUid = rl.ReqUid,
                    ReqTitle = reqItem?.Title ?? rl.ReqUid,
                    AnchorId = reqItem?.AnchorId ?? string.Empty,
                    Changed = rl.ChangedVersionLabel != null,
                    ChangedVersionLabel = rl.ChangedVersionLabel
                });
            }

            IssueTestSummaryDto? testSummary = null;
            if (bugCasesByIssue.TryGetValue(i.Id, out var linkedCases) && linkedCases.Count > 0)
            {
                int pass = 0, fail = 0, blocked = 0, notRun = 0, needsRerun = 0;
                foreach (var tc in linkedCases)
                {
                    var (lastResult, _, isRetest) = TestStatusEvaluator.ComputeWorstTestCaseResult(tc);
                    switch (lastResult)
                    {
                        case "PASS": pass++; break;
                        case "FAIL": fail++; break;
                        case "BLOCKED": blocked++; break;
                        default: notRun++; break;
                    }
                    if (isRetest)
                    {
                        needsRerun++;
                    }
                }
                testSummary = new IssueTestSummaryDto
                {
                    Total = linkedCases.Count,
                    Pass = pass,
                    Fail = fail,
                    Blocked = blocked,
                    NotRun = notRun,
                    NeedsRerun = needsRerun
                };
            }

            var stCount = subtaskStats.GetValueOrDefault(i.Id);

            dtos.Add(new IssueSummaryDto
            {
                IssueId = i.Id,
                ProjectId = i.ProjectId,
                Key = i.Key,
                Type = i.Type,
                Title = i.Title,
                StatusId = i.StatusId,
                StatusName = i.Status.Name,
                StatusCategory = i.Status.Category,
                StatusColor = i.Status.Color,
                Priority = i.Priority,
                Points = i.Points,
                Assignee = i.Assignee != null ? MapUserRef(i.Assignee) : null,
                SprintId = i.SprintId,
                ParentId = i.ParentId,
                ParentKey = i.Parent?.Key,
                Rank = i.Rank,
                Labels = SafeDeserialize<List<string>>(i.Labels) ?? new List<string>(),
                DueDate = i.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ReqChanged = i.ReqChanged,
                Requirements = reqRefs,
                SubtaskCount = stCount?.Total ?? 0,
                SubtaskDone = stCount?.Done ?? 0,
                Tests = testSummary ?? new IssueTestSummaryDto(),
                TestSummary = testSummary,
                CreatedAt = FormatUtc(i.CreatedAt),
                UpdatedAt = FormatUtc(i.UpdatedAt)
            });
        }

        return dtos;
    }

    private async Task<SprintStatsDto> ComputeSprintStatsAsync(long sprintId, CancellationToken ct)
    {
        var issues = await _db.Issues
            .AsNoTracking()
            .Include(i => i.Status)
            .Where(i => i.SprintId == sprintId && !i.IsDeleted && i.ParentId == null)
            .Select(i => new { i.Points, IsDone = i.Status.Category == "DONE" })
            .ToListAsync(ct);

        return new SprintStatsDto
        {
            TotalIssues = issues.Count,
            DoneIssues = issues.Count(i => i.IsDone),
            TotalPoints = issues.Sum(i => i.Points ?? 0),
            DonePoints = issues.Where(i => i.IsDone).Sum(i => i.Points ?? 0)
        };
    }

    private async Task<string> ComputeTailRankAsync(long projectId, long? sprintId, long statusId, CancellationToken ct)
    {
        var ranks = await _db.Issues
            .AsNoTracking()
            .Where(i => i.ProjectId == projectId && i.SprintId == sprintId && i.StatusId == statusId && !i.IsDeleted)
            .Select(i => i.Rank)
            .ToListAsync(ct);

        if (ranks.Count == 0) return FormatRank(1000);

        int maxVal = 0;
        foreach (var r in ranks)
        {
            if (int.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > maxVal)
                maxVal = v;
        }
        return FormatRank(maxVal + 1000);
    }

    private async Task<string> ComputePositionRankAsync(
        long projectId,
        long? sprintId,
        long statusId,
        long movingIssueId,
        long? beforeIssueId,
        long? afterIssueId,
        CancellationToken ct)
    {
        var siblings = await _db.Issues
            .Where(i => i.ProjectId == projectId &&
                        i.SprintId == sprintId &&
                        i.StatusId == statusId &&
                        !i.IsDeleted &&
                        i.Id != movingIssueId)
            .ToListAsync(ct);

        siblings.Sort((a, b) =>
        {
            var cmp = string.CompareOrdinal(a.Rank, b.Rank);
            return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
        });

        int insertIndex = siblings.Count;
        if (beforeIssueId.HasValue)
        {
            var idx = siblings.FindIndex(s => s.Id == beforeIssueId.Value);
            if (idx >= 0) insertIndex = idx;
        }
        else if (afterIssueId.HasValue)
        {
            var idx = siblings.FindIndex(s => s.Id == afterIssueId.Value);
            if (idx >= 0) insertIndex = idx + 1;
        }

        // Rebalance all ranks cleanly with step 1000
        string assignedRank = FormatRank((insertIndex + 1) * 1000);
        int pos = 1;
        for (int i = 0; i <= siblings.Count; i++)
        {
            if (i == insertIndex)
            {
                assignedRank = FormatRank(pos * 1000);
                pos++;
            }
            if (i < siblings.Count)
            {
                siblings[i].Rank = FormatRank(pos * 1000);
                pos++;
            }
        }

        return assignedRank;
    }

    private static string FormatRank(int n) => n.ToString("D8", CultureInfo.InvariantCulture);

    private static SprintDto MapSprintDto(Sprint s, SprintStatsDto stats) => new()
    {
        SprintId = s.Id,
        ProjectId = s.ProjectId,
        Name = s.Name,
        Goal = s.Goal,
        StartDate = s.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        EndDate = s.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Status = s.Status,
        CapacityPoints = s.CapacityPoints,
        Stats = stats,
        CreatedAt = FormatUtc(s.CreatedAt),
        CompletedAt = s.CompletedAt.HasValue ? FormatUtc(s.CompletedAt.Value) : null
    };

    private static IssueCommentDto MapIssueCommentDto(IssueComment c, List<IssueComment> replies) => new()
    {
        CommentId = c.Id,
        IssueId = c.IssueId,
        ParentId = c.ParentId,
        Body = c.Body,
        Mentions = c.Mentions.Select(m => MapUserRef(m.User)).ToList(),
        Author = MapUserRef(c.Author),
        CreatedAt = FormatUtc(c.CreatedAt),
        Replies = replies.Select(r => MapIssueCommentDto(r, new List<IssueComment>())).ToList()
    };
}
