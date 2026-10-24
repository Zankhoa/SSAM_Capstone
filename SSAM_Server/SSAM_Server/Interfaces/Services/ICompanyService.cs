using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;

namespace SSAM_Server.Interfaces.Services;

public interface ICompanyService
{
    Task<ServiceResult<CompanyMatchResponseDto>> MatchDomainAsync(string email, CancellationToken cancellationToken = default);
    Task<ServiceResult<CompanyDto>> CreateCompanyAsync(CreateCompanyRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<CompanyJoinResponseDto>> JoinCompanyAsync(long companyId, CancellationToken cancellationToken = default);
    Task<ServiceResult<JoinRequestResponseDto>> CreateJoinRequestAsync(long companyId, CancellationToken cancellationToken = default);
    Task<ServiceResult<JoinRequestStatusResponseDto>> GetJoinRequestStatusAsync(long companyId, long requestId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PageDto<CompanyUserDto>>> ListUsersAsync(long companyId, string? search, long? roleId, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ServiceResult<CompanyUserDto>> UpdateUserRoleAsync(long companyId, long targetUserId, UpdateCompanyUserRoleRequestDto request, CancellationToken cancellationToken = default);
}
