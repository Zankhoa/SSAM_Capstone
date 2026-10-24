using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Roles;

namespace SSAM_Server.Interfaces.Services;

public interface IRoleService
{
    Task<ServiceResult<ItemsDto<RoleDto>>> ListRolesAsync(long companyId, string? scope, bool includeInactive, CancellationToken ct = default);
    Task<ServiceResult<RoleDto>> GetRoleAsync(long companyId, long roleId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<RoleMemberDto>>> GetRoleMembersAsync(long companyId, long roleId, CancellationToken ct = default);
    Task<ServiceResult<RoleDto>> CreateRoleAsync(long companyId, CreateRoleRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<RoleDto>> UpdateRoleAsync(long companyId, long roleId, UpdateRoleRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<RoleDto>> UpdateRoleStatusAsync(long companyId, long roleId, UpdateRoleStatusRequestDto request, CancellationToken ct = default);
}
