using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SSAM.Application.Common.Exceptions;
using SSAM.Application.Common.Interfaces.Repositories;
using SSAM.Application.Common.Interfaces.Security;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Companies;
using SSAM.Application.DTOs.Invitations;
using SSAM.Domain.Entities.Companies;
using SSAM.Domain.Entities.Identity;
using SSAM.Domain.Entities.Workspaces;

namespace SSAM.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    private static readonly HashSet<string> PublicEmailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", "icloud.com", "aol.com", "mail.com", "zoho.com"
    };

    public CompanyService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<CompanyMatchResponseDto> MatchDomainAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return new CompanyMatchResponseDto { Matched = false };
        }

        var domain = email.Split('@')[1].Trim().ToLowerInvariant();

        // 1. Exclude public free email providers (per contract 4.1)
        if (PublicEmailDomains.Contains(domain))
        {
            return new CompanyMatchResponseDto { Matched = false };
        }

        // 2. Query company by email domain
        var company = await _unitOfWork.Companies.FirstOrDefaultAsync(
            c => c.EmailDomain == domain && c.IsActive, 
            cancellationToken);

        if (company == null)
        {
            return new CompanyMatchResponseDto { Matched = false };
        }

        return new CompanyMatchResponseDto
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
        };
    }

    public async Task<CompanyDto> CreateCompanyAsync(CreateCompanyRequestDto request, long creatorUserId, CancellationToken cancellationToken = default)
    {
        var validationErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
            validationErrors["name"] = new[] { "Tên công ty là bắt buộc." };
        if (string.IsNullOrWhiteSpace(request.WorkspaceSlug))
            validationErrors["workspaceSlug"] = new[] { "Workspace slug là bắt buộc." };
        else if (!Regex.IsMatch(request.WorkspaceSlug, "^[a-z0-9-]+$"))
            validationErrors["workspaceSlug"] = new[] { "Workspace slug chỉ được chứa chữ thường, số và dấu gạch ngang." };

        if (validationErrors.Count > 0)
        {
            throw AppException.Validation(validationErrors);
        }

        var slugExists = await _unitOfWork.Companies.ExistsAsync(c => c.Code == request.WorkspaceSlug, cancellationToken);
        if (slugExists)
        {
            throw AppException.Conflict("WORKSPACE_SLUG_TAKEN", "Mã công ty (slug) đã được sử dụng.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Create Company
            var company = new Company
            {
                Name = request.Name,
                Code = request.WorkspaceSlug.ToLowerInvariant(),
                EmailDomain = request.EmailDomain,
                DomainAutoJoinEnabled = request.DomainAutoJoinEnabled,
                TaxCode = request.TaxCode,
                Address = request.Address,
                Industry = "Software",
                CompanySize = "10-50",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Companies.AddAsync(company, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 2. Seed System Roles for this Company
            var adminRole = new CompanyRole
            {
                CompanyId = company.Id,
                Name = "Tenant Admin",
                Code = "TENANT_ADMIN",
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            };
            var memberRole = new CompanyRole
            {
                CompanyId = company.Id,
                Name = "Member",
                Code = "MEMBER",
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.CompanyRoles.AddRangeAsync(new[] { adminRole, memberRole }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 3. Assign Creator as Tenant Admin
            var member = new CompanyMember
            {
                CompanyId = company.Id,
                UserId = creatorUserId,
                RoleId = adminRole.Id,
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };
            await _unitOfWork.CompanyMembers.AddAsync(member, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return MapToDto(company);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CompanyJoinResponseDto> JoinCompanyAsync(long companyId, long userId, CancellationToken cancellationToken = default)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(companyId, cancellationToken);
        if (company == null || !company.IsActive)
        {
            throw AppException.NotFound("Không tìm thấy công ty.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound("Không tìm thấy người dùng.");
        }

        // Server-side validation: domainAutoJoinEnabled must be true AND user domain must match
        var userDomain = user.Email.Contains('@') ? user.Email.Split('@')[1].ToLowerInvariant() : string.Empty;
        var companyDomain = company.EmailDomain?.ToLowerInvariant();

        if (!company.DomainAutoJoinEnabled || string.IsNullOrEmpty(companyDomain) || userDomain != companyDomain)
        {
            throw AppException.Forbidden("AUTO_JOIN_NOT_ALLOWED", "Công ty chưa bật tự động tham gia hoặc domain email không khớp.");
        }

        var existingMember = await _unitOfWork.CompanyMembers.FirstOrDefaultAsync(
            cm => cm.CompanyId == companyId && cm.UserId == userId, 
            cancellationToken);

        if (existingMember != null)
        {
            return new CompanyJoinResponseDto { CompanyId = companyId, Status = "joined" };
        }

        var memberRole = await _unitOfWork.CompanyRoles.FirstOrDefaultAsync(
            r => r.CompanyId == companyId && r.Code == "MEMBER", 
            cancellationToken);

        var member = new CompanyMember
        {
            CompanyId = companyId,
            UserId = userId,
            RoleId = memberRole?.Id ?? 0,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };
        await _unitOfWork.CompanyMembers.AddAsync(member, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CompanyJoinResponseDto { CompanyId = companyId, Status = "joined" };
    }

    public async Task<JoinRequestResponseDto> CreateJoinRequestAsync(long companyId, long userId, CancellationToken cancellationToken = default)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
        {
            throw AppException.NotFound("Không tìm thấy công ty.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound("Không tìm thấy người dùng.");
        }

        var request = new MembershipRequest
        {
            CompanyId = companyId,
            UserId = userId,
            Email = user.Email,
            Source = "join_request",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.MembershipRequests.AddAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new JoinRequestResponseDto { RequestId = request.Id, Status = "pending" };
    }

    public async Task<JoinRequestStatusResponseDto> GetJoinRequestStatusAsync(long companyId, long requestId, CancellationToken cancellationToken = default)
    {
        var request = await _unitOfWork.MembershipRequests.FirstOrDefaultAsync(
            r => r.Id == requestId && r.CompanyId == companyId, 
            cancellationToken);

        if (request == null)
        {
            throw AppException.NotFound("Không tìm thấy yêu cầu gia nhập.");
        }

        return new JoinRequestStatusResponseDto { Status = request.Status };
    }

    public async Task<CreateInvitationResponseDto> CreateInvitationAsync(long companyId, CreateInvitationRequestDto request, CancellationToken cancellationToken = default)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
        {
            throw AppException.NotFound("Không tìm thấy công ty.");
        }

        var token = GenerateSecureToken();
        var invite = new MembershipRequest
        {
            CompanyId = companyId,
            Email = request.Email,
            WorkspaceId = request.WorkspaceId,
            RoleName = request.RoleName ?? "Member",
            Source = "admin_invite",
            Status = "pending",
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.MembershipRequests.AddAsync(invite, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateInvitationResponseDto
        {
            InvitationId = invite.Id,
            Token = token,
            InviteUrl = $"https://app.ssam.cloud/invite/{token}"
        };
    }

    public async Task<InvitationDetailsResponseDto> GetInvitationDetailsAsync(string token, CancellationToken cancellationToken = default)
    {
        var invite = await _unitOfWork.MembershipRequests.Query()
            .Include(m => m.Company)
            .Include(m => m.Workspace)
            .FirstOrDefaultAsync(m => m.Token == token && m.Source == "admin_invite", cancellationToken);

        if (invite == null)
        {
            return new InvitationDetailsResponseDto { Valid = false, Reason = "NOT_FOUND" };
        }

        if (invite.Status == "accepted")
        {
            return new InvitationDetailsResponseDto { Valid = false, Reason = "ALREADY_USED" };
        }

        if (invite.ExpiresAt != null && invite.ExpiresAt <= DateTime.UtcNow)
        {
            return new InvitationDetailsResponseDto { Valid = false, Reason = "EXPIRED" };
        }

        return new InvitationDetailsResponseDto
        {
            Valid = true,
            Email = invite.Email,
            CompanyName = invite.Company.Name,
            WorkspaceName = invite.Workspace?.Name,
            RoleName = invite.RoleName,
            ExpiresAt = invite.ExpiresAt
        };
    }

    public async Task<AcceptInvitationResponseDto> AcceptInvitationAsync(
        string token, 
        AcceptInvitationRequestDto? request, 
        long? currentUserId, 
        CancellationToken cancellationToken = default)
    {
        var invite = await _unitOfWork.MembershipRequests.FirstOrDefaultAsync(
            m => m.Token == token && m.Source == "admin_invite", 
            cancellationToken);

        if (invite == null)
        {
            throw AppException.BadRequest("TOKEN_INVALID", "Mã lời mời không hợp lệ.");
        }

        if (invite.Status == "accepted")
        {
            throw AppException.BadRequest("TOKEN_INVALID", "Lời mời đã được sử dụng.");
        }

        if (invite.ExpiresAt != null && invite.ExpiresAt <= DateTime.UtcNow)
        {
            throw AppException.BadRequest("TOKEN_EXPIRED", "Lời mời đã hết hạn.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            long userId;
            string? accessToken = null;
            string? refreshToken = null;
            int? expiresIn = null;

            // Case A: User is not logged in / does not have account
            if (currentUserId == null)
            {
                if (string.IsNullOrWhiteSpace(request?.FullName) || string.IsNullOrWhiteSpace(request?.Password))
                {
                    throw AppException.Validation(new Dictionary<string, string[]>
                    {
                        { "fullName", new[] { "Họ và tên là bắt buộc." } },
                        { "password", new[] { "Mật khẩu là bắt buộc." } }
                    });
                }

                // Check if account already exists with this email
                var existingAccount = await _unitOfWork.Accounts.FirstOrDefaultAsync(a => a.Email == invite.Email, cancellationToken);
                if (existingAccount != null)
                {
                    throw AppException.Conflict("CONFLICT", "Email này đã có tài khoản, vui lòng đăng nhập trước khi chấp nhận lời mời.");
                }

                var newAccount = new Account
                {
                    Email = invite.Email,
                    Password = _passwordHasher.HashPassword(request.Password),
                    EmailVerified = true, // Trusted from invite
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Accounts.AddAsync(newAccount, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var newUser = new User
                {
                    AccountId = newAccount.Id,
                    Email = invite.Email,
                    FullName = request.FullName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Users.AddAsync(newUser, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                userId = newUser.Id;

                // Generate Auth Tokens
                var tokenPair = _jwtTokenGenerator.GenerateAccessToken(newAccount, newUser);
                accessToken = tokenPair.Token;
                expiresIn = tokenPair.ExpiresIn;
                refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

                var refreshTokenEntity = new RefreshToken
                {
                    AccountId = newAccount.Id,
                    TokenHash = _jwtTokenGenerator.HashRefreshToken(refreshToken),
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(7)
                };
                await _unitOfWork.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // Case B: User already logged in
                var loggedInUser = await _unitOfWork.Users.GetByIdAsync(currentUserId.Value, cancellationToken);
                if (loggedInUser == null)
                {
                    throw AppException.NotFound("Không tìm thấy người dùng.");
                }

                if (!string.Equals(loggedInUser.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
                {
                    throw AppException.Conflict("CONFLICT", "Email của tài khoản đang đăng nhập không khớp với email trong lời mời.");
                }

                userId = loggedInUser.Id;
            }

            // Add user to CompanyMembers
            var role = await _unitOfWork.CompanyRoles.FirstOrDefaultAsync(
                r => r.CompanyId == invite.CompanyId && (r.Name == invite.RoleName || r.Code == "MEMBER"), 
                cancellationToken);

            var companyMemberExists = await _unitOfWork.CompanyMembers.ExistsAsync(
                cm => cm.CompanyId == invite.CompanyId && cm.UserId == userId, 
                cancellationToken);

            if (!companyMemberExists)
            {
                var companyMember = new CompanyMember
                {
                    CompanyId = invite.CompanyId,
                    UserId = userId,
                    RoleId = role?.Id ?? 0,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };
                await _unitOfWork.CompanyMembers.AddAsync(companyMember, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Add to Workspace if specified
            if (invite.WorkspaceId.HasValue)
            {
                var workspaceMemberExists = await _unitOfWork.WorkspaceMembers.ExistsAsync(
                    wm => wm.WorkspaceId == invite.WorkspaceId.Value && wm.UserId == userId, 
                    cancellationToken);

                if (!workspaceMemberExists)
                {
                    var workspaceMember = new WorkspaceMember
                    {
                        WorkspaceId = invite.WorkspaceId.Value,
                        UserId = userId,
                        JoinedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.WorkspaceMembers.AddAsync(workspaceMember, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }

            // Mark invite accepted
            invite.Status = "accepted";
            _unitOfWork.MembershipRequests.Update(invite);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new AcceptInvitationResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
                CompanyId = invite.CompanyId,
                WorkspaceId = invite.WorkspaceId
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
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

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLower();
    }
}
