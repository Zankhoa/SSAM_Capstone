using System.Text.RegularExpressions;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Companies;
using SSAM_Server.Models;
using SSAM_Server.Repositories;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class CompanyService : ICompanyService
{
    private static readonly HashSet<string> PublicEmailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", "icloud.com", "aol.com", "mail.com", "zoho.com"
    };

    private readonly ICompanyRepository _companyRepository;
    private readonly IAuthRepository _authRepository;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public CompanyService(
        ICompanyRepository companyRepository,
        IAuthRepository authRepository,
        IPermissionService permissionService,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _companyRepository = companyRepository;
        _authRepository = authRepository;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ServiceResult<CompanyMatchResponseDto>> MatchDomainAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return ServiceResult<CompanyMatchResponseDto>.Ok(new CompanyMatchResponseDto { Matched = false });
        }

        var domain = email.Split('@')[1].Trim().ToLowerInvariant();
        if (PublicEmailDomains.Contains(domain))
        {
            return ServiceResult<CompanyMatchResponseDto>.Ok(new CompanyMatchResponseDto { Matched = false });
        }

        var company = await _companyRepository.GetActiveByDomainAsync(domain, cancellationToken);
        if (company == null)
        {
            return ServiceResult<CompanyMatchResponseDto>.Ok(new CompanyMatchResponseDto { Matched = false });
        }

        return ServiceResult<CompanyMatchResponseDto>.Ok(new CompanyMatchResponseDto
        {
            Matched = true,
            Company = new MatchedCompanyDto
            {
                Id = company.Id,
                Name = company.Name,
                Domain = company.EmailDomain,
                LogoUrl = company.LogoUrl,
                AutoJoinEnabled = company.DomainAutoJoinEnabled
            }
        });
    }

    public async Task<ServiceResult<CompanyDto>> CreateCompanyAsync(CreateCompanyRequestDto request, CancellationToken cancellationToken = default)
    {
        var creatorUserId = _currentUser.UserIdOrNull;
        if (creatorUserId == null)
        {
            return ServiceResult<CompanyDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var validationErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
            validationErrors["name"] = new[] { "Tên công ty là bắt buộc." };
        if (string.IsNullOrWhiteSpace(request.WorkspaceSlug))
            validationErrors["workspaceSlug"] = new[] { "Workspace slug là bắt buộc." };
        else if (!Regex.IsMatch(request.WorkspaceSlug, "^[a-z0-9-]+$"))
            validationErrors["workspaceSlug"] = new[] { "Workspace slug chỉ được chứa chữ thường, số và dấu gạch ngang." };

        if (validationErrors.Count > 0)
        {
            return ServiceResult<CompanyDto>.Validation(validationErrors);
        }

        var slug = request.WorkspaceSlug.Trim().ToLowerInvariant();
        if (await _companyRepository.CodeExistsAsync(slug, cancellationToken))
        {
            return ServiceResult<CompanyDto>.Conflict("WORKSPACE_SLUG_TAKEN", "Mã công ty (slug) đã được sử dụng.");
        }

        var dto = await _companyRepository.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            var company = new Company
            {
                Name = request.Name.Trim(),
                Code = slug,
                EmailDomain = string.IsNullOrWhiteSpace(request.EmailDomain) ? null : request.EmailDomain.Trim().ToLowerInvariant(),
                DomainAutoJoinEnabled = request.DomainAutoJoinEnabled,
                TaxCode = request.TaxCode,
                Address = request.Address,
                Industry = "Software",
                CompanySize = "10-50",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _companyRepository.AddCompanyAsync(company, ct);
            await _companyRepository.SaveChangesAsync(ct);

            await _companyRepository.SeedSystemRolesAndPermissionsAsync(company.Id, now, ct);

            var adminRole = await _companyRepository.GetRoleByCodeAsync(company.Id, CompanyRoleCodes.TenantAdmin, ct);

            await _companyRepository.AddMemberAsync(new CompanyMember
            {
                CompanyId = company.Id,
                UserId = creatorUserId.Value,
                RoleId = adminRole!.Id,
                IsActive = true,
                JoinedAt = now
            }, ct);
            await _companyRepository.SaveChangesAsync(ct);

            return MapToDto(company);
        }, cancellationToken);

        return ServiceResult<CompanyDto>.Created(dto);
    }

    public async Task<ServiceResult<CompanyJoinResponseDto>> JoinCompanyAsync(long companyId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<CompanyJoinResponseDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null || !company.IsActive)
        {
            return ServiceResult<CompanyJoinResponseDto>.NotFound("Không tìm thấy công ty.");
        }

        var user = await _authRepository.GetUserByIdAsync(userId.Value, cancellationToken);
        if (user == null)
        {
            return ServiceResult<CompanyJoinResponseDto>.NotFound("Không tìm thấy người dùng.");
        }

        var userDomain = user.Email.Contains('@') ? user.Email.Split('@')[1].ToLowerInvariant() : string.Empty;
        var companyDomain = company.EmailDomain?.ToLowerInvariant();

        if (!company.DomainAutoJoinEnabled || string.IsNullOrEmpty(companyDomain) || userDomain != companyDomain)
        {
            return ServiceResult<CompanyJoinResponseDto>.Forbidden("AUTO_JOIN_NOT_ALLOWED", "Công ty chưa bật tự động tham gia hoặc domain email không khớp.");
        }

        var existingMember = await _companyRepository.GetMemberAsync(companyId, userId.Value, cancellationToken);
        if (existingMember != null)
        {
            return ServiceResult<CompanyJoinResponseDto>.Ok(new CompanyJoinResponseDto { CompanyId = companyId, Status = "joined" });
        }

        var memberRoleId = await _companyRepository.ResolveRoleIdAsync(companyId, CompanyRoleCodes.Member, RoleScopes.Tenant, CompanyRoleCodes.Member, cancellationToken);

        await _companyRepository.AddMemberAsync(new CompanyMember
        {
            CompanyId = companyId,
            UserId = userId.Value,
            RoleId = memberRoleId,
            IsActive = true,
            JoinedAt = _clock.GetUtcNow().UtcDateTime
        }, cancellationToken);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult<CompanyJoinResponseDto>.Ok(new CompanyJoinResponseDto { CompanyId = companyId, Status = "joined" });
    }

    public async Task<ServiceResult<JoinRequestResponseDto>> CreateJoinRequestAsync(long companyId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<JoinRequestResponseDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null || !company.IsActive)
        {
            return ServiceResult<JoinRequestResponseDto>.NotFound("Không tìm thấy công ty.");
        }

        var user = await _authRepository.GetUserByIdAsync(userId.Value, cancellationToken);
        if (user == null)
        {
            return ServiceResult<JoinRequestResponseDto>.NotFound("Không tìm thấy người dùng.");
        }

        var request = new MembershipRequest
        {
            CompanyId = companyId,
            UserId = userId.Value,
            Email = user.Email,
            Source = "join_request",
            Status = "pending",
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        await _companyRepository.AddMembershipRequestAsync(request, cancellationToken);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult<JoinRequestResponseDto>.Created(new JoinRequestResponseDto { RequestId = request.Id, Status = "pending" });
    }

    public async Task<ServiceResult<JoinRequestStatusResponseDto>> GetJoinRequestStatusAsync(long companyId, long requestId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<JoinRequestStatusResponseDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var request = await _companyRepository.GetJoinRequestAsync(companyId, requestId, cancellationToken);
        if (request == null)
        {
            return ServiceResult<JoinRequestStatusResponseDto>.NotFound("Không tìm thấy yêu cầu gia nhập.");
        }

        if (request.UserId != userId.Value && !await _companyRepository.IsAdminAsync(companyId, userId.Value, cancellationToken))
        {
            return ServiceResult<JoinRequestStatusResponseDto>.Forbidden("FORBIDDEN", "Bạn không có quyền xem yêu cầu gia nhập này.");
        }

        return ServiceResult<JoinRequestStatusResponseDto>.Ok(new JoinRequestStatusResponseDto { Status = request.Status });
    }

    public async Task<ServiceResult<PageDto<CompanyUserDto>>> ListUsersAsync(
        long companyId,
        string? search,
        long? roleId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<PageDto<CompanyUserDto>>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.UserView, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<PageDto<CompanyUserDto>>.FromError(auth);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

        var pageDto = await _companyRepository.ListCompanyUsersAsync(companyId, search, roleId, status, safePage, safePageSize, cancellationToken);
        return ServiceResult<PageDto<CompanyUserDto>>.Ok(pageDto);
    }

    public async Task<ServiceResult<CompanyUserDto>> UpdateUserRoleAsync(
        long companyId,
        long targetUserId,
        UpdateCompanyUserRoleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<CompanyUserDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.UserAssignRole, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<CompanyUserDto>.FromError(auth);

        var member = await _companyRepository.GetMemberAsync(companyId, targetUserId, cancellationToken);
        if (member == null)
            return ServiceResult<CompanyUserDto>.NotFound("Không tìm thấy dữ liệu.");

        var targetRole = await _companyRepository.GetRoleByIdAsync(companyId, request.RoleId, cancellationToken);
        if (targetRole == null)
            return ServiceResult<CompanyUserDto>.NotFound("Không tìm thấy dữ liệu.");

        if (targetRole.Scope != RoleScopes.Tenant)
            return ServiceResult<CompanyUserDto>.BadRequest("ROLE_SCOPE_MISMATCH", $"Role này thuộc phạm vi {targetRole.Scope}, không gán ở đây được.");

        if (!targetRole.IsActive)
            return ServiceResult<CompanyUserDto>.BadRequest("ROLE_INACTIVE", "Role đang tắt, không gán được.");

        if (member.Role.Code == CompanyRoleCodes.TenantAdmin && targetRole.Code != CompanyRoleCodes.TenantAdmin)
        {
            var adminCount = await _companyRepository.CountActiveTenantAdminsAsync(companyId, cancellationToken);
            if (adminCount <= 1)
                return ServiceResult<CompanyUserDto>.Conflict("LAST_TENANT_ADMIN", "Công ty phải còn ít nhất 1 Tenant Admin.");
        }

        member.RoleId = targetRole.Id;
        await _companyRepository.SaveChangesAsync(cancellationToken);

        var dto = await _companyRepository.GetCompanyUserDtoAsync(companyId, targetUserId, cancellationToken);
        return ServiceResult<CompanyUserDto>.Ok(dto!);
    }

    private static CompanyDto MapToDto(Company c)
    {
        return new CompanyDto
        {
            Id = c.Id,
            Name = c.Name,
            Code = c.Code,
            EmailDomain = c.EmailDomain,
            DomainAutoJoinEnabled = c.DomainAutoJoinEnabled,
            TaxCode = c.TaxCode,
            Address = c.Address,
            LogoUrl = c.LogoUrl
        };
    }
}
