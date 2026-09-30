using SSAM.Application.DTOs.Companies;

namespace SSAM.Application.Common.Interfaces.Services;

public interface ICompanyService
{
    Task<CompanyResponseDto> CreateCompanyAsync(CreateCompanyRequestDto request, long creatorUserId, CancellationToken cancellationToken = default);
    Task<CompanyResponseDto?> GetCompanyByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyResponseDto>> GetAllActiveCompaniesAsync(CancellationToken cancellationToken = default);
}
