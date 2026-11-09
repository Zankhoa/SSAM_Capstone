using System.Text.RegularExpressions;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Models;
using SSAM_Server.Repositories;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class ProjectService : IProjectService
{
    private static readonly HashSet<string> ValidPhaseCriteria = new(StringComparer.Ordinal)
    {
        "DOC_APPROVED",
        "REQ_SEGMENTED",
        "REQ_LINKED_ISSUES",
        "ISSUES_DONE",
        "TESTS_PASSED",
        "RTM_ZERO_STALE"
    };

    private readonly IProjectRepository _projectRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public ProjectService(
        IProjectRepository projectRepository,
        IWorkspaceRepository workspaceRepository,
        ICompanyRepository companyRepository,
        IPermissionService permissionService,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _projectRepository = projectRepository;
        _workspaceRepository = workspaceRepository;
        _companyRepository = companyRepository;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ServiceResult<ItemsDto<ProjectDto>>> ListWorkspaceProjectsAsync(
        long workspaceId,
        string? status,
        string? search,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<ProjectDto>>.Unauthorized("Your session is invalid or has expired.");

        var wsAuth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, null, ct);
        if (!wsAuth.IsSuccess)
            return ServiceResult<ItemsDto<ProjectDto>>.FromError(wsAuth);

        var projects = await _projectRepository.ListByWorkspaceIdsAsync(new[] { workspaceId }, ct);
        var dtos = await FilterAndMapProjectsAsync(projects, userId.Value, status, search, ct);
        return ServiceResult<ItemsDto<ProjectDto>>.Ok(new ItemsDto<ProjectDto>(dtos));
    }

    public async Task<ServiceResult<ItemsDto<ProjectDto>>> ListProjectsAsync(
        long? companyId,
        string? status,
        string? search,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<ProjectDto>>.Unauthorized("Your session is invalid or has expired.");

        if (companyId.HasValue)
        {
            var tenantAuth = await _permissionService.RequireTenantAsync(userId.Value, companyId.Value, null, ct);
            if (!tenantAuth.IsSuccess)
                return ServiceResult<ItemsDto<ProjectDto>>.FromError(tenantAuth);
        }

        var workspaces = await _workspaceRepository.ListByCompanyOrPersonalAsync(companyId, userId.Value, ct);
        var visibleWorkspaceIds = new List<long>();
        foreach (var ws in workspaces)
        {
            var access = await _permissionService.GetWorkspaceAccessAsync(userId.Value, ws.Id, ct);
            if (access != null)
                visibleWorkspaceIds.Add(ws.Id);
        }

        var projects = await _projectRepository.ListByWorkspaceIdsAsync(visibleWorkspaceIds, ct);
        var dtos = await FilterAndMapProjectsAsync(projects, userId.Value, status, search, ct);
        return ServiceResult<ItemsDto<ProjectDto>>.Ok(new ItemsDto<ProjectDto>(dtos));
    }

    public async Task<ServiceResult<ProjectKeyAvailabilityDto>> CheckKeyAvailabilityAsync(
        long workspaceId,
        string? key,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ProjectKeyAvailabilityDto>.Unauthorized("Your session is invalid or has expired.");

        var wsAuth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.ProjectCreate, ct);
        if (!wsAuth.IsSuccess)
            return ServiceResult<ProjectKeyAvailabilityDto>.FromError(wsAuth);

        var ws = wsAuth.Data!.Workspace;
        if (ws.CompanyId == null)
            return ServiceResult<ProjectKeyAvailabilityDto>.BadRequest("PERSONAL_WORKSPACE", "Workspace cá nhân không hỗ trợ tạo dự án.");

        var normalizedKey = (key ?? string.Empty).Trim().ToUpperInvariant();
        var validFormat = SlugHelper.ProjectKeyRegex().IsMatch(normalizedKey);
        var available = validFormat && !await _projectRepository.KeyExistsAsync(workspaceId, normalizedKey, ct);

        string? suggestion = null;
        if (!available)
        {
            var cleaned = Regex.Replace(normalizedKey, "[^A-Z0-9]", "");
            cleaned = Regex.Replace(cleaned, "^[0-9]+", "");
            if (cleaned.Length > 10)
                cleaned = cleaned[..10];
            var baseKey = cleaned.Length >= 2 ? cleaned : "PRJ";
            suggestion = await GenerateUniqueProjectKeyAsync(workspaceId, baseKey, ct);
        }

        return ServiceResult<ProjectKeyAvailabilityDto>.Ok(new ProjectKeyAvailabilityDto
        {
            Key = normalizedKey,
            Available = available,
            Suggestion = suggestion
        });
    }

    public async Task<ServiceResult<ProjectDto>> CreateProjectAsync(
        long workspaceId,
        CreateProjectRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ProjectDto>.Unauthorized("Your session is invalid or has expired.");

        var wsAuth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.ProjectCreate, ct);
        if (!wsAuth.IsSuccess)
            return ServiceResult<ProjectDto>.FromError(wsAuth);

        var ws = wsAuth.Data!.Workspace;
        if (!ws.IsActive)
            return ServiceResult<ProjectDto>.Conflict("WORKSPACE_ARCHIVED", "Workspace đã được lưu trữ, dữ liệu chỉ đọc.");

        if (ws.CompanyId == null)
            return ServiceResult<ProjectDto>.BadRequest("PERSONAL_WORKSPACE", "Workspace cá nhân không hỗ trợ tạo dự án.");

        var companyId = ws.CompanyId.Value;
        var name = (request.Name ?? string.Empty).Trim();
        var key = (request.Key ?? string.Empty).Trim().ToUpperInvariant();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var sdlcMethod = (request.SdlcMethod ?? string.Empty).Trim().ToUpperInvariant();
        var phases = request.Phases ?? new List<CreateProjectPhaseInputDto>();

        var errors = new Dictionary<string, string[]>();
        if (name.Length < 2 || name.Length > 100)
            errors["name"] = new[] { "Tên dự án từ 2 đến 100 ký tự" };
        if (!SlugHelper.ProjectKeyRegex().IsMatch(key))
            errors["key"] = new[] { "Key 2–10 ký tự, chữ HOA và số, bắt đầu bằng chữ" };
        if (description != null && description.Length > 500)
            errors["description"] = new[] { "Mô tả tối đa 500 ký tự" };
        if (!SdlcMethods.IsValid(sdlcMethod))
            errors["sdlcMethod"] = new[] { "Chọn phương pháp SDLC" };
        if (phases.Count == 0 || phases.Count > 10)
            errors["phases"] = new[] { "Dự án cần 1–10 phase" };
        else if (phases.Any(ph => string.IsNullOrWhiteSpace(ph.Name)))
            errors["phases"] = new[] { "Phase phải có tên" };
        else if (phases.Select(ph => ph.Name.Trim().ToLowerInvariant()).Distinct().Count() != phases.Count)
            errors["phases"] = new[] { "Tên phase bị trùng" };

        for (int i = 0; i < phases.Count; i++)
        {
            var ec = phases[i].ExitCriteria;
            if (ec != null && ec.Any(c => string.IsNullOrWhiteSpace(c) || !ValidPhaseCriteria.Contains(c.Trim())))
            {
                errors[$"phases[{i}].exitCriteria"] = new[] { "Tiêu chí hoàn tất giai đoạn không hợp lệ." };
            }
        }

        if (errors.Count > 0)
            return ServiceResult<ProjectDto>.Validation(errors);

        if (await _projectRepository.KeyExistsAsync(workspaceId, key, ct))
            return ServiceResult<ProjectDto>.Conflict("PROJECT_KEY_TAKEN", "Project Key đã được dùng trong workspace.");

        var allIssues = new List<WorkflowIssueDto>();
        for (int i = 0; i < phases.Count; i++)
        {
            var ph = phases[i];
            var roleCheck = await ValidateTransitionRolesAsync(companyId, ph.Transitions ?? new List<TransitionInputDto>(), ct);
            if (!roleCheck.IsSuccess)
                return ServiceResult<ProjectDto>.FromError(roleCheck);

            var phaseIssues = WorkflowValidator.ValidatePhaseWorkflow(
                ph.Statuses ?? new List<StatusInputDto>(),
                ph.Transitions ?? new List<TransitionInputDto>(),
                i);
            allIssues.AddRange(phaseIssues);
        }

        if (allIssues.Count > 0)
            return ServiceResult<ProjectDto>.WorkflowInvalid(allIssues);

        var members = request.Members ?? new List<ProjectMemberInputDto>();
        foreach (var m in members)
        {
            if (m.UserId == userId.Value)
                continue;

            if (!await _workspaceRepository.IsMemberAsync(workspaceId, m.UserId, ct))
                return ServiceResult<ProjectDto>.BadRequest("NOT_WORKSPACE_MEMBER", "Có người chưa là thành viên workspace.");

            var roleCheck = await RequireAssignableProjectRoleAsync(companyId, m.RoleId, ct);
            if (!roleCheck.IsSuccess)
                return ServiceResult<ProjectDto>.FromError(roleCheck);
        }

        return await _projectRepository.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            var project = new Project
            {
                WorkspaceId = workspaceId,
                Key = key,
                Name = name,
                Description = description,
                SdlcMethod = sdlcMethod,
                AiEnabled = request.AiEnabled != false,
                CreatedBy = userId.Value,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _projectRepository.AddProjectAsync(project, innerCt);
            await _projectRepository.SaveChangesAsync(innerCt);

            for (int i = 0; i < phases.Count; i++)
            {
                var phInput = phases[i];
                var normalizedCriteria = (phInput.ExitCriteria ?? new List<string>())
                    .Select(c => c.Trim())
                    .Where(ValidPhaseCriteria.Contains)
                    .Distinct()
                    .ToList();

                var phase = new ProjectPhase
                {
                    ProjectId = project.Id,
                    Name = phInput.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(phInput.Description) ? null : phInput.Description.Trim(),
                    Position = i + 1,
                    IsActive = true,
                    ExitCriteria = System.Text.Json.JsonSerializer.Serialize(normalizedCriteria),
                    LifecycleStatus = i == 0 ? "ACTIVE" : "UPCOMING",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _projectRepository.AddPhaseAsync(phase, innerCt);
                await _projectRepository.SaveChangesAsync(innerCt);

                await _projectRepository.WritePhaseWorkflowAsync(
                    project.Id,
                    phase.Id,
                    phInput.Statuses ?? new List<StatusInputDto>(),
                    phInput.Transitions ?? new List<TransitionInputDto>(),
                    innerCt);
            }

            var pmRole = await _companyRepository.GetRoleByCodeAsync(companyId, CompanyRoleCodes.ProjectManager, innerCt);
            await _projectRepository.AddProjectMemberAsync(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = userId.Value,
                RoleId = pmRole!.Id,
                IsActive = true,
                JoinedAt = now
            }, innerCt);

            var addedUserIds = new HashSet<long> { userId.Value };
            foreach (var m in members)
            {
                if (!addedUserIds.Add(m.UserId))
                    continue;

                await _projectRepository.AddProjectMemberAsync(new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = m.UserId,
                    RoleId = m.RoleId,
                    IsActive = true,
                    JoinedAt = now
                }, innerCt);
            }

            var trackedWs = await _workspaceRepository.GetByIdAsync(workspaceId, innerCt);
            if (trackedWs != null)
                trackedWs.UpdatedAt = now;

            await _projectRepository.SaveChangesAsync(innerCt);

            var access = await _permissionService.GetProjectAccessAsync(userId.Value, project.Id, innerCt);
            var dto = await _projectRepository.BuildProjectDtoAsync(
                project,
                ws,
                userId.Value,
                access?.MyRole,
                access?.Permissions ?? PermissionKeys.AllProjectKeys.ToHashSet(),
                innerCt);

            return ServiceResult<ProjectDto>.Created(dto);
        }, ct);
    }

    public async Task<ServiceResult<ProjectDto>> GetProjectAsync(long projectId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ProjectDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, null, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ProjectDto>.FromError(auth);

        var access = auth.Data!;
        var dto = await _projectRepository.BuildProjectDtoAsync(access.Project, access.Workspace, userId.Value, access.MyRole, access.Permissions, ct);
        return ServiceResult<ProjectDto>.Ok(dto);
    }

    public async Task<ServiceResult<ProjectDto>> UpdateProjectAsync(
        long projectId,
        UpdateProjectRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ProjectDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, null, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ProjectDto>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<ProjectDto>.FromError(writable);

        if ((request.Name != null || request.DescriptionSpecified) && !access.Permissions.Contains(PermissionKeys.ProjectUpdate))
            return ServiceResult<ProjectDto>.ForbiddenPermission(PermissionKeys.ProjectUpdate);

        // Bật/tắt tiện ích AI đã được gộp vào project.update ("Chỉnh sửa cấu hình Project & Bật/tắt tiện ích AI").
        if (request.AiEnabled.HasValue && !access.Permissions.Contains(PermissionKeys.ProjectUpdate))
            return ServiceResult<ProjectDto>.ForbiddenPermission(PermissionKeys.ProjectUpdate);

        var tracked = await _projectRepository.GetByIdAsync(projectId, ct);
        if (tracked == null)
            return ServiceResult<ProjectDto>.NotFound("Không tìm thấy dữ liệu.");

        if (request.Name != null)
        {
            var trimmedName = request.Name.Trim();
            if (trimmedName.Length < 2 || trimmedName.Length > 100)
            {
                return ServiceResult<ProjectDto>.Validation(new Dictionary<string, string[]>
                {
                    ["name"] = new[] { "Tên dự án từ 2 đến 100 ký tự" }
                });
            }
            tracked.Name = trimmedName;
        }

        if (request.DescriptionSpecified)
        {
            var desc = (request.Description ?? string.Empty).Trim();
            if (desc.Length > 500)
            {
                return ServiceResult<ProjectDto>.Validation(new Dictionary<string, string[]>
                {
                    ["description"] = new[] { "Mô tả tối đa 500 ký tự" }
                });
            }
            tracked.Description = string.IsNullOrEmpty(desc) ? null : desc;
        }

        if (request.AiEnabled.HasValue)
            tracked.AiEnabled = request.AiEnabled.Value;

        tracked.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _projectRepository.SaveChangesAsync(ct);

        var dto = await _projectRepository.BuildProjectDtoAsync(tracked, access.Workspace, userId.Value, access.MyRole, access.Permissions, ct);
        return ServiceResult<ProjectDto>.Ok(dto);
    }

    public async Task<ServiceResult<ProjectDto>> ArchiveProjectAsync(long projectId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ProjectDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectArchive, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ProjectDto>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<ProjectDto>.FromError(writable);

        var tracked = await _projectRepository.GetByIdAsync(projectId, ct);
        if (tracked == null)
            return ServiceResult<ProjectDto>.NotFound("Không tìm thấy dữ liệu.");

        var now = _clock.GetUtcNow().UtcDateTime;
        tracked.IsActive = false;
        tracked.ArchivedAt = now;
        tracked.UpdatedAt = now;
        await _projectRepository.SaveChangesAsync(ct);

        var dto = await _projectRepository.BuildProjectDtoAsync(tracked, access.Workspace, userId.Value, access.MyRole, access.Permissions, ct);
        return ServiceResult<ProjectDto>.Ok(dto);
    }

    public async Task<ServiceResult<ProjectDto>> RestoreProjectAsync(long projectId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ProjectDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectArchive, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ProjectDto>.FromError(auth);

        var access = auth.Data!;
        if (!access.Workspace.IsActive)
            return ServiceResult<ProjectDto>.Conflict("WORKSPACE_ARCHIVED", "Workspace đã được lưu trữ, dữ liệu chỉ đọc.");

        var tracked = await _projectRepository.GetByIdAsync(projectId, ct);
        if (tracked == null)
            return ServiceResult<ProjectDto>.NotFound("Không tìm thấy dữ liệu.");

        tracked.IsActive = true;
        tracked.ArchivedAt = null;
        tracked.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _projectRepository.SaveChangesAsync(ct);

        var dto = await _projectRepository.BuildProjectDtoAsync(tracked, access.Workspace, userId.Value, access.MyRole, access.Permissions, ct);
        return ServiceResult<ProjectDto>.Ok(dto);
    }

    // --- §11 Project Members ---

    public async Task<ServiceResult<ItemsDto<ProjectMemberDto>>> ListMembersAsync(long projectId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<ProjectMemberDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectView, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ItemsDto<ProjectMemberDto>>.FromError(auth);

        var access = auth.Data!;
        var items = await _projectRepository.ListMembersAsync(projectId, access.Project.WorkspaceId, ct);
        return ServiceResult<ItemsDto<ProjectMemberDto>>.Ok(new ItemsDto<ProjectMemberDto>(items));
    }

    public async Task<ServiceResult<ItemsDto<ProjectMemberCandidateDto>>> ListMemberCandidatesAsync(
        long projectId,
        string? search,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<ProjectMemberCandidateDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectMemberManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ItemsDto<ProjectMemberCandidateDto>>.FromError(auth);

        var access = auth.Data!;
        var items = await _projectRepository.ListMemberCandidatesAsync(projectId, access.Project.WorkspaceId, search, ct);
        return ServiceResult<ItemsDto<ProjectMemberCandidateDto>>.Ok(new ItemsDto<ProjectMemberCandidateDto>(items));
    }

    public async Task<ServiceResult<ItemsDto<ProjectMemberDto>>> AddMembersAsync(
        long projectId,
        AddProjectMembersRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<ProjectMemberDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectMemberManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ItemsDto<ProjectMemberDto>>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<ItemsDto<ProjectMemberDto>>.FromError(writable);

        if (request.Members == null || request.Members.Count == 0)
        {
            return ServiceResult<ItemsDto<ProjectMemberDto>>.Validation(new Dictionary<string, string[]>
            {
                ["members"] = new[] { "Chọn ít nhất 1 thành viên" }
            });
        }

        var companyId = access.Workspace.CompanyId!.Value;
        var workspaceId = access.Project.WorkspaceId;

        foreach (var m in request.Members)
        {
            if (!await _workspaceRepository.IsMemberAsync(workspaceId, m.UserId, ct))
                return ServiceResult<ItemsDto<ProjectMemberDto>>.BadRequest("NOT_WORKSPACE_MEMBER", "Người dùng chưa là thành viên workspace.");

            if (await _projectRepository.IsActiveMemberAsync(projectId, m.UserId, ct))
                return ServiceResult<ItemsDto<ProjectMemberDto>>.Conflict("ALREADY_MEMBER", "Người dùng đã là thành viên dự án.");

            var roleCheck = await RequireAssignableProjectRoleAsync(companyId, m.RoleId, ct);
            if (!roleCheck.IsSuccess)
                return ServiceResult<ItemsDto<ProjectMemberDto>>.FromError(roleCheck);
        }

        return await _projectRepository.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            foreach (var m in request.Members)
            {
                var previous = await _projectRepository.GetMemberRecordAsync(projectId, m.UserId, innerCt);
                if (previous != null)
                {
                    previous.IsActive = true;
                    previous.RoleId = m.RoleId;
                    previous.JoinedAt = now;
                }
                else
                {
                    await _projectRepository.AddProjectMemberAsync(new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = m.UserId,
                        RoleId = m.RoleId,
                        IsActive = true,
                        JoinedAt = now
                    }, innerCt);
                }
            }

            var trackedProject = await _projectRepository.GetByIdAsync(projectId, innerCt);
            if (trackedProject != null)
                trackedProject.UpdatedAt = now;

            await _projectRepository.SaveChangesAsync(innerCt);

            var addedDtos = new List<ProjectMemberDto>();
            foreach (var m in request.Members)
            {
                var dto = await _projectRepository.GetMemberDtoAsync(projectId, workspaceId, m.UserId, innerCt);
                if (dto != null)
                    addedDtos.Add(dto);
            }

            return ServiceResult<ItemsDto<ProjectMemberDto>>.Created(new ItemsDto<ProjectMemberDto>(addedDtos));
        }, ct);
    }

    public async Task<ServiceResult<ProjectMemberDto>> UpdateMemberRoleAsync(
        long projectId,
        long targetUserId,
        UpdateProjectMemberRoleRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ProjectMemberDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectMemberManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ProjectMemberDto>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<ProjectMemberDto>.FromError(writable);

        var member = await _projectRepository.GetActiveMemberAsync(projectId, targetUserId, ct);
        if (member == null)
            return ServiceResult<ProjectMemberDto>.NotFound("Không tìm thấy dữ liệu.");

        var companyId = access.Workspace.CompanyId!.Value;
        var roleCheck = await RequireAssignableProjectRoleAsync(companyId, request.RoleId, ct);
        if (!roleCheck.IsSuccess)
            return ServiceResult<ProjectMemberDto>.FromError(roleCheck);

        var newRole = roleCheck.Data!;
        if (member.Role.Code == CompanyRoleCodes.ProjectManager && newRole.Code != CompanyRoleCodes.ProjectManager)
        {
            var pmCount = await _projectRepository.CountActiveProjectManagersAsync(projectId, ct);
            if (pmCount <= 1)
                return ServiceResult<ProjectMemberDto>.Conflict("LAST_PROJECT_MANAGER", "Dự án phải còn ít nhất 1 Project Manager.");
        }

        member.RoleId = newRole.Id;
        await _projectRepository.SaveChangesAsync(ct);

        var dto = await _projectRepository.GetMemberDtoAsync(projectId, access.Project.WorkspaceId, targetUserId, ct);
        return ServiceResult<ProjectMemberDto>.Ok(dto!);
    }

    public async Task<ServiceResult> RemoveMemberAsync(long projectId, long targetUserId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult.Unauthorized("Your session is invalid or has expired.");

        var requiredPerm = targetUserId == userId.Value ? null : PermissionKeys.ProjectMemberManage;
        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, requiredPerm, ct);
        if (!auth.IsSuccess)
            return auth;

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return writable;

        var member = await _projectRepository.GetActiveMemberAsync(projectId, targetUserId, ct);
        if (member == null)
            return ServiceResult.NotFound("Không tìm thấy dữ liệu.");

        if (member.Role.Code == CompanyRoleCodes.ProjectManager)
        {
            var pmCount = await _projectRepository.CountActiveProjectManagersAsync(projectId, ct);
            if (pmCount <= 1)
                return ServiceResult.Conflict("LAST_PROJECT_MANAGER", "Dự án phải còn ít nhất 1 Project Manager.");
        }

        member.IsActive = false;
        var trackedProject = await _projectRepository.GetByIdAsync(projectId, ct);
        if (trackedProject != null)
            trackedProject.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

        await _projectRepository.SaveChangesAsync(ct);
        return ServiceResult.NoContent();
    }

    // --- §12 Workflow ---

    public async Task<ServiceResult<WorkflowDto>> GetWorkflowAsync(long projectId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkflowDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, null, ct);
        if (!auth.IsSuccess)
            return ServiceResult<WorkflowDto>.FromError(auth);

        var dto = await _projectRepository.BuildWorkflowDtoAsync(auth.Data!.Project, ct);
        return ServiceResult<WorkflowDto>.Ok(dto);
    }

    public async Task<ServiceResult<PhaseDto>> CreatePhaseAsync(
        long projectId,
        CreatePhaseRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<PhaseDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectWorkflowManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<PhaseDto>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<PhaseDto>.FromError(writable);

        var name = (request.Name ?? string.Empty).Trim();
        var existingPhases = await _projectRepository.ListPhasesByProjectAsync(projectId, ct);

        if (name.Length < 1 || name.Length > 100)
        {
            return ServiceResult<PhaseDto>.Validation(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Tên phase từ 1 đến 100 ký tự" }
            });
        }

        if (existingPhases.Any(ph => ph.IsActive && string.Equals(ph.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceResult<PhaseDto>.Validation(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Tên phase đã tồn tại" }
            });
        }

        if (existingPhases.Count >= 10)
        {
            return ServiceResult<PhaseDto>.Validation(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Dự án tối đa 10 phase" }
            });
        }

        var companyId = access.Workspace.CompanyId!.Value;
        var projectRoleIds = await _projectRepository.GetActiveProjectRoleIdsAsync(companyId, ct);

        return await _projectRepository.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var hasActiveLifecycle = existingPhases.Any(ph => ph.IsActive && ph.LifecycleStatus == "ACTIVE");
            var phase = new ProjectPhase
            {
                ProjectId = projectId,
                Name = name,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Position = existingPhases.Count + 1,
                IsActive = true,
                ExitCriteria = "[]",
                LifecycleStatus = hasActiveLifecycle ? "UPCOMING" : "ACTIVE",
                CreatedAt = now,
                UpdatedAt = now
            };
            await _projectRepository.AddPhaseAsync(phase, innerCt);
            await _projectRepository.SaveChangesAsync(innerCt);

            var defaultStatuses = new List<StatusInputDto>
            {
                new() { Key = "todo", Name = "To Do", Category = StatusCategories.Todo, Color = "#78716c", Description = null, IsInitial = true },
                new() { Key = "doing", Name = "In Progress", Category = StatusCategories.InProgress, Color = "#2563eb", Description = null, IsInitial = false },
                new() { Key = "done", Name = "Done", Category = StatusCategories.Done, Color = "#079455", Description = null, IsInitial = false }
            };

            var defaultTransitions = new List<TransitionInputDto>
            {
                new()
                {
                    From = System.Text.Json.JsonSerializer.SerializeToElement("todo"),
                    To = System.Text.Json.JsonSerializer.SerializeToElement("doing"),
                    AllowedRoleIds = projectRoleIds,
                    RequireComment = false
                },
                new()
                {
                    From = System.Text.Json.JsonSerializer.SerializeToElement("doing"),
                    To = System.Text.Json.JsonSerializer.SerializeToElement("done"),
                    AllowedRoleIds = projectRoleIds,
                    RequireComment = false
                }
            };

            await _projectRepository.WritePhaseWorkflowAsync(projectId, phase.Id, defaultStatuses, defaultTransitions, innerCt);

            var trackedProject = await _projectRepository.GetByIdAsync(projectId, innerCt);
            if (trackedProject != null)
                trackedProject.UpdatedAt = now;

            await _projectRepository.SaveChangesAsync(innerCt);

            var dto = await _projectRepository.BuildPhaseDtoAsync(phase.Id, innerCt);
            return ServiceResult<PhaseDto>.Created(dto);
        }, ct);
    }

    public async Task<ServiceResult<WorkflowDto>> UpdatePhaseAsync(
        long projectId,
        long phaseId,
        UpdatePhaseRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkflowDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectWorkflowManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<WorkflowDto>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<WorkflowDto>.FromError(writable);

        var siblings = await _projectRepository.ListPhasesByProjectAsync(projectId, ct);
        var phase = siblings.FirstOrDefault(ph => ph.Id == phaseId);
        if (phase == null)
            return ServiceResult<WorkflowDto>.NotFound("Không tìm thấy dữ liệu.");

        var editable = true;
        if (!editable && (request.Name != null || request.Position.HasValue || request.IsActive.HasValue))
            return ServiceResult<WorkflowDto>.Forbidden("FORBIDDEN", "Bạn không có quyền thực hiện thao tác này. (phase cố định theo mẫu SDLC)");

        if (request.Name != null)
        {
            var trimmedName = request.Name.Trim();
            if (trimmedName.Length < 1 || trimmedName.Length > 100)
            {
                return ServiceResult<WorkflowDto>.Validation(new Dictionary<string, string[]>
                {
                    ["name"] = new[] { "Tên phase từ 1 đến 100 ký tự" }
                });
            }

            if (siblings.Any(ph => ph.Id != phase.Id && ph.IsActive && string.Equals(ph.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                return ServiceResult<WorkflowDto>.Validation(new Dictionary<string, string[]>
                {
                    ["name"] = new[] { "Tên phase đã tồn tại" }
                });
            }

            phase.Name = trimmedName;
        }

        if (request.DescriptionSpecified)
            phase.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.ExitCriteria != null)
        {
            if (request.ExitCriteria.Any(c => string.IsNullOrWhiteSpace(c) || !ValidPhaseCriteria.Contains(c.Trim())))
            {
                return ServiceResult<WorkflowDto>.Validation(new Dictionary<string, string[]>
                {
                    ["exitCriteria"] = new[] { "Tiêu chí hoàn tất giai đoạn không hợp lệ." }
                });
            }

            var normalizedCriteria = request.ExitCriteria
                .Select(c => c.Trim())
                .Where(ValidPhaseCriteria.Contains)
                .Distinct()
                .ToList();
            phase.ExitCriteria = System.Text.Json.JsonSerializer.Serialize(normalizedCriteria);
        }

        if (request.IsActive.HasValue)
        {
            if (!request.IsActive.Value && siblings.Count(ph => ph.IsActive && ph.Id != phase.Id) == 0)
                return ServiceResult<WorkflowDto>.Conflict("PHASE_REQUIRED", "Dự án phải còn ít nhất 1 phase đang hoạt động.");

            phase.IsActive = request.IsActive.Value;
        }

        if (request.Position.HasValue)
        {
            var target = Math.Clamp(request.Position.Value, 1, siblings.Count);
            var ordered = siblings.Where(ph => ph.Id != phase.Id).OrderBy(ph => ph.Position).ToList();
            ordered.Insert(target - 1, phase);
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].Position = i + 1;
            }
        }

        // Ensure at least one active phase is ACTIVE if any UPCOMING active phase exists
        if (!siblings.Any(ph => ph.IsActive && ph.LifecycleStatus == "ACTIVE"))
        {
            var firstUpcoming = siblings
                .Where(ph => ph.IsActive && ph.LifecycleStatus == "UPCOMING")
                .OrderBy(ph => ph.Position)
                .FirstOrDefault();
            if (firstUpcoming != null)
            {
                firstUpcoming.LifecycleStatus = "ACTIVE";
            }
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        phase.UpdatedAt = now;

        var trackedProject = await _projectRepository.GetByIdAsync(projectId, ct);
        if (trackedProject != null)
            trackedProject.UpdatedAt = now;

        await _projectRepository.SaveChangesAsync(ct);

        var dto = await _projectRepository.BuildWorkflowDtoAsync(access.Project, ct);
        return ServiceResult<WorkflowDto>.Ok(dto);
    }

    public async Task<ServiceResult<PhaseDto>> UpdatePhaseWorkflowAsync(
        long projectId,
        long phaseId,
        UpdatePhaseWorkflowRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<PhaseDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectWorkflowManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<PhaseDto>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<PhaseDto>.FromError(writable);

        var phase = await _projectRepository.GetPhaseByIdAsync(projectId, phaseId, ct);
        if (phase == null)
            return ServiceResult<PhaseDto>.NotFound("Không tìm thấy dữ liệu.");

        var statuses = request.Statuses ?? new List<StatusInputDto>();
        var transitions = request.Transitions ?? new List<TransitionInputDto>();

        var existingStatuses = await _projectRepository.ListStatusesByPhaseAsync(phaseId, ct);
        var existingStatusIds = existingStatuses.Select(s => s.Id).ToHashSet();

        foreach (var s in statuses)
        {
            if (s.StatusId.HasValue && !existingStatusIds.Contains(s.StatusId.Value))
                return ServiceResult<PhaseDto>.NotFound("Không tìm thấy dữ liệu.");
        }

        var keepStatusIds = statuses
            .Where(s => s.StatusId.HasValue)
            .Select(s => s.StatusId!.Value)
            .ToHashSet();
        var removedStatusIds = existingStatuses
            .Where(s => !keepStatusIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToList();

        if (removedStatusIds.Count > 0)
        {
            var inUseIds = await _projectRepository.GetStatusIdsWithIssuesAsync(removedStatusIds, ct);
            if (inUseIds.Count > 0)
            {
                return ServiceResult<PhaseDto>.Conflict(
                    "STATUS_IN_USE",
                    "Không thể xoá trạng thái đang có công việc.",
                    new Dictionary<string, string[]>
                    {
                        ["statuses"] = inUseIds.Select(id => id.ToString()).ToArray()
                    });
            }
        }

        var companyId = access.Workspace.CompanyId!.Value;
        var roleCheck = await ValidateTransitionRolesAsync(companyId, transitions, ct);
        if (!roleCheck.IsSuccess)
            return ServiceResult<PhaseDto>.FromError(roleCheck);

        var issues = WorkflowValidator.ValidatePhaseWorkflow(statuses, transitions, 0);
        if (issues.Count > 0)
            return ServiceResult<PhaseDto>.WorkflowInvalid(issues);

        return await _projectRepository.ExecuteInTransactionAsync(async innerCt =>
        {
            await _projectRepository.WritePhaseWorkflowAsync(projectId, phaseId, statuses, transitions, innerCt);

            var now = _clock.GetUtcNow().UtcDateTime;
            var trackedProject = await _projectRepository.GetByIdAsync(projectId, innerCt);
            if (trackedProject != null)
                trackedProject.UpdatedAt = now;

            await _projectRepository.SaveChangesAsync(innerCt);

            var dto = await _projectRepository.BuildPhaseDtoAsync(phaseId, innerCt);
            return ServiceResult<PhaseDto>.Ok(dto);
        }, ct);
    }

    public async Task<ServiceResult<WorkflowDto>> CompletePhaseAsync(
        long projectId,
        long phaseId,
        CompletePhaseRequestDto? request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkflowDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectWorkflowManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<WorkflowDto>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<WorkflowDto>.FromError(writable);

        var siblings = await _projectRepository.ListPhasesByProjectAsync(projectId, ct);
        var phase = siblings.FirstOrDefault(ph => ph.Id == phaseId && ph.IsActive);
        if (phase == null)
            return ServiceResult<WorkflowDto>.NotFound("Không tìm thấy dữ liệu.");

        if (phase.LifecycleStatus == "COMPLETED")
            return ServiceResult<WorkflowDto>.Conflict("PHASE_ALREADY_COMPLETED", "Giai đoạn này đã hoàn tất.");

        List<string> exitCriteria;
        try
        {
            exitCriteria = System.Text.Json.JsonSerializer.Deserialize<List<string>>(phase.ExitCriteria) ?? new List<string>();
        }
        catch
        {
            exitCriteria = new List<string>();
        }

        var unmet = await _projectRepository.EvaluatePhaseGatesAsync(projectId, phaseId, exitCriteria, ct);
        var overrideReason = request?.OverrideReason?.Trim();

        if (unmet.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(overrideReason))
            {
                return new ServiceResult<WorkflowDto>
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status409Conflict,
                    ErrorCode = "PHASE_GATE_UNMET",
                    ErrorMessage = "Giai đoạn chưa đạt đủ tiêu chí hoàn tất. Vui lòng hoàn thành các điều kiện hoặc nhập lý do bỏ qua.",
                    UnmetCriteria = unmet
                };
            }

            if (overrideReason.Length > 500)
            {
                return ServiceResult<WorkflowDto>.Validation(new Dictionary<string, string[]>
                {
                    ["overrideReason"] = new[] { "Lý do bỏ qua tối đa 500 ký tự." }
                });
            }
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        phase.LifecycleStatus = "COMPLETED";
        phase.CompletedAt = now;
        phase.CompletedById = userId.Value;
        phase.OverrideReason = unmet.Count > 0 ? overrideReason : null;
        phase.UpdatedAt = now;

        if (!siblings.Any(ph => ph.IsActive && ph.Id != phase.Id && ph.LifecycleStatus == "ACTIVE"))
        {
            var nextPhase = siblings
                .Where(ph => ph.IsActive && ph.Id != phase.Id && ph.Position > phase.Position && ph.LifecycleStatus == "UPCOMING")
                .OrderBy(ph => ph.Position)
                .FirstOrDefault()
                ?? siblings
                    .Where(ph => ph.IsActive && ph.Id != phase.Id && ph.LifecycleStatus == "UPCOMING")
                    .OrderBy(ph => ph.Position)
                    .FirstOrDefault();

            if (nextPhase != null)
            {
                nextPhase.LifecycleStatus = "ACTIVE";
                nextPhase.UpdatedAt = now;
            }
        }

        await _projectRepository.AddPhaseLogAsync(new ProjectPhaseLog
        {
            ProjectId = projectId,
            PhaseId = phaseId,
            Action = unmet.Count > 0 ? "OVERRIDE_COMPLETE" : "COMPLETE",
            ActorId = userId.Value,
            At = now,
            UnmetCriteria = unmet.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(unmet) : null,
            Reason = unmet.Count > 0 ? overrideReason : null
        }, ct);

        var trackedProject = await _projectRepository.GetByIdAsync(projectId, ct);
        if (trackedProject != null)
            trackedProject.UpdatedAt = now;

        await _projectRepository.SaveChangesAsync(ct);

        var dto = await _projectRepository.BuildWorkflowDtoAsync(access.Project, ct);
        return ServiceResult<WorkflowDto>.Ok(dto);
    }

    public async Task<ServiceResult<WorkflowDto>> ReopenPhaseAsync(
        long projectId,
        long phaseId,
        ReopenPhaseRequestDto? request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkflowDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireProjectAsync(userId.Value, projectId, PermissionKeys.ProjectWorkflowManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<WorkflowDto>.FromError(auth);

        var access = auth.Data!;
        var writable = EnsureProjectWritable(access.Workspace, access.Project);
        if (!writable.IsSuccess)
            return ServiceResult<WorkflowDto>.FromError(writable);

        var siblings = await _projectRepository.ListPhasesByProjectAsync(projectId, ct);
        var phase = siblings.FirstOrDefault(ph => ph.Id == phaseId && ph.IsActive);
        if (phase == null)
            return ServiceResult<WorkflowDto>.NotFound("Không tìm thấy dữ liệu.");

        if (phase.LifecycleStatus != "COMPLETED")
            return ServiceResult<WorkflowDto>.Conflict("PHASE_NOT_COMPLETED", "Chỉ có thể mở lại giai đoạn đã hoàn tất.");

        var reason = request?.Reason?.Trim();
        if (reason != null && reason.Length > 500)
        {
            return ServiceResult<WorkflowDto>.Validation(new Dictionary<string, string[]>
            {
                ["reason"] = new[] { "Lý do mở lại tối đa 500 ký tự." }
            });
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var other in siblings.Where(ph => ph.IsActive && ph.Id != phase.Id && ph.LifecycleStatus == "ACTIVE"))
        {
            other.LifecycleStatus = "UPCOMING";
            other.UpdatedAt = now;
        }

        phase.LifecycleStatus = "ACTIVE";
        phase.CompletedAt = null;
        phase.CompletedById = null;
        phase.OverrideReason = null;
        phase.UpdatedAt = now;

        await _projectRepository.AddPhaseLogAsync(new ProjectPhaseLog
        {
            ProjectId = projectId,
            PhaseId = phaseId,
            Action = "REOPEN",
            ActorId = userId.Value,
            At = now,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason
        }, ct);

        var trackedProject = await _projectRepository.GetByIdAsync(projectId, ct);
        if (trackedProject != null)
            trackedProject.UpdatedAt = now;

        await _projectRepository.SaveChangesAsync(ct);

        var dto = await _projectRepository.BuildWorkflowDtoAsync(access.Project, ct);
        return ServiceResult<WorkflowDto>.Ok(dto);
    }

    // --- Helpers ---

    private async Task<List<ProjectDto>> FilterAndMapProjectsAsync(
        IEnumerable<Project> projects,
        long userId,
        string? status,
        string? search,
        CancellationToken ct)
    {
        var dtos = new List<ProjectDto>();
        foreach (var p in projects)
        {
            var access = await _permissionService.GetProjectAccessAsync(userId, p.Id, ct);
            if (access == null)
                continue;

            var dto = await _projectRepository.BuildProjectDtoAsync(p, access.Workspace, userId, access.MyRole, access.Permissions, ct);
            dtos.Add(dto);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var upper = status.Trim().ToUpperInvariant();
            dtos = dtos.Where(d => d.Status == upper).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            dtos = dtos
                .Where(d => d.Name.ToLowerInvariant().Contains(term) || d.Key.ToLowerInvariant().Contains(term))
                .ToList();
        }

        return dtos.OrderByDescending(d => d.UpdatedAt).ToList();
    }

    private static ServiceResult EnsureProjectWritable(Workspace ws, Project project)
    {
        if (!ws.IsActive)
            return ServiceResult.Conflict("WORKSPACE_ARCHIVED", "Workspace đã được lưu trữ, dữ liệu chỉ đọc.");
        if (!project.IsActive)
            return ServiceResult.Conflict("PROJECT_ARCHIVED", "Dự án đã được lưu trữ, dữ liệu chỉ đọc.");
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult<CompanyRole>> RequireAssignableProjectRoleAsync(long companyId, long roleId, CancellationToken ct)
    {
        var role = await _companyRepository.GetRoleByIdAsync(companyId, roleId, ct);
        if (role == null)
            return ServiceResult<CompanyRole>.NotFound("Không tìm thấy dữ liệu.");
        if (role.Scope != RoleScopes.Project)
            return ServiceResult<CompanyRole>.BadRequest("ROLE_SCOPE_MISMATCH", $"Role này thuộc phạm vi {role.Scope}, không gán ở đây được.");
        if (!role.IsActive)
            return ServiceResult<CompanyRole>.BadRequest("ROLE_INACTIVE", "Role đang tắt, không gán được.");
        return ServiceResult<CompanyRole>.Ok(role);
    }

    private async Task<ServiceResult> ValidateTransitionRolesAsync(long companyId, IEnumerable<TransitionInputDto> transitions, CancellationToken ct)
    {
        foreach (var t in transitions)
        {
            foreach (var roleId in t.AllowedRoleIds ?? Enumerable.Empty<long>())
            {
                var check = await RequireAssignableProjectRoleAsync(companyId, roleId, ct);
                if (!check.IsSuccess)
                    return check;
            }
        }
        return ServiceResult.Ok();
    }

    private async Task<string> GenerateUniqueProjectKeyAsync(long workspaceId, string baseKey, CancellationToken ct)
    {
        if (!await _projectRepository.KeyExistsAsync(workspaceId, baseKey, ct))
            return baseKey;

        var prefix = baseKey.Length > 9 ? baseKey[..9] : baseKey;
        for (int i = 2; ; i++)
        {
            var candidate = $"{prefix}{i}";
            if (candidate.Length > 10)
                candidate = candidate[..10];
            if (!await _projectRepository.KeyExistsAsync(workspaceId, candidate, ct))
                return candidate;
        }
    }
}
