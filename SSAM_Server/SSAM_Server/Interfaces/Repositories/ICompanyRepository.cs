using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Interfaces.Repositories;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(long companyId, CancellationToken ct = default);
    Task<Company?> GetActiveByDomainAsync(string domain, CancellationToken ct = default);
    Task<bool> AnyActiveCompanyExistsAsync(CancellationToken ct = default);
    Task<Company?> GetFirstActiveCompanyAsync(CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task AddCompanyAsync(Company company, CancellationToken ct = default);

    Task AddRolesAsync(IEnumerable<CompanyRole> roles, CancellationToken ct = default);
    Task SeedSystemRolesAndPermissionsAsync(long companyId, DateTime now, CancellationToken ct = default);
    Task<CompanyRole?> GetRoleByIdAsync(long companyId, long roleId, CancellationToken ct = default);
    Task<CompanyRole?> GetRoleByCodeAsync(long companyId, string code, CancellationToken ct = default);
    Task<long> ResolveRoleIdAsync(long companyId, string? roleName, string expectedScope = RoleScopes.Tenant, string fallbackCode = CompanyRoleCodes.Member, CancellationToken ct = default);

    Task<CompanyMember?> GetMemberAsync(long companyId, long userId, CancellationToken ct = default);
    Task<bool> IsMemberAsync(long companyId, long userId, CancellationToken ct = default);
    Task<bool> IsEmailMemberAsync(long companyId, string email, CancellationToken ct = default);
    Task<bool> HasAnyActiveCompanyAsync(long userId, long? excludeCompanyId = null, CancellationToken ct = default);
    Task<bool> IsEmailInAnyOtherCompanyAsync(long excludeCompanyId, string email, CancellationToken ct = default);
    Task<bool> IsAdminAsync(long companyId, long userId, CancellationToken ct = default);
    Task<int> CountActiveTenantAdminsAsync(long companyId, CancellationToken ct = default);
    Task AddMemberAsync(CompanyMember member, CancellationToken ct = default);

    Task<PageDto<CompanyUserDto>> ListCompanyUsersAsync(long companyId, string? search, long? roleId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<CompanyUserDto?> GetCompanyUserDtoAsync(long companyId, long userId, CancellationToken ct = default);

    Task AddMembershipRequestAsync(MembershipRequest request, CancellationToken ct = default);
    Task<MembershipRequest?> GetJoinRequestAsync(long companyId, long requestId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default);
}
