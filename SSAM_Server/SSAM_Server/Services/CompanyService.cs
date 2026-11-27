using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Interfaces.Repositories;
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

    private static readonly Regex EmailDomainRegex = new(
        @"^[a-z0-9-]+(\.[a-z0-9-]+)+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ICompanyRepository _companyRepository;
    private readonly IAuthRepository _authRepository;
    private readonly IPermissionService _permissionService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDepartmentRepository _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public CompanyService(
        ICompanyRepository companyRepository,
        IAuthRepository authRepository,
        IPermissionService permissionService,
        IPasswordHasher passwordHasher,
        IDepartmentRepository db,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _companyRepository = companyRepository;
        _authRepository = authRepository;
        _permissionService = permissionService;
        _passwordHasher = passwordHasher;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ServiceResult<CompanyDto>> GetCompanyAsync(long companyId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<CompanyDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var isMember = await _companyRepository.IsMemberAsync(companyId, userId.Value, cancellationToken);
        if (!isMember)
        {
            return ServiceResult<CompanyDto>.Forbidden("FORBIDDEN", "Bạn không phải thành viên của công ty này.");
        }

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null || !company.IsActive)
        {
            return ServiceResult<CompanyDto>.NotFound("Không tìm thấy công ty.");
        }

        return ServiceResult<CompanyDto>.Ok(MapToDto(company));
    }

    public async Task<ServiceResult<CompanyDto>> UpdateCompanyAsync(long companyId, UpdateCompanyRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<CompanyDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.CompanyUpdate, cancellationToken);
        if (!auth.IsSuccess)
        {
            return ServiceResult<CompanyDto>.FromError(auth);
        }

        var validationErrors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            validationErrors["name"] = new[] { "Tên công ty là bắt buộc." };
        }
        else if (name.Length > 200)
        {
            validationErrors["name"] = new[] { "Tên công ty không được vượt quá 200 ký tự." };
        }

        var taxCode = request.TaxCode?.Trim() ?? string.Empty;
        if (taxCode.Length > 100)
        {
            validationErrors["taxCode"] = new[] { "Mã số thuế không được vượt quá 100 ký tự." };
        }

        var address = request.Address?.Trim() ?? string.Empty;

        string? normalizedDomain = null;
        if (!string.IsNullOrWhiteSpace(request.EmailDomain))
        {
            normalizedDomain = request.EmailDomain.Trim().ToLowerInvariant();
            if (normalizedDomain.Length > 255)
            {
                validationErrors["emailDomain"] = new[] { "Tên miền email không được vượt quá 255 ký tự." };
            }
            else if (!EmailDomainRegex.IsMatch(normalizedDomain))
            {
                validationErrors["emailDomain"] = new[] { "Tên miền email không đúng định dạng." };
            }
            else if (PublicEmailDomains.Contains(normalizedDomain))
            {
                validationErrors["emailDomain"] = new[] { "Không thể sử dụng tên miền email công cộng (gmail.com, outlook.com, ...)." };
            }
        }

        if (validationErrors.Count > 0)
        {
            return ServiceResult<CompanyDto>.Validation(validationErrors);
        }

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null || !company.IsActive)
        {
            return ServiceResult<CompanyDto>.NotFound("Không tìm thấy công ty.");
        }

        company.Name = name;
        company.TaxCode = taxCode;
        company.Address = address;
        company.EmailDomain = normalizedDomain;
        if (request.BlockForeignEmailDomains.HasValue)
            company.BlockForeignEmailDomains = request.BlockForeignEmailDomains.Value;
        company.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

        await _companyRepository.SaveChangesAsync(cancellationToken);
        return ServiceResult<CompanyDto>.Ok(MapToDto(company));
    }

    public async Task<ServiceResult<CompanyMatchResponseDto>> MatchDomainAsync(string email, CancellationToken cancellationToken = default)
    {
        // Không cho phép tự xin vào công ty; chỉ vào được qua lời mời từ Admin (/invite/{token}).
        // Trả về companyExists để FE biết hệ thống đã có công ty (chặn cả việc tạo công ty thứ 2).
        var companyExists = await _companyRepository.AnyActiveCompanyExistsAsync(cancellationToken);
        return ServiceResult<CompanyMatchResponseDto>.Ok(new CompanyMatchResponseDto
        {
            Matched = false,
            CompanyExists = companyExists
        });
    }

    public async Task<ServiceResult<CompanyDto>> CreateCompanyAsync(CreateCompanyRequestDto request, CancellationToken cancellationToken = default)
    {
        var creatorUserId = _currentUser.UserIdOrNull;
        if (creatorUserId == null)
        {
            return ServiceResult<CompanyDto>.Unauthorized("Your session is invalid or has expired.");
        }

        if (await _companyRepository.AnyActiveCompanyExistsAsync(cancellationToken))
        {
            return ServiceResult<CompanyDto>.Conflict("COMPANY_ALREADY_EXISTS", "Hệ thống đã có công ty, không thể tạo thêm công ty mới.");
        }

        if (await _companyRepository.HasAnyActiveCompanyAsync(creatorUserId.Value, null, cancellationToken))
        {
            return ServiceResult<CompanyDto>.Conflict("ALREADY_IN_COMPANY", "Tài khoản này đã thuộc một công ty, không thể tạo thêm công ty khác.");
        }

        var validationErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
            validationErrors["name"] = new[] { "Tên công ty là bắt buộc." };
        if (string.IsNullOrWhiteSpace(request.WorkspaceSlug))
            validationErrors["workspaceSlug"] = new[] { "Workspace slug là bắt buộc." };
        else if (!Regex.IsMatch(request.WorkspaceSlug, "^[a-z0-9-]+$"))
            validationErrors["workspaceSlug"] = new[] { "Workspace slug chỉ được chứa chữ thường, số và dấu gạch ngang." };

        var normalizedDomain = string.IsNullOrWhiteSpace(request.EmailDomain)
            ? null
            : request.EmailDomain.Trim().ToLowerInvariant();
        if (normalizedDomain != null && normalizedDomain.Length > 255)
            validationErrors["emailDomain"] = new[] { "Tên miền email không được vượt quá 255 ký tự." };
        else if (normalizedDomain != null && PublicEmailDomains.Contains(normalizedDomain))
            validationErrors["emailDomain"] = new[] { "Không dùng tên miền email công cộng (gmail.com, yahoo.com…)." };

        if (!string.IsNullOrWhiteSpace(request.TaxCode) && request.TaxCode.Trim().Length > 100)
            validationErrors["taxCode"] = new[] { "Mã số thuế không được vượt quá 100 ký tự." };

        string? cleanPhone = null;
        if (!string.IsNullOrWhiteSpace(request.AdminPhone))
        {
            cleanPhone = Regex.Replace(request.AdminPhone.Trim(), @"[\s\-]", "");
            if (!Regex.IsMatch(cleanPhone, @"^(\+84|0)\d{9,10}$"))
                validationErrors["adminPhone"] = new[] { "Số điện thoại không hợp lệ." };
        }

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
                EmailDomain = normalizedDomain,
                DomainAutoJoinEnabled = request.DomainAutoJoinEnabled,
                BlockForeignEmailDomains = request.BlockForeignEmailDomains ?? true,
                TaxCode = request.TaxCode?.Trim() ?? string.Empty,
                Address = request.Address?.Trim() ?? string.Empty,
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

            if (!string.IsNullOrEmpty(cleanPhone))
            {
                var creatorUser = await _authRepository.GetUserByIdAsync(creatorUserId.Value, ct);
                if (creatorUser != null && string.IsNullOrWhiteSpace(creatorUser.Phone))
                {
                    creatorUser.Phone = cleanPhone;
                    creatorUser.UpdatedAt = now;
                }
            }

            await _companyRepository.SaveChangesAsync(ct);

            return MapToDto(company);
        }, cancellationToken);

        return ServiceResult<CompanyDto>.Created(dto);
    }

    public Task<ServiceResult<CompanyJoinResponseDto>> JoinCompanyAsync(long companyId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserIdOrNull == null)
        {
            return Task.FromResult(ServiceResult<CompanyJoinResponseDto>.Unauthorized("Your session is invalid or has expired."));
        }

        return Task.FromResult(ServiceResult<CompanyJoinResponseDto>.Forbidden(
            "JOIN_BY_INVITE_ONLY",
            "Chỉ quản trị viên mới có thể mời thành viên vào công ty."));
    }

    public Task<ServiceResult<JoinRequestResponseDto>> CreateJoinRequestAsync(long companyId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserIdOrNull == null)
        {
            return Task.FromResult(ServiceResult<JoinRequestResponseDto>.Unauthorized("Your session is invalid or has expired."));
        }

        return Task.FromResult(ServiceResult<JoinRequestResponseDto>.Forbidden(
            "JOIN_BY_INVITE_ONLY",
            "Chỉ quản trị viên mới có thể mời thành viên vào công ty."));
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

    public async Task<ServiceResult<CompanyUserDto>> UpdateUserStatusAsync(
        long companyId,
        long targetUserId,
        UpdateCompanyUserStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<CompanyUserDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.UserDeactivate, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<CompanyUserDto>.FromError(auth);

        var normalized = (request.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized is not ("ACTIVE" or "DEACTIVATED"))
            return ServiceResult<CompanyUserDto>.BadRequest("INVALID_STATUS", "Trạng thái phải là ACTIVE hoặc DEACTIVATED.");

        var targetActive = normalized == "ACTIVE";
        if (targetUserId == userId.Value && !targetActive)
            return ServiceResult<CompanyUserDto>.Conflict("CANNOT_DEACTIVATE_SELF", "Không thể tự vô hiệu hoá tài khoản của chính mình.");

        var member = await _companyRepository.GetMemberAsync(companyId, targetUserId, cancellationToken);
        if (member == null)
            return ServiceResult<CompanyUserDto>.NotFound("Không tìm thấy thành viên trong công ty.");

        if (!targetActive && member.Role.Code == CompanyRoleCodes.TenantAdmin)
        {
            var adminCount = await _companyRepository.CountActiveTenantAdminsAsync(companyId, cancellationToken);
            if (adminCount <= 1)
                return ServiceResult<CompanyUserDto>.Conflict("LAST_TENANT_ADMIN", "Công ty phải còn ít nhất 1 Tenant Admin đang hoạt động.");
        }

        if (member.IsActive != targetActive)
        {
            member.IsActive = targetActive;
            await _companyRepository.SaveChangesAsync(cancellationToken);
        }

        var dto = await _companyRepository.GetCompanyUserDtoAsync(companyId, targetUserId, cancellationToken);
        return ServiceResult<CompanyUserDto>.Ok(dto!);
    }

    public async Task<ServiceResult<CompanyUserDto>> CreateUserAsync(
        long companyId,
        CreateCompanyUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<CompanyUserDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.UserInvite, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<CompanyUserDto>.FromError(auth);

        var errors = new Dictionary<string, string[]>();
        var fullName = (request.FullName ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(fullName))
            errors["fullName"] = new[] { "Họ và tên không được để trống." };
        else if (fullName.Length > 255)
            errors["fullName"] = new[] { "Họ và tên không được vượt quá 255 ký tự." };

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            errors["email"] = new[] { "Email không hợp lệ." };
        else if (email.Length > 255)
            errors["email"] = new[] { "Email không được vượt quá 255 ký tự." };
        AuthService.ValidatePasswordRules(request.Password ?? string.Empty, errors);
        var cleanPhone = ValidateOptionalPhone(request.Phone, errors);
        if (errors.Count > 0)
            return ServiceResult<CompanyUserDto>.ValidationFailure(errors);

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null || !company.IsActive)
            return ServiceResult<CompanyUserDto>.NotFound("Không tìm thấy công ty.");

        if (!company.IsEmailAllowed(email))
        {
            return ServiceResult<CompanyUserDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["email"] = new[] { $"Email nhân viên phải thuộc tên miền công ty (@{company.EmailDomain})." }
            });
        }

        if (await _companyRepository.IsEmailMemberAsync(companyId, email, cancellationToken))
            return ServiceResult<CompanyUserDto>.Conflict("ALREADY_MEMBER", "Email này đã thuộc công ty.");

        long roleId;
        if (request.RoleId.HasValue)
        {
            var role = await _companyRepository.GetRoleByIdAsync(companyId, request.RoleId.Value, cancellationToken);
            if (role == null || role.Scope != RoleScopes.Tenant || !role.IsActive)
                return ServiceResult<CompanyUserDto>.BadRequest("INVALID_ROLE", "Vai trò công ty không hợp lệ.");
            roleId = role.Id;
        }
        else
        {
            roleId = await _companyRepository.ResolveRoleIdAsync(companyId, null, RoleScopes.Tenant, CompanyRoleCodes.Member, cancellationToken);
        }

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _db.Departments.AsNoTracking()
                .AnyAsync(d => d.Id == request.DepartmentId.Value && d.CompanyId == companyId && d.IsActive, cancellationToken);
            if (!deptExists)
                return ServiceResult<CompanyUserDto>.BadRequest("INVALID_DEPARTMENT", "Phòng ban không thuộc công ty này.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var existingAccount = await _authRepository.GetAccountByEmailAsync(email, cancellationToken);
        if (existingAccount != null)
            return ServiceResult<CompanyUserDto>.Conflict("EMAIL_EXISTS", "Email này đã có tài khoản trong hệ thống.");

        var createdUser = await _companyRepository.ExecuteInTransactionAsync(async ct =>
        {
            var account = new Account
            {
                Email = email,
                Password = _passwordHasher.HashPassword(request.Password ?? string.Empty),
                EmailVerified = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _authRepository.AddAccountAsync(account, ct);
            await _authRepository.SaveChangesAsync(ct);

            var user = new User
            {
                AccountId = account.Id,
                Email = email,
                FullName = fullName,
                Phone = cleanPhone,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _authRepository.AddUserAsync(user, ct);
            await _authRepository.SaveChangesAsync(ct);

            var member = new CompanyMember
            {
                CompanyId = companyId,
                UserId = user.Id,
                RoleId = roleId,
                DepartmentId = request.DepartmentId,
                IsActive = true,
                JoinedAt = now
            };
            await _companyRepository.AddMemberAsync(member, ct);
            await _companyRepository.SaveChangesAsync(ct);
            return user;
        }, cancellationToken);

        var dto = await _companyRepository.GetCompanyUserDtoAsync(companyId, createdUser.Id, cancellationToken);
        return ServiceResult<CompanyUserDto>.Created(dto!);
    }

    public async Task<ServiceResult<CompanyUserDto>> UpdateUserAsync(
        long companyId,
        long targetUserId,
        UpdateCompanyUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<CompanyUserDto>.Unauthorized("Your session is invalid or has expired.");

        var auth = await _permissionService.RequireTenantAsync(userId.Value, companyId, PermissionKeys.UserUpdate, cancellationToken);
        if (!auth.IsSuccess)
            return ServiceResult<CompanyUserDto>.FromError(auth);

        var errors = new Dictionary<string, string[]>();
        var fullName = (request.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            errors["fullName"] = new[] { "Họ và tên không được để trống." };
        else if (fullName.Length > 255)
            errors["fullName"] = new[] { "Họ và tên không được vượt quá 255 ký tự." };
        var cleanPhone = ValidateOptionalPhone(request.Phone, errors);
        if (errors.Count > 0)
            return ServiceResult<CompanyUserDto>.ValidationFailure(errors);

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _db.Departments.AsNoTracking()
                .AnyAsync(d => d.Id == request.DepartmentId.Value && d.CompanyId == companyId && d.IsActive, cancellationToken);
            if (!deptExists)
                return ServiceResult<CompanyUserDto>.BadRequest("INVALID_DEPARTMENT", "Phòng ban không thuộc công ty này.");
        }

        var member = await _companyRepository.GetMemberAsync(companyId, targetUserId, cancellationToken);
        if (member == null)
            return ServiceResult<CompanyUserDto>.NotFound("Không tìm thấy thành viên trong công ty.");

        member.User.FullName = fullName;
        member.User.Phone = cleanPhone;
        member.User.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        member.DepartmentId = request.DepartmentId;

        await _companyRepository.SaveChangesAsync(cancellationToken);

        var dto = await _companyRepository.GetCompanyUserDtoAsync(companyId, targetUserId, cancellationToken);
        return ServiceResult<CompanyUserDto>.Ok(dto!);
    }

    private static string? ValidateOptionalPhone(string? rawPhone, Dictionary<string, string[]> errors, string fieldName = "phone")
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            return null;

        var trimmed = rawPhone.Trim();
        var cleanPhone = Regex.Replace(trimmed, @"[\s\-]", "");
        if (trimmed.Length > 50 || cleanPhone.Length > 50)
        {
            errors[fieldName] = new[] { "Số điện thoại không được vượt quá 50 ký tự." };
            return null;
        }
        if (!Regex.IsMatch(cleanPhone, @"^(\+84|0)\d{9,10}$"))
        {
            errors[fieldName] = new[] { "Số điện thoại không hợp lệ (ví dụ: 0901234567 hoặc +84901234567)." };
            return null;
        }
        return cleanPhone;
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
            BlockForeignEmailDomains = c.BlockForeignEmailDomains,
            TaxCode = c.TaxCode,
            Address = c.Address,
            LogoUrl = c.LogoUrl
        };
    }
}
