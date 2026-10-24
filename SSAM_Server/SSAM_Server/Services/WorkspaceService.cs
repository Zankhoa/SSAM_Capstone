using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Workspaces;
using SSAM_Server.Models;
using SSAM_Server.Repositories;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        ICompanyRepository companyRepository,
        IPermissionService permissionService,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _workspaceRepository = workspaceRepository;
        _companyRepository = companyRepository;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ServiceResult<ItemsDto<WorkspaceDto>>> ListWorkspacesAsync(
        long? companyId,
        string? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<WorkspaceDto>>.Unauthorized("Your session is invalid or has expired.");

        if (companyId.HasValue)
        {
            var tenantAuth = await _permissionService.RequireTenantAsync(userId.Value, companyId.Value, null, cancellationToken);
            if (!tenantAuth.IsSuccess)
                return ServiceResult<ItemsDto<WorkspaceDto>>.FromError(tenantAuth);
        }

        var workspaces = await _workspaceRepository.ListByCompanyOrPersonalAsync(companyId, userId.Value, cancellationToken);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var upper = status.Trim().ToUpperInvariant();
            if (upper == "ACTIVE")
                workspaces = workspaces.Where(w => w.IsActive).ToList();
            else if (upper == "ARCHIVED")
                workspaces = workspaces.Where(w => !w.IsActive).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            workspaces = workspaces
                .Where(w => w.Name.ToLowerInvariant().Contains(term) || w.Code.ToLowerInvariant().Contains(term))
                .ToList();
        }

        var dtos = new List<WorkspaceDto>();
        foreach (var ws in workspaces)
        {
            var access = await _permissionService.GetWorkspaceAccessAsync(userId.Value, ws.Id, cancellationToken);
            if (access == null)
                continue;

            var dto = await _workspaceRepository.BuildWorkspaceDtoAsync(ws, userId.Value, access.MyRole, access.Permissions, cancellationToken);
            dtos.Add(dto);
        }

        return ServiceResult<ItemsDto<WorkspaceDto>>.Ok(new ItemsDto<WorkspaceDto>(dtos));
    }

    public async Task<ServiceResult<WorkspaceDto>> GetWorkspaceAsync(long workspaceId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkspaceDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, null, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<WorkspaceDto>.FromError(auth);

        var access = auth.Data!;
        var dto = await _workspaceRepository.BuildWorkspaceDtoAsync(access.Workspace, userId.Value, access.MyRole, access.Permissions, cancellationToken);
        return ServiceResult<WorkspaceDto>.Ok(dto);
    }

    public async Task<ServiceResult<WorkspaceCodeAvailabilityDto>> CheckCodeAvailabilityAsync(
        long companyId,
        string? code,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkspaceCodeAvailabilityDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, null, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<WorkspaceCodeAvailabilityDto>.FromError(auth);

        var rawCode = (code ?? string.Empty).Trim();
        var validFormat = rawCode.Length >= 2 && rawCode.Length <= 50 && SlugHelper.WorkspaceCodeRegex().IsMatch(rawCode);
        var available = validFormat && !await _workspaceRepository.CodeExistsAsync(companyId, rawCode, null, cancellationToken);

        string? suggestion = null;
        if (!available)
        {
            var baseSlug = SlugHelper.Slugify(rawCode);
            if (string.IsNullOrEmpty(baseSlug) || baseSlug.Length < 2)
                baseSlug = "workspace";
            suggestion = await GenerateUniqueWorkspaceCodeAsync(companyId, baseSlug, null, cancellationToken);
        }

        return ServiceResult<WorkspaceCodeAvailabilityDto>.Ok(new WorkspaceCodeAvailabilityDto
        {
            Code = rawCode,
            Available = available,
            Suggestion = suggestion
        });
    }

    public async Task<ServiceResult<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequestDto request, CancellationToken cancellationToken = default)
    {
        var creatorUserId = _currentUser.UserIdOrNull;
        if (creatorUserId == null)
            return ServiceResult<WorkspaceDto>.Unauthorized("Your session is invalid or has expired.");

        var name = (request.Name ?? string.Empty).Trim();
        var rawCode = (request.Code ?? string.Empty).Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var validationErrors = new Dictionary<string, string[]>();
        if (name.Length < 2 || name.Length > 255)
            validationErrors["name"] = new[] { "Tên workspace từ 2 đến 255 ký tự" };
        if (!string.IsNullOrEmpty(rawCode) && (rawCode.Length < 2 || rawCode.Length > 50 || !SlugHelper.WorkspaceCodeRegex().IsMatch(rawCode)))
            validationErrors["code"] = new[] { "Mã chỉ gồm chữ thường, số và dấu \"-\" (2–50 ký tự)" };
        if (description != null && description.Length > 1000)
            validationErrors["description"] = new[] { "Mô tả tối đa 1000 ký tự" };

        if (validationErrors.Count > 0)
            return ServiceResult<WorkspaceDto>.Validation(validationErrors);

        if (request.CompanyId.HasValue)
        {
            var auth = await _permissionService.RequireTenantAsync(creatorUserId.Value, request.CompanyId.Value, PermissionKeys.WorkspaceCreate, cancellationToken);
            if (!auth.IsSuccess)
                return ServiceResult<WorkspaceDto>.FromError(auth);
        }
        else
        {
            if (await _workspaceRepository.PersonalWorkspaceExistsAsync(creatorUserId.Value, cancellationToken))
                return ServiceResult<WorkspaceDto>.Conflict("CONFLICT", "Bạn đã có workspace cá nhân.");
        }

        string code;
        if (!string.IsNullOrEmpty(rawCode))
        {
            if (request.CompanyId.HasValue && await _workspaceRepository.CodeExistsAsync(request.CompanyId.Value, rawCode, null, cancellationToken))
                return ServiceResult<WorkspaceDto>.Conflict("WORKSPACE_CODE_TAKEN", "Mã workspace đã được dùng trong công ty.");
            code = rawCode;
        }
        else
        {
            var baseSlug = SlugHelper.Slugify(name);
            if (string.IsNullOrEmpty(baseSlug) || baseSlug.Length < 2)
                baseSlug = "workspace";
            code = await GenerateUniqueWorkspaceCodeAsync(request.CompanyId, baseSlug, null, cancellationToken);
        }

        var dto = await _workspaceRepository.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            var workspace = new Workspace
            {
                Name = name,
                Code = code,
                Description = description,
                CompanyId = request.CompanyId,
                CreatedBy = creatorUserId.Value,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _workspaceRepository.AddWorkspaceAsync(workspace, ct);
            await _workspaceRepository.SaveChangesAsync(ct);

            if (request.CompanyId.HasValue)
            {
                var wsAdminRole = await _companyRepository.GetRoleByCodeAsync(request.CompanyId.Value, CompanyRoleCodes.WorkspaceAdmin, ct);
                await _workspaceRepository.AddMemberAsync(new WorkspaceMember
                {
                    WorkspaceId = workspace.Id,
                    UserId = creatorUserId.Value,
                    RoleId = wsAdminRole?.Id,
                    IsActive = true,
                    JoinedAt = now
                }, ct);
                await _workspaceRepository.SaveChangesAsync(ct);
            }

            var access = await _permissionService.GetWorkspaceAccessAsync(creatorUserId.Value, workspace.Id, ct);
            return await _workspaceRepository.BuildWorkspaceDtoAsync(
                workspace,
                creatorUserId.Value,
                access?.MyRole,
                access?.Permissions ?? PermissionKeys.AllWorkspaceKeys.ToHashSet(),
                ct);
        }, cancellationToken);

        return ServiceResult<WorkspaceDto>.Created(dto);
    }

    public async Task<ServiceResult<WorkspaceDto>> UpdateWorkspaceAsync(
        long workspaceId,
        UpdateWorkspaceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkspaceDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.WorkspaceUpdate, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<WorkspaceDto>.FromError(auth);

        var access = auth.Data!;
        if (!access.Workspace.IsActive)
            return ServiceResult<WorkspaceDto>.Conflict("WORKSPACE_ARCHIVED", "Workspace đã được lưu trữ, dữ liệu chỉ đọc.");

        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim();
        var code = request.Code?.Trim();

        if (name != null && (name.Length < 2 || name.Length > 255))
            errors["name"] = new[] { "Tên workspace từ 2 đến 255 ký tự" };
        if (code != null && (code.Length < 2 || code.Length > 50 || !SlugHelper.WorkspaceCodeRegex().IsMatch(code)))
            errors["code"] = new[] { "Mã chỉ gồm chữ thường, số và dấu \"-\" (2–50 ký tự)" };

        if (errors.Count > 0)
            return ServiceResult<WorkspaceDto>.Validation(errors);

        if (code != null && code != access.Workspace.Code && access.Workspace.CompanyId.HasValue)
        {
            if (await _workspaceRepository.CodeExistsAsync(access.Workspace.CompanyId.Value, code, workspaceId, cancellationToken))
                return ServiceResult<WorkspaceDto>.Conflict("WORKSPACE_CODE_TAKEN", "Mã workspace đã được dùng trong công ty.");
        }

        var tracked = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        if (tracked == null)
            return ServiceResult<WorkspaceDto>.NotFound("Không tìm thấy dữ liệu.");

        if (name != null)
            tracked.Name = name;
        if (code != null)
            tracked.Code = code;
        if (request.DescriptionSpecified)
            tracked.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        tracked.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        var dto = await _workspaceRepository.BuildWorkspaceDtoAsync(tracked, userId.Value, access.MyRole, access.Permissions, cancellationToken);
        return ServiceResult<WorkspaceDto>.Ok(dto);
    }

    public async Task<ServiceResult<WorkspaceDto>> ArchiveWorkspaceAsync(long workspaceId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkspaceDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.WorkspaceArchive, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<WorkspaceDto>.FromError(auth);

        var access = auth.Data!;
        if (!access.Workspace.IsActive)
            return ServiceResult<WorkspaceDto>.Conflict("WORKSPACE_ARCHIVED", "Workspace đã được lưu trữ, dữ liệu chỉ đọc.");

        var tracked = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        if (tracked == null)
            return ServiceResult<WorkspaceDto>.NotFound("Không tìm thấy dữ liệu.");

        var now = _clock.GetUtcNow().UtcDateTime;
        tracked.IsActive = false;
        tracked.ArchivedAt = now;
        tracked.UpdatedAt = now;
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        var dto = await _workspaceRepository.BuildWorkspaceDtoAsync(tracked, userId.Value, access.MyRole, access.Permissions, cancellationToken);
        return ServiceResult<WorkspaceDto>.Ok(dto);
    }

    public async Task<ServiceResult<WorkspaceDto>> RestoreWorkspaceAsync(long workspaceId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkspaceDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.WorkspaceArchive, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<WorkspaceDto>.FromError(auth);

        var access = auth.Data!;
        var tracked = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        if (tracked == null)
            return ServiceResult<WorkspaceDto>.NotFound("Không tìm thấy dữ liệu.");

        tracked.IsActive = true;
        tracked.ArchivedAt = null;
        tracked.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        var dto = await _workspaceRepository.BuildWorkspaceDtoAsync(tracked, userId.Value, access.MyRole, access.Permissions, cancellationToken);
        return ServiceResult<WorkspaceDto>.Ok(dto);
    }

    public async Task<ServiceResult<ItemsDto<WorkspaceMemberDto>>> ListMembersAsync(
        long workspaceId,
        long? roleId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.WorkspaceMemberView, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.FromError(auth);

        var ws = auth.Data!.Workspace;
        if (ws.CompanyId == null)
            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.BadRequest("PERSONAL_WORKSPACE", "Workspace cá nhân không có thành viên khác.");

        var items = await _workspaceRepository.ListMembersAsync(workspaceId, ws.CompanyId.Value, roleId, search, cancellationToken);
        return ServiceResult<ItemsDto<WorkspaceMemberDto>>.Ok(new ItemsDto<WorkspaceMemberDto>(items));
    }

    public async Task<ServiceResult<ItemsDto<UserRefDto>>> ListMemberCandidatesAsync(
        long workspaceId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<UserRefDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.WorkspaceMemberManage, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<ItemsDto<UserRefDto>>.FromError(auth);

        var ws = auth.Data!.Workspace;
        if (ws.CompanyId == null)
            return ServiceResult<ItemsDto<UserRefDto>>.BadRequest("PERSONAL_WORKSPACE", "Workspace cá nhân không có thành viên khác.");

        var items = await _workspaceRepository.ListMemberCandidatesAsync(workspaceId, ws.CompanyId.Value, search, cancellationToken);
        return ServiceResult<ItemsDto<UserRefDto>>.Ok(new ItemsDto<UserRefDto>(items));
    }

    public async Task<ServiceResult<ItemsDto<WorkspaceMemberDto>>> AddMembersAsync(
        long workspaceId,
        AddWorkspaceMembersRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.WorkspaceMemberManage, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.FromError(auth);

        var ws = auth.Data!.Workspace;
        if (ws.CompanyId == null)
            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.BadRequest("PERSONAL_WORKSPACE", "Workspace cá nhân không có thành viên khác.");

        if (!ws.IsActive)
            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.Conflict("WORKSPACE_ARCHIVED", "Workspace đã được lưu trữ, dữ liệu chỉ đọc.");

        if (request.Members == null || request.Members.Count == 0)
        {
            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.Validation(new Dictionary<string, string[]>
            {
                ["members"] = new[] { "Chọn ít nhất 1 thành viên" }
            });
        }

        var companyId = ws.CompanyId.Value;
        var defaultWsMemberRole = await _companyRepository.GetRoleByCodeAsync(companyId, CompanyRoleCodes.WorkspaceMember, cancellationToken);

        var resolved = new List<(long UserId, long RoleId)>();
        foreach (var item in request.Members)
        {
            if (!await _companyRepository.IsMemberAsync(companyId, item.UserId, cancellationToken))
                return ServiceResult<ItemsDto<WorkspaceMemberDto>>.Forbidden("NOT_COMPANY_MEMBER", "Người dùng không thuộc công ty.");

            if (await _workspaceRepository.IsMemberAsync(workspaceId, item.UserId, cancellationToken))
                return ServiceResult<ItemsDto<WorkspaceMemberDto>>.Conflict("ALREADY_MEMBER", "Người dùng đã là thành viên.");

            long roleId;
            if (item.RoleId.HasValue)
            {
                var role = await _companyRepository.GetRoleByIdAsync(companyId, item.RoleId.Value, cancellationToken);
                if (role == null)
                    return ServiceResult<ItemsDto<WorkspaceMemberDto>>.NotFound("Không tìm thấy dữ liệu.");
                if (role.Scope != RoleScopes.Workspace)
                    return ServiceResult<ItemsDto<WorkspaceMemberDto>>.BadRequest("ROLE_SCOPE_MISMATCH", $"Role này thuộc phạm vi {role.Scope}, không gán ở đây được.");
                if (!role.IsActive)
                    return ServiceResult<ItemsDto<WorkspaceMemberDto>>.BadRequest("ROLE_INACTIVE", "Role đang tắt, không gán được.");
                roleId = role.Id;
            }
            else
            {
                roleId = defaultWsMemberRole!.Id;
            }

            resolved.Add((item.UserId, roleId));
        }

        return await _workspaceRepository.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            foreach (var (memberUserId, memberRoleId) in resolved)
            {
                var previous = await _workspaceRepository.GetMemberRecordAsync(workspaceId, memberUserId, ct);
                if (previous != null)
                {
                    previous.IsActive = true;
                    previous.RoleId = memberRoleId;
                    previous.JoinedAt = now;
                }
                else
                {
                    await _workspaceRepository.AddMemberAsync(new WorkspaceMember
                    {
                        WorkspaceId = workspaceId,
                        UserId = memberUserId,
                        RoleId = memberRoleId,
                        IsActive = true,
                        JoinedAt = now
                    }, ct);
                }
            }

            var trackedWs = await _workspaceRepository.GetByIdAsync(workspaceId, ct);
            if (trackedWs != null)
                trackedWs.UpdatedAt = now;

            await _workspaceRepository.SaveChangesAsync(ct);

            var addedDtos = new List<WorkspaceMemberDto>();
            foreach (var (memberUserId, _) in resolved)
            {
                var dto = await _workspaceRepository.GetMemberDtoAsync(workspaceId, companyId, memberUserId, ct);
                if (dto != null)
                    addedDtos.Add(dto);
            }

            return ServiceResult<ItemsDto<WorkspaceMemberDto>>.Created(new ItemsDto<WorkspaceMemberDto>(addedDtos));
        }, cancellationToken);
    }

    public async Task<ServiceResult<WorkspaceMemberDto>> UpdateMemberRoleAsync(
        long workspaceId,
        long targetUserId,
        UpdateWorkspaceMemberRoleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<WorkspaceMemberDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, PermissionKeys.WorkspaceMemberManage, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<WorkspaceMemberDto>.FromError(auth);

        var ws = auth.Data!.Workspace;
        if (ws.CompanyId == null)
            return ServiceResult<WorkspaceMemberDto>.BadRequest("PERSONAL_WORKSPACE", "Workspace cá nhân không có thành viên khác.");

        if (!ws.IsActive)
            return ServiceResult<WorkspaceMemberDto>.Conflict("WORKSPACE_ARCHIVED", "Workspace đã được lưu trữ, dữ liệu chỉ đọc.");

        var companyId = ws.CompanyId.Value;
        var member = await _workspaceRepository.GetActiveMemberAsync(workspaceId, targetUserId, cancellationToken);
        if (member == null)
            return ServiceResult<WorkspaceMemberDto>.NotFound("Không tìm thấy dữ liệu.");

        var role = await _companyRepository.GetRoleByIdAsync(companyId, request.RoleId, cancellationToken);
        if (role == null)
            return ServiceResult<WorkspaceMemberDto>.NotFound("Không tìm thấy dữ liệu.");
        if (role.Scope != RoleScopes.Workspace)
            return ServiceResult<WorkspaceMemberDto>.BadRequest("ROLE_SCOPE_MISMATCH", $"Role này thuộc phạm vi {role.Scope}, không gán ở đây được.");
        if (!role.IsActive)
            return ServiceResult<WorkspaceMemberDto>.BadRequest("ROLE_INACTIVE", "Role đang tắt, không gán được.");

        if (member.Role?.Code == CompanyRoleCodes.WorkspaceAdmin && role.Code != CompanyRoleCodes.WorkspaceAdmin)
        {
            var adminCount = await _workspaceRepository.CountActiveWorkspaceAdminsAsync(workspaceId, cancellationToken);
            if (adminCount <= 1)
                return ServiceResult<WorkspaceMemberDto>.Conflict("LAST_WORKSPACE_ADMIN", "Workspace phải còn ít nhất 1 Workspace Admin.");
        }

        member.RoleId = role.Id;
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        var dto = await _workspaceRepository.GetMemberDtoAsync(workspaceId, companyId, targetUserId, cancellationToken);
        return ServiceResult<WorkspaceMemberDto>.Ok(dto!);
    }

    public async Task<ServiceResult> RemoveMemberAsync(long workspaceId, long targetUserId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult.Unauthorized("Your session is invalid or has expired.");

        var requiredPerm = targetUserId == userId.Value ? null : PermissionKeys.WorkspaceMemberManage;
        var auth = await _permissionService.RequireWorkspaceAsync(userId.Value, workspaceId, requiredPerm, cancellationToken);
        if (!auth.IsSuccess)
            return auth;

        var ws = auth.Data!.Workspace;
        if (ws.CompanyId == null)
            return ServiceResult.BadRequest("PERSONAL_WORKSPACE", "Workspace cá nhân không có thành viên khác.");

        if (!ws.IsActive)
            return ServiceResult.Conflict("WORKSPACE_ARCHIVED", "Workspace đã được lưu trữ, dữ liệu chỉ đọc.");

        var member = await _workspaceRepository.GetActiveMemberAsync(workspaceId, targetUserId, cancellationToken);
        if (member == null)
            return ServiceResult.NotFound("Không tìm thấy dữ liệu.");

        if (member.Role?.Code == CompanyRoleCodes.WorkspaceAdmin)
        {
            var adminCount = await _workspaceRepository.CountActiveWorkspaceAdminsAsync(workspaceId, cancellationToken);
            if (adminCount <= 1)
                return ServiceResult.Conflict("LAST_WORKSPACE_ADMIN", "Workspace phải còn ít nhất 1 Workspace Admin.");
        }

        var solePmProjectKey = await _workspaceRepository.FindSoleProjectManagerProjectKeyAsync(workspaceId, targetUserId, cancellationToken);
        if (solePmProjectKey != null)
        {
            return ServiceResult.Conflict("LAST_PROJECT_MANAGER", $"Người này là Project Manager duy nhất của dự án {solePmProjectKey} — chuyển PM trước khi gỡ.");
        }

        return await _workspaceRepository.ExecuteInTransactionAsync(async ct =>
        {
            await _workspaceRepository.DeactivateProjectMembershipsInWorkspaceAsync(workspaceId, targetUserId, ct);

            var trackedMember = await _workspaceRepository.GetActiveMemberAsync(workspaceId, targetUserId, ct);
            if (trackedMember != null)
                trackedMember.IsActive = false;

            var trackedWs = await _workspaceRepository.GetByIdAsync(workspaceId, ct);
            if (trackedWs != null)
                trackedWs.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

            await _workspaceRepository.SaveChangesAsync(ct);
            return ServiceResult.NoContent();
        }, cancellationToken);
    }

    private async Task<string> GenerateUniqueWorkspaceCodeAsync(long? companyId, string baseSlug, long? excludeId, CancellationToken ct)
    {
        if (!companyId.HasValue)
            return baseSlug;

        if (!await _workspaceRepository.CodeExistsAsync(companyId.Value, baseSlug, excludeId, ct))
            return baseSlug;

        var prefix = baseSlug.Length > 46 ? baseSlug[..46].TrimEnd('-') : baseSlug;
        for (int i = 2; ; i++)
        {
            var candidate = $"{prefix}-{i}";
            if (!await _workspaceRepository.CodeExistsAsync(companyId.Value, candidate, excludeId, ct))
                return candidate;
        }
    }
}
