using SSAM.Application.Common.Interfaces.Repositories;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Companies;
using SSAM.Domain.Entities.Companies;

namespace SSAM.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IUnitOfWork _unitOfWork;

    public CompanyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CompanyResponseDto> CreateCompanyAsync(
        CreateCompanyRequestDto request, 
        long creatorUserId, 
        CancellationToken cancellationToken = default)
    {
        var codeExists = await _unitOfWork.Companies.ExistsAsync(c => c.Code == request.Code, cancellationToken);
        if (codeExists)
        {
            throw new InvalidOperationException($"Company code '{request.Code}' is already in use.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Create Company
            var company = new Company
            {
                Name = request.Name,
                Code = request.Code,
                EmailDomain = request.EmailDomain,
                DomainAutoJoinEnabled = request.DomainAutoJoinEnabled,
                TaxCode = request.TaxCode,
                Address = request.Address,
                Industry = request.Industry,
                CompanySize = request.CompanySize,
                LogoUrl = request.LogoUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Companies.AddAsync(company, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 2. Create Default Owner Role for this company
            var ownerRole = new CompanyRole
            {
                CompanyId = company.Id,
                Name = "Company Admin",
                Code = "TENANT_ADMIN",
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.CompanyRoles.AddAsync(ownerRole, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 3. Assign Creator as Member with Owner Role
            var member = new CompanyMember
            {
                CompanyId = company.Id,
                UserId = creatorUserId,
                RoleId = ownerRole.Id,
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };
            await _unitOfWork.CompanyMembers.AddAsync(member, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return MapToResponse(company);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CompanyResponseDto?> GetCompanyByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(id, cancellationToken);
        return company == null ? null : MapToResponse(company);
    }

    public async Task<IEnumerable<CompanyResponseDto>> GetAllActiveCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _unitOfWork.Companies.FindAsync(c => c.IsActive, cancellationToken);
        return companies.Select(MapToResponse);
    }

    private static CompanyResponseDto MapToResponse(Company company)
    {
        return new CompanyResponseDto
        {
            Id = company.Id,
            Name = company.Name,
            Code = company.Code,
            EmailDomain = company.EmailDomain,
            DomainAutoJoinEnabled = company.DomainAutoJoinEnabled,
            TaxCode = company.TaxCode,
            Address = company.Address,
            Industry = company.Industry,
            CompanySize = company.CompanySize,
            LogoUrl = company.LogoUrl,
            IsActive = company.IsActive,
            CreatedAt = company.CreatedAt
        };
    }
}
