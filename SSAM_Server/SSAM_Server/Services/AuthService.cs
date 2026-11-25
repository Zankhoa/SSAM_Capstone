using System.Security.Cryptography;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Auth;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Models;
using SSAM_Server.Repositories;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailService _emailService;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public AuthService(
        IAuthRepository authRepository,
        ICompanyRepository companyRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailService emailService,
        IGoogleTokenValidator googleTokenValidator,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _authRepository = authRepository;
        _companyRepository = companyRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailService = emailService;
        _googleTokenValidator = googleTokenValidator;
        _currentUser = currentUser;
        _clock = clock;
    }

    private async Task<bool> IsAllowedCompanyEmailAsync(string email, long? existingUserId, CancellationToken ct)
    {
        var atIdx = email.IndexOf('@');
        if (atIdx < 0 || atIdx == email.Length - 1) return false;
        var domain = email[(atIdx + 1)..].Trim().ToLowerInvariant();

        if (!await _companyRepository.AnyActiveCompanyExistsAsync(ct))
            return true;

        if (await _companyRepository.GetActiveByDomainAsync(domain, ct) != null)
            return true;

        if (existingUserId.HasValue && await _companyRepository.HasAnyActiveCompanyAsync(existingUserId.Value, null, ct))
            return true;

        return false;
    }

    public async Task<ServiceResult<RegisterResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FullName))
            validationErrors["fullName"] = new[] { "Họ và tên không được để trống." };
        else if (request.FullName.Trim().Length > 255)
            validationErrors["fullName"] = new[] { "Họ và tên không được vượt quá 255 ký tự." };

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            validationErrors["email"] = new[] { "Email không đúng định dạng." };
        else if (request.Email.Trim().Length > 255)
            validationErrors["email"] = new[] { "Email không được vượt quá 255 ký tự." };

        ValidatePasswordRules(request.Password, validationErrors);

        if (validationErrors.Count > 0)
        {
            return ServiceResult<RegisterResponseDto>.Validation(validationErrors);
        }

        var email = request.Email.Trim();
        if (!await IsAllowedCompanyEmailAsync(email, null, cancellationToken))
        {
            return ServiceResult<RegisterResponseDto>.Forbidden(
                "COMPANY_DOMAIN_REQUIRED",
                "Email không thuộc tên miền công ty. Vui lòng sử dụng email công ty để đăng ký.");
        }
        if (await _authRepository.EmailExistsAsync(email, cancellationToken))
        {
            return ServiceResult<RegisterResponseDto>.Conflict("EMAIL_ALREADY_EXISTS", "Email already exists.");
        }

        var (account, token) = await _authRepository.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            var newAccount = new Account
            {
                Email = email,
                Password = _passwordHasher.HashPassword(request.Password),
                EmailVerified = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _authRepository.AddAccountAsync(newAccount, ct);
            await _authRepository.SaveChangesAsync(ct);

            var user = new User
            {
                AccountId = newAccount.Id,
                Email = email,
                FullName = request.FullName.Trim(),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _authRepository.AddUserAsync(user, ct);

            var rawToken = GenerateSecureToken();
            var emailVerificationToken = new EmailVerificationToken
            {
                AccountId = newAccount.Id,
                Token = rawToken,
                ExpiresAt = now.AddMinutes(15),
                CreatedAt = now
            };
            await _authRepository.AddEmailVerificationTokenAsync(emailVerificationToken, ct);
            await _authRepository.SaveChangesAsync(ct);

            return (newAccount, rawToken);
        }, cancellationToken);

        await _emailService.SendVerificationEmailAsync(account.Email, request.FullName.Trim(), token, cancellationToken);

        return ServiceResult<RegisterResponseDto>.Created(new RegisterResponseDto
        {
            AccountId = account.Id,
            Email = account.Email,
            EmailVerified = false
        });
    }

    public async Task<ServiceResult<AuthSessionDto>> LoginAsync(
        LoginRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var account = await _authRepository.GetAccountByEmailAsync(request.Email, cancellationToken);

        if (account == null || string.IsNullOrEmpty(account.Password) || !_passwordHasher.VerifyPassword(request.Password, account.Password))
        {
            return ServiceResult<AuthSessionDto>.Unauthorized("Invalid email or password.");
        }

        if (!account.IsActive)
        {
            return ServiceResult<AuthSessionDto>.Forbidden("ACCOUNT_DISABLED", "Your account has been disabled.");
        }

        var user = await _authRepository.GetUserByAccountIdAsync(account.Id, cancellationToken);
        if (!await IsAllowedCompanyEmailAsync(account.Email, user?.Id, cancellationToken))
        {
            return ServiceResult<AuthSessionDto>.Forbidden(
                "COMPANY_DOMAIN_REQUIRED",
                "Email không thuộc tên miền công ty, không thể đăng nhập.");
        }

        if (!account.EmailVerified)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var rawToken = GenerateSecureToken();
            var emailVerificationToken = new EmailVerificationToken
            {
                AccountId = account.Id,
                Token = rawToken,
                ExpiresAt = now.AddMinutes(15),
                CreatedAt = now
            };
            await _authRepository.AddEmailVerificationTokenAsync(emailVerificationToken, cancellationToken);
            await _authRepository.SaveChangesAsync(cancellationToken);
            await _emailService.SendVerificationEmailAsync(account.Email, user?.FullName ?? account.Email, rawToken, cancellationToken);

            return ServiceResult<AuthSessionDto>.Forbidden(
                "EMAIL_NOT_VERIFIED",
                "Tài khoản chưa xác thực email. Hệ thống đã gửi email xác thực tới hộp thư của bạn — vui lòng kiểm tra email để kích hoạt.");
        }

        if (user == null)
        {
            return ServiceResult<AuthSessionDto>.NotFound("User information could not be found.");
        }

        var session = await IssueSessionAsync(account, user, ipAddress, userAgent, isNewAccount: null, cancellationToken);
        return ServiceResult<AuthSessionDto>.Ok(session);
    }

    public async Task<ServiceResult<AuthSessionDto>> GoogleLoginAsync(
        GoogleLoginRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return ServiceResult<AuthSessionDto>.BadRequest("TOKEN_INVALID", "The Google ID token is invalid.");
        }

        var googlePayload = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (googlePayload == null)
        {
            return ServiceResult<AuthSessionDto>.BadRequest("TOKEN_INVALID", "The Google ID token is invalid.");
        }

        var email = googlePayload.Email;
        var fullName = googlePayload.FullName;
        var googleSub = googlePayload.Subject;

        return await _authRepository.ExecuteInTransactionAsync(async ct =>
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var isNewAccount = false;

            var account = await _authRepository.GetAccountByEmailAsync(email, ct);
            var existingUser = account != null ? await _authRepository.GetUserByAccountIdAsync(account.Id, ct) : null;
            if (!await IsAllowedCompanyEmailAsync(email, existingUser?.Id, ct))
            {
                return ServiceResult<AuthSessionDto>.Forbidden(
                    "COMPANY_DOMAIN_REQUIRED",
                    "Email không thuộc tên miền công ty, không thể đăng nhập.");
            }
            if (account == null)
            {
                isNewAccount = true;
                account = new Account
                {
                    Email = email,
                    Password = null,
                    EmailVerified = true,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _authRepository.AddAccountAsync(account, ct);
                await _authRepository.SaveChangesAsync(ct);

                var newUser = new User
                {
                    AccountId = account.Id,
                    Email = email,
                    FullName = fullName,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _authRepository.AddUserAsync(newUser, ct);
                await _authRepository.SaveChangesAsync(ct);
            }
            else if (!account.IsActive)
            {
                return ServiceResult<AuthSessionDto>.Forbidden("ACCOUNT_DISABLED", "Your account has been disabled.");
            }

            var externalLoginExists = await _authRepository.ExternalLoginExistsAsync("google", googleSub, ct);
            if (!externalLoginExists)
            {
                await _authRepository.AddExternalLoginAsync(new ExternalLogin
                {
                    AccountId = account.Id,
                    Provider = "google",
                    ProviderUserId = googleSub,
                    EmailAtProvider = email,
                    CreatedAt = now
                }, ct);
                await _authRepository.SaveChangesAsync(ct);
            }

            var user = await _authRepository.GetUserByAccountIdAsync(account.Id, ct);
            if (user == null)
            {
                return ServiceResult<AuthSessionDto>.NotFound("User information could not be found.");
            }

            var session = await IssueSessionAsync(account, user, ipAddress, userAgent, isNewAccount, ct);
            return ServiceResult<AuthSessionDto>.Ok(session);
        }, cancellationToken);
    }

    public async Task<ServiceResult<TokenRefreshResponseDto>> RefreshTokenAsync(
        RefreshTokenRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult<TokenRefreshResponseDto>.Unauthorized("The refresh token is invalid or has expired.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var hashedToken = _jwtTokenGenerator.HashRefreshToken(request.RefreshToken);
        var existingToken = await _authRepository.GetRefreshTokenByHashAsync(hashedToken, cancellationToken);

        if (existingToken == null || existingToken.RevokedAt != null || existingToken.ExpiresAt <= now)
        {
            return ServiceResult<TokenRefreshResponseDto>.Unauthorized("The refresh token is invalid or has expired.");
        }

        var account = await _authRepository.GetAccountByIdAsync(existingToken.AccountId, cancellationToken);
        if (account == null || !account.IsActive)
        {
            return ServiceResult<TokenRefreshResponseDto>.Unauthorized("The account does not exist or has been disabled.");
        }

        var user = await _authRepository.GetUserByAccountIdAsync(account.Id, cancellationToken);
        if (user == null)
        {
            return ServiceResult<TokenRefreshResponseDto>.Unauthorized("User information could not be found.");
        }

        existingToken.RevokedAt = now;
        _authRepository.UpdateRefreshToken(existingToken);

        var (accessToken, expiresIn) = _jwtTokenGenerator.GenerateAccessToken(account, user);
        var newRawRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var newHashedRefreshToken = _jwtTokenGenerator.HashRefreshToken(newRawRefreshToken);

        await _authRepository.AddRefreshTokenAsync(new RefreshToken
        {
            AccountId = account.Id,
            TokenHash = newHashedRefreshToken,
            IssuedAt = now,
            ExpiresAt = now.AddDays(7),
            IpAddress = ipAddress,
            UserAgent = userAgent
        }, cancellationToken);

        await _authRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult<TokenRefreshResponseDto>.Ok(new TokenRefreshResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRawRefreshToken,
            ExpiresIn = expiresIn
        });
    }

    public async Task<ServiceResult> LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hashedToken = _jwtTokenGenerator.HashRefreshToken(request.RefreshToken);
            var existingToken = await _authRepository.GetRefreshTokenByHashAsync(hashedToken, cancellationToken);

            if (existingToken != null && existingToken.RevokedAt == null)
            {
                existingToken.RevokedAt = _clock.GetUtcNow().UtcDateTime;
                _authRepository.UpdateRefreshToken(existingToken);
                await _authRepository.SaveChangesAsync(cancellationToken);
            }
        }

        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult<AuthMeResponseDto>> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<AuthMeResponseDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var user = await _authRepository.GetUserByIdAsync(userId.Value, cancellationToken);
        if (user == null)
        {
            return ServiceResult<AuthMeResponseDto>.NotFound("User could not be found.");
        }

        var account = await _authRepository.GetAccountByIdAsync(user.AccountId, cancellationToken);
        var companyMemberships = await _authRepository.GetUserCompanyMembershipsAsync(userId.Value, cancellationToken);
        var activeCompanyRoles = companyMemberships.ToDictionary(cm => cm.CompanyId, cm => cm.Role);
        var workspaceMemberships = await _authRepository.GetUserWorkspaceMembershipsAsync(userId.Value, activeCompanyRoles.Keys, cancellationToken);

        var companies = BuildUserCompanies(companyMemberships);
        var workspaces = BuildUserWorkspaces(workspaceMemberships, activeCompanyRoles);

        return ServiceResult<AuthMeResponseDto>.Ok(new AuthMeResponseDto
        {
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                EmailVerified = account?.EmailVerified ?? false
            },
            Companies = companies,
            Workspaces = workspaces
        });
    }

    public async Task<ServiceResult<object>> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var tokenEntity = await _authRepository.GetEmailVerificationTokenAsync(token, cancellationToken);
        if (tokenEntity == null)
        {
            return ServiceResult<object>.BadRequest("TOKEN_INVALID", "The verification token is invalid.");
        }

        if (tokenEntity.ExpiresAt <= now)
            return ServiceResult<object>.BadRequest("TOKEN_EXPIRED", "The verification token has expired.");

        if (tokenEntity.UsedAt != null)
            return ServiceResult<object>.BadRequest("TOKEN_INVALID", "The verification token has already been used.");

        tokenEntity.UsedAt = now;
        _authRepository.UpdateEmailVerificationToken(tokenEntity);

        var account = await _authRepository.GetAccountByIdAsync(tokenEntity.AccountId, cancellationToken);
        if (account != null)
        {
            account.EmailVerified = true;
            account.UpdatedAt = now;
            _authRepository.UpdateAccount(account);
        }

        await _authRepository.SaveChangesAsync(cancellationToken);
        return ServiceResult<object>.Ok(new { verified = true });
    }

    public async Task<ServiceResult> ResendEmailVerificationAsync(string email, CancellationToken cancellationToken = default)
    {
        var account = await _authRepository.GetAccountByEmailAsync(email, cancellationToken);
        if (account != null && !account.EmailVerified)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var token = GenerateSecureToken();

            await _authRepository.AddEmailVerificationTokenAsync(new EmailVerificationToken
            {
                AccountId = account.Id,
                Token = token,
                ExpiresAt = now.AddMinutes(15),
                CreatedAt = now
            }, cancellationToken);

            await _authRepository.SaveChangesAsync(cancellationToken);

            var user = await _authRepository.GetUserByAccountIdAsync(account.Id, cancellationToken);
            await _emailService.SendVerificationEmailAsync(account.Email, user?.FullName ?? account.Email, token, cancellationToken);
        }

        return ServiceResult.Accepted();
    }

    public async Task<ServiceResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var account = await _authRepository.GetAccountByEmailAsync(email, cancellationToken);
        if (account != null)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var token = GenerateSecureToken();

            await _authRepository.AddPasswordResetTokenAsync(new PasswordResetToken
            {
                AccountId = account.Id,
                Token = token,
                ExpiresAt = now.AddHours(2),
                CreatedAt = now
            }, cancellationToken);

            await _authRepository.SaveChangesAsync(cancellationToken);

            var user = await _authRepository.GetUserByAccountIdAsync(account.Id, cancellationToken);
            await _emailService.SendPasswordResetEmailAsync(account.Email, user?.FullName ?? account.Email, token, cancellationToken);
        }

        return ServiceResult.Accepted();
    }

    public async Task<ServiceResult<object>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationErrors = new Dictionary<string, string[]>();
        ValidatePasswordRules(request.NewPassword, validationErrors);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<object>.Validation(validationErrors);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var tokenEntity = await _authRepository.GetPasswordResetTokenAsync(request.Token, cancellationToken);
        if (tokenEntity == null)
        {
            return ServiceResult<object>.BadRequest("TOKEN_INVALID", "The password reset token is invalid.");
        }

        if (tokenEntity.ExpiresAt <= now)
            return ServiceResult<object>.BadRequest("TOKEN_EXPIRED", "The password reset token has expired.");

        if (tokenEntity.UsedAt != null)
            return ServiceResult<object>.BadRequest("TOKEN_INVALID", "The password reset token has already been used.");

        tokenEntity.UsedAt = now;
        _authRepository.UpdatePasswordResetToken(tokenEntity);

        var account = await _authRepository.GetAccountByIdAsync(tokenEntity.AccountId, cancellationToken);
        if (account != null)
        {
            account.Password = _passwordHasher.HashPassword(request.NewPassword);
            account.UpdatedAt = now;
            _authRepository.UpdateAccount(account);
        }

        await _authRepository.SaveChangesAsync(cancellationToken);
        return ServiceResult<object>.Ok(new { success = true });
    }

    public async Task<ServiceResult<object>> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<object>.Unauthorized("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

        var errors = new Dictionary<string, string[]>();
        ValidatePasswordRules(request.NewPassword, errors);
        if (errors.Count > 0)
            return ServiceResult<object>.ValidationFailure(errors);

        var user = await _authRepository.GetUserByIdAsync(userId.Value, cancellationToken);
        if (user == null)
            return ServiceResult<object>.NotFound("Không tìm thấy tài khoản.");

        var account = await _authRepository.GetAccountByIdAsync(user.AccountId, cancellationToken);
        if (account == null)
            return ServiceResult<object>.NotFound("Không tìm thấy tài khoản.");

        if (!string.IsNullOrEmpty(account.Password))
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) ||
                !_passwordHasher.VerifyPassword(request.CurrentPassword, account.Password))
            {
                return ServiceResult<object>.ValidationFailure(new Dictionary<string, string[]>
                {
                    ["currentPassword"] = new[] { "Mật khẩu hiện tại không đúng." }
                });
            }
        }

        account.Password = _passwordHasher.HashPassword(request.NewPassword);
        account.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        _authRepository.UpdateAccount(account);
        await _authRepository.SaveChangesAsync(cancellationToken);
        return ServiceResult<object>.Ok(new { success = true });
    }

    public static void ValidatePasswordRules(string password, Dictionary<string, string[]> errors)
    {
        var passwordErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            passwordErrors.Add("Password must contain at least 8 characters.");
        }
        else
        {
            if (!password.Any(char.IsDigit))
                passwordErrors.Add("Password must contain at least one digit.");

            if (!password.Any(char.IsUpper) || !password.Any(char.IsLower))
                passwordErrors.Add("Password must contain both uppercase and lowercase letters.");

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                passwordErrors.Add("Password must contain at least one special character.");
        }

        if (passwordErrors.Count > 0)
        {
            errors["password"] = passwordErrors.ToArray();
        }
    }

    public static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<AuthSessionDto> IssueSessionAsync(
        Account account,
        User user,
        string? ipAddress,
        string? userAgent,
        bool? isNewAccount,
        CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var (accessToken, expiresIn) = _jwtTokenGenerator.GenerateAccessToken(account, user);
        var rawRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var hashedRefreshToken = _jwtTokenGenerator.HashRefreshToken(rawRefreshToken);

        await _authRepository.AddRefreshTokenAsync(new RefreshToken
        {
            AccountId = account.Id,
            TokenHash = hashedRefreshToken,
            IssuedAt = now,
            ExpiresAt = now.AddDays(7),
            IpAddress = ipAddress,
            UserAgent = userAgent
        }, ct);

        await _authRepository.SaveChangesAsync(ct);

        return new AuthSessionDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresIn = expiresIn,
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                EmailVerified = account.EmailVerified
            },
            IsNewAccount = isNewAccount
        };
    }

    private static List<UserCompanyDto> BuildUserCompanies(List<CompanyMember> memberships)
    {
        return memberships.Select(cm =>
        {
            List<string> perms;
            if (cm.Role.Code == CompanyRoleCodes.TenantAdmin)
            {
                perms = PermissionKeys.AllTenantKeys.ToList();
            }
            else if (cm.Role.IsActive)
            {
                perms = cm.Role.RolePermissions
                    .Where(rp => rp.Permission != null && rp.Permission.IsActive)
                    .OrderBy(rp => rp.Permission.SortOrder)
                    .Select(rp => rp.Permission.Code)
                    .ToList();
            }
            else
            {
                perms = new List<string>();
            }

            return new UserCompanyDto
            {
                CompanyId = cm.CompanyId,
                CompanyName = cm.Company.Name,
                RoleName = cm.Role.Name,
                RoleId = cm.Role.Id,
                RoleCode = cm.Role.Code,
                Department = cm.Department == null
                    ? null
                    : new SSAM_Server.DTOs.Companies.DepartmentRefDto
                    {
                        DepartmentId = cm.Department.Id,
                        Name = cm.Department.Name,
                        Code = cm.Department.Code
                    },
                Permissions = perms
            };
        }).ToList();
    }

    private static List<UserWorkspaceDto> BuildUserWorkspaces(
        List<WorkspaceMember> memberWorkspaces,
        Dictionary<long, CompanyRole> activeCompanyRoles)
    {
        var result = new List<UserWorkspaceDto>();

        foreach (var wm in memberWorkspaces)
        {
            var ws = wm.Workspace;
            var companyId = ws.CompanyId!.Value;
            if (!activeCompanyRoles.TryGetValue(companyId, out var tenantRole))
                continue;

            List<string> perms;
            if (tenantRole.Code == CompanyRoleCodes.TenantAdmin)
            {
                perms = PermissionKeys.AllWorkspaceKeys.ToList();
            }
            else if (wm.Role != null && wm.Role.IsActive)
            {
                perms = wm.Role.RolePermissions
                    .Where(rp => rp.Permission != null && rp.Permission.IsActive)
                    .OrderBy(rp => rp.Permission.SortOrder)
                    .Select(rp => rp.Permission.Code)
                    .ToList();
            }
            else
            {
                perms = new List<string>();
            }

            result.Add(new UserWorkspaceDto
            {
                WorkspaceId = ws.Id,
                WorkspaceName = ws.Name,
                Code = ws.Code,
                CompanyId = ws.CompanyId,
                Status = ws.IsActive ? "ACTIVE" : "ARCHIVED",
                Role = wm.Role != null
                    ? new RoleRefDto
                    {
                        RoleId = wm.Role.Id,
                        Code = wm.Role.Code,
                        Name = wm.Role.Name,
                        Scope = wm.Role.Scope
                    }
                    : null,
                Permissions = perms
            });
        }

        return result;
    }
}
