using SSAM_Server.DTOs.Roles;
using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IRoleRepository
{
    Task<List<CompanyRole>> ListRolesAsync(long companyId, string? scope, bool includeInactive, CancellationToken ct = default);
    Task<CompanyRole?> GetRoleByIdAsync(long companyId, long roleId, CancellationToken ct = default);
    Task<bool> RoleNameExistsAsync(long companyId, string name, long? excludeRoleId = null, CancellationToken ct = default);
    Task<bool> RoleCodeExistsAsync(long companyId, string code, long? excludeRoleId = null, CancellationToken ct = default);
    Task<List<Permission>> GetPermissionsByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);
    Task<Dictionary<long, int>> GetMemberCountsAsync(long companyId, IEnumerable<CompanyRole> roles, CancellationToken ct = default);
    Task<int> GetSingleRoleMemberCountAsync(CompanyRole role, CancellationToken ct = default);
    Task<List<RoleMemberDto>> GetRoleMembersAsync(CompanyRole role, CancellationToken ct = default);
    Task AddRoleAsync(CompanyRole role, CancellationToken ct = default);
    Task ReplaceRolePermissionsAsync(long roleId, IEnumerable<long> permissionIds, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default);
}
