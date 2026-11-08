using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Roles;
using SSAM_Server.Models;
using SSAM_Server.Repositories;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public RoleService(
        IRoleRepository roleRepository,
        IPermissionService permissionService,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _roleRepository = roleRepository;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ServiceResult<ItemsDto<RoleDto>>> ListRolesAsync(
        long companyId,
        string? scope,
        bool includeInactive,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<RoleDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(
            userId.Value,
            companyId,
            includeInactive ? PermissionKeys.RoleView : null,
            ct);
        if (!auth.IsSuccess)
            return ServiceResult<ItemsDto<RoleDto>>.FromError(auth);

        var roles = await _roleRepository.ListRolesAsync(companyId, scope, includeInactive, ct);
        var counts = await _roleRepository.GetMemberCountsAsync(companyId, roles, ct);

        var dtos = roles.Select(r => MapToDto(r, counts.GetValueOrDefault(r.Id, 0))).ToList();
        return ServiceResult<ItemsDto<RoleDto>>.Ok(new ItemsDto<RoleDto>(dtos));
    }

    public async Task<ServiceResult<RoleDto>> GetRoleAsync(long companyId, long roleId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<RoleDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.RoleView, ct);
        if (!auth.IsSuccess)
            return ServiceResult<RoleDto>.FromError(auth);

        var role = await _roleRepository.GetRoleByIdAsync(companyId, roleId, ct);
        if (role == null)
            return ServiceResult<RoleDto>.NotFound("Không tìm thấy dữ liệu.");

        var count = await _roleRepository.GetSingleRoleMemberCountAsync(role, ct);
        return ServiceResult<RoleDto>.Ok(MapToDto(role, count));
    }

    public async Task<ServiceResult<ItemsDto<RoleMemberDto>>> GetRoleMembersAsync(long companyId, long roleId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<ItemsDto<RoleMemberDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.RoleView, ct);
        if (!auth.IsSuccess)
            return ServiceResult<ItemsDto<RoleMemberDto>>.FromError(auth);

        var role = await _roleRepository.GetRoleByIdAsync(companyId, roleId, ct);
        if (role == null)
            return ServiceResult<ItemsDto<RoleMemberDto>>.NotFound("Không tìm thấy dữ liệu.");

        var members = await _roleRepository.GetRoleMembersAsync(role, ct);
        return ServiceResult<ItemsDto<RoleMemberDto>>.Ok(new ItemsDto<RoleMemberDto>(members));
    }

    public async Task<ServiceResult<RoleDto>> CreateRoleAsync(long companyId, CreateRoleRequestDto request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<RoleDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.RoleManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<RoleDto>.FromError(auth);

        var name = (request.Name ?? string.Empty).Trim();
        var scope = (request.Scope ?? string.Empty).Trim().ToUpperInvariant();
        var rawCode = (request.Code ?? string.Empty).Trim();
        var kind = RoleKinds.Other;

        var errors = new Dictionary<string, string[]>();
        if (name.Length < 2 || name.Length > 100)
            errors["name"] = new[] { "Tên role từ 2 đến 100 ký tự" };
        if (!RoleScopes.IsValid(scope))
            errors["scope"] = new[] { "Chọn phạm vi áp dụng" };
        if (scope == RoleScopes.Project && !string.IsNullOrWhiteSpace(request.Kind))
        {
            var candidateKind = request.Kind.Trim().ToUpperInvariant();
            if (!RoleKinds.IsValid(candidateKind))
                errors["kind"] = new[] { "Chọn nhóm chuyên môn hợp lệ" };
            else
                kind = candidateKind;
        }
        if (!string.IsNullOrEmpty(rawCode) && !SlugHelper.RoleCodeRegex().IsMatch(rawCode))
            errors["code"] = new[] { "Mã viết HOA, bắt đầu bằng chữ, chỉ gồm A-Z 0-9 _" };

        if (errors.Count > 0)
            return ServiceResult<RoleDto>.Validation(errors);

        if (await _roleRepository.RoleNameExistsAsync(companyId, name, null, ct))
            return ServiceResult<RoleDto>.Conflict("ROLE_NAME_TAKEN", "Tên role đã tồn tại trong công ty.");

        string code;
        if (!string.IsNullOrEmpty(rawCode))
        {
            if (await _roleRepository.RoleCodeExistsAsync(companyId, rawCode, null, ct))
                return ServiceResult<RoleDto>.Conflict("ROLE_CODE_TAKEN", "Mã role đã tồn tại trong công ty.");
            code = rawCode;
        }
        else
        {
            var baseCode = SlugHelper.ToRoleCode(name);
            if (string.IsNullOrEmpty(baseCode))
                baseCode = "ROLE";

            code = baseCode;
            for (int i = 2; await _roleRepository.RoleCodeExistsAsync(companyId, code, null, ct); i++)
            {
                var prefix = baseCode.Length > 46 ? baseCode[..46] : baseCode;
                code = $"{prefix}_{i}";
            }
        }

        var permResult = await ValidatePermissionKeysAsync(request.PermissionKeys ?? new List<string>(), scope, ct);
        if (!permResult.IsSuccess)
            return ServiceResult<RoleDto>.FromError(permResult);

        var permissions = permResult.Data!;

        return await _roleRepository.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var role = new CompanyRole
            {
                CompanyId = companyId,
                Name = name,
                Code = code,
                Scope = scope,
                Kind = kind,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsSystem = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _roleRepository.AddRoleAsync(role, innerCt);
            await _roleRepository.SaveChangesAsync(innerCt);

            if (permissions.Count > 0)
            {
                await _roleRepository.ReplaceRolePermissionsAsync(role.Id, permissions.Select(p => p.Id), innerCt);
                await _roleRepository.SaveChangesAsync(innerCt);
            }

            var created = await _roleRepository.GetRoleByIdAsync(companyId, role.Id, innerCt);
            return ServiceResult<RoleDto>.Created(MapToDto(created!, 0));
        }, ct);
    }

    public async Task<ServiceResult<RoleDto>> UpdateRoleAsync(
        long companyId,
        long roleId,
        UpdateRoleRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<RoleDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.RoleManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<RoleDto>.FromError(auth);

        var role = await _roleRepository.GetRoleByIdAsync(companyId, roleId, ct);
        if (role == null)
            return ServiceResult<RoleDto>.NotFound("Không tìm thấy dữ liệu.");

        if (CompanyRoleCodes.IsLocked(role.Code))
            return ServiceResult<RoleDto>.Forbidden("ROLE_LOCKED", "Role hệ thống này được khoá, không chỉnh sửa được.");

        if (request.Name != null)
        {
            var trimmedName = request.Name.Trim();
            if (trimmedName.Length < 2 || trimmedName.Length > 100)
            {
                return ServiceResult<RoleDto>.Validation(new Dictionary<string, string[]>
                {
                    ["name"] = new[] { "Tên role từ 2 đến 100 ký tự" }
                });
            }

            if (await _roleRepository.RoleNameExistsAsync(companyId, trimmedName, role.Id, ct))
                return ServiceResult<RoleDto>.Conflict("ROLE_NAME_TAKEN", "Tên role đã tồn tại trong công ty.");
        }

        string? validatedKind = null;
        if (request.Kind != null)
        {
            if (role.Scope == RoleScopes.Project)
            {
                var candidateKind = request.Kind.Trim().ToUpperInvariant();
                if (!RoleKinds.IsValid(candidateKind))
                {
                    return ServiceResult<RoleDto>.Validation(new Dictionary<string, string[]>
                    {
                        ["kind"] = new[] { "Chọn nhóm chuyên môn hợp lệ" }
                    });
                }
                validatedKind = candidateKind;
            }
            else
            {
                validatedKind = RoleKinds.Other;
            }
        }

        List<Permission>? validatedPermissions = null;
        if (request.PermissionKeys != null)
        {
            var permResult = await ValidatePermissionKeysAsync(request.PermissionKeys, role.Scope, ct);
            if (!permResult.IsSuccess)
                return ServiceResult<RoleDto>.FromError(permResult);

            validatedPermissions = permResult.Data!;
        }

        return await _roleRepository.ExecuteInTransactionAsync(async innerCt =>
        {
            var tracked = await _roleRepository.GetRoleByIdAsync(companyId, roleId, innerCt);
            if (tracked == null)
                return ServiceResult<RoleDto>.NotFound("Không tìm thấy dữ liệu.");

            if (request.Name != null)
                tracked.Name = request.Name.Trim();

            if (validatedKind != null)
                tracked.Kind = validatedKind;

            if (request.DescriptionSpecified)
                tracked.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

            tracked.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

            if (validatedPermissions != null)
            {
                await _roleRepository.ReplaceRolePermissionsAsync(tracked.Id, validatedPermissions.Select(p => p.Id), innerCt);
            }

            await _roleRepository.SaveChangesAsync(innerCt);

            var updated = await _roleRepository.GetRoleByIdAsync(companyId, roleId, innerCt);
            var count = await _roleRepository.GetSingleRoleMemberCountAsync(updated!, innerCt);
            return ServiceResult<RoleDto>.Ok(MapToDto(updated!, count));
        }, ct);
    }

    public async Task<ServiceResult<RoleDto>> UpdateRoleStatusAsync(
        long companyId,
        long roleId,
        UpdateRoleStatusRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<RoleDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.RoleManage, ct);
        if (!auth.IsSuccess)
            return ServiceResult<RoleDto>.FromError(auth);

        var role = await _roleRepository.GetRoleByIdAsync(companyId, roleId, ct);
        if (role == null)
            return ServiceResult<RoleDto>.NotFound("Không tìm thấy dữ liệu.");

        if (role.IsSystem)
            return ServiceResult<RoleDto>.Forbidden("ROLE_LOCKED", "Không thể tắt role hệ thống.");

        var count = await _roleRepository.GetSingleRoleMemberCountAsync(role, ct);
        if (!request.IsActive && count > 0)
            return ServiceResult<RoleDto>.Conflict("ROLE_IN_USE", "Role đang được gán cho người dùng — chuyển họ sang role khác trước khi tắt.");

        role.IsActive = request.IsActive;
        role.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _roleRepository.SaveChangesAsync(ct);

        return ServiceResult<RoleDto>.Ok(MapToDto(role, count));
    }

    private async Task<ServiceResult<List<Permission>>> ValidatePermissionKeysAsync(
        IEnumerable<string> keys,
        string roleScope,
        CancellationToken ct)
    {
        var uniqueKeys = keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct().ToList();
        if (uniqueKeys.Count == 0)
            return ServiceResult<List<Permission>>.Ok(new List<Permission>());

        var found = await _roleRepository.GetPermissionsByCodesAsync(uniqueKeys, ct);
        var byCode = found.ToDictionary(p => p.Code, StringComparer.Ordinal);

        foreach (var key in uniqueKeys)
        {
            if (!byCode.TryGetValue(key, out var perm))
                return ServiceResult<List<Permission>>.BadRequest("UNKNOWN_PERMISSION", $"Quyền \"{key}\" không tồn tại.");

            if (perm.Scope != roleScope)
                return ServiceResult<List<Permission>>.BadRequest("PERMISSION_SCOPE_MISMATCH", $"Quyền \"{key}\" không thuộc phạm vi {roleScope}.");
        }

        return ServiceResult<List<Permission>>.Ok(found);
    }

    private static RoleDto MapToDto(CompanyRole role, int memberCount)
    {
        var permissionKeys = role.Code == CompanyRoleCodes.TenantAdmin
            ? PermissionKeys.AllTenantKeys.ToList()
            : role.RolePermissions
                .Where(rp => rp.Permission != null && rp.Permission.IsActive)
                .OrderBy(rp => rp.Permission.SortOrder)
                .Select(rp => rp.Permission.Code)
                .ToList();

        return new RoleDto
        {
            RoleId = role.Id,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description,
            Scope = role.Scope,
            Kind = role.Kind,
            IsSystem = role.IsSystem,
            IsLocked = CompanyRoleCodes.IsLocked(role.Code),
            IsActive = role.IsActive,
            PermissionKeys = permissionKeys,
            MemberCount = memberCount,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };
    }
}
