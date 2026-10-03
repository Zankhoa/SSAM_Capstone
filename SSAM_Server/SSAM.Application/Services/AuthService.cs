using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SSAM.Application.Common.Exceptions;
using SSAM.Application.Common.Interfaces.Repositories;
using SSAM.Application.Common.Interfaces.Security;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Auth;
using SSAM.Domain.Entities.Identity;

namespace SSAM.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher _passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailSender emailSender,
        IGoogleTokenValidator googleTokenValidator)
    {
        _unitOfWork = unitOfWork;
        this._passwordHasher = _passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailSender = emailSender;
        _googleTokenValidator = googleTokenValidator;
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        // 1. Validation
        var validationErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FullName))
            validationErrors["fullName"] = new[] { "Field fullname is require" };
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            validationErrors["email"] = new[] { "Email is wrong format " };

        ValidatePasswordRules(request.Password, validationErrors);

        if (validationErrors.Count > 0)
        {
            throw AppException.Validation(validationErrors);
        }

        // 2. Check if email exists
        var emailExists = await _unitOfWork.Accounts.ExistsAsync(a => a.Email == request.Email, cancellationToken);
        if (emailExists)
        {
            throw AppException.Conflict("EMAIL_ALREADY_EXISTS", "Email existed");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var account = new Account
            {
                Email = request.Email,
                Password = _passwordHasher.HashPassword(request.Password),
                EmailVerified = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Accounts.AddAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var user = new User
            {
                AccountId = account.Id,
                Email = request.Email,
                FullName = request.FullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Generate Email verification token
            var token = GenerateSecureToken();
            var emailVerificationToken = new EmailVerificationToken
            {
                AccountId = account.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.EmailVerificationTokens.AddAsync(emailVerificationToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            await _emailSender.SendEmailVerificationAsync(account.Email, token, cancellationToken);

            return new RegisterResponseDto
            {
                AccountId = account.Id,
                Email = account.Email,
                EmailVerified = false
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
    public async Task<AuthSessionDto> LoginAsync(
        LoginRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // 1. Find the account
        var account = await _unitOfWork.Accounts.FirstOrDefaultAsync(
            a => a.Email == request.Email,
            cancellationToken);

        // 2. Use a generic error message for security reasons
        if (account == null || string.IsNullOrEmpty(account.Password) || !_passwordHasher.VerifyPassword(request.Password, account.Password))
        {
            throw AppException.Unauthorized("Invalid email or password.");
        }

        // 3. Check account status
        if (!account.IsActive)
        {
            throw AppException.Forbidden( "ACCOUNT_DISABLED", "Your account has been disabled.");
        }

        if (!account.EmailVerified)
        {
            throw AppException.Forbidden( "EMAIL_NOT_VERIFIED", "Your email address has not been verified.");
        }

        // 4. Find the associated user
        var user = await _unitOfWork.Users.FirstOrDefaultAsync( u => u.AccountId == account.Id, cancellationToken);

        if (user == null)
        {
            throw AppException.NotFound( "User information could not be found.");
        }

        // 5. Generate access and refresh tokens
        var (accessToken, expiresIn) =
            _jwtTokenGenerator.GenerateAccessToken(account, user);

        var rawRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var hashedRefreshToken =
            _jwtTokenGenerator.HashRefreshToken(rawRefreshToken);

        var refreshTokenEntity = new RefreshToken
        {
            AccountId = account.Id,
            TokenHash = hashedRefreshToken,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await _unitOfWork.RefreshTokens.AddAsync(
            refreshTokenEntity,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            }
        };
    }

    public async Task<AuthSessionDto> GoogleLoginAsync(
        GoogleLoginRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            throw AppException.BadRequest(
                "TOKEN_INVALID",
                "The Google ID token is invalid.");
        }

        var googlePayload = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (googlePayload == null)
        {
            throw AppException.BadRequest(
                "TOKEN_INVALID",
                "The Google ID token is invalid.");
        }

        var email = googlePayload.Email;
        var fullName = googlePayload.FullName;
        var googleSub = googlePayload.Subject;

        var isNewAccount = false;

        var account = await _unitOfWork.Accounts.FirstOrDefaultAsync(
            a => a.Email == email,
            cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            if (account == null)
            {
                isNewAccount = true;

                account = new Account
                {
                    Email = email,
                    Password = null, // OAuth-only account
                    EmailVerified = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Accounts.AddAsync(
                    account,
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var newUser = new User
                {
                    AccountId = account.Id,
                    Email = email,
                    FullName = fullName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Users.AddAsync(
                    newUser,
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Link the Google account if it has not been linked yet
            var externalLoginExists =
                await _unitOfWork.ExternalLogins.ExistsAsync(
                    el => el.Provider == "google" &&
                          el.ProviderUserId == googleSub,
                    cancellationToken);

            if (!externalLoginExists)
            {
                var externalLogin = new ExternalLogin
                {
                    AccountId = account.Id,
                    Provider = "google",
                    ProviderUserId = googleSub,
                    EmailAtProvider = email,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.ExternalLogins.AddAsync(
                    externalLogin,
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(
                u => u.AccountId == account.Id,
                cancellationToken);

            if (user == null)
            {
                throw AppException.NotFound(
                    "User information could not be found.");
            }

            // Generate access and refresh tokens
            var (accessToken, expiresIn) =
                _jwtTokenGenerator.GenerateAccessToken(account, user);

            var rawRefreshToken =
                _jwtTokenGenerator.GenerateRefreshToken();

            var hashedRefreshToken =
                _jwtTokenGenerator.HashRefreshToken(rawRefreshToken);

            var refreshTokenEntity = new RefreshToken
            {
                AccountId = account.Id,
                TokenHash = hashedRefreshToken,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            await _unitOfWork.RefreshTokens.AddAsync(
                refreshTokenEntity,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

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
                    EmailVerified = true
                },
                IsNewAccount = isNewAccount
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(
                cancellationToken);

            throw;
        }
    }

    public async Task<TokenRefreshResponseDto> RefreshTokenAsync(
        RefreshTokenRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var hashedToken =
            _jwtTokenGenerator.HashRefreshToken(request.RefreshToken);

        var existingToken =
            await _unitOfWork.RefreshTokens.FirstOrDefaultAsync(
                rt => rt.TokenHash == hashedToken,
                cancellationToken);

        if (existingToken == null ||
            existingToken.RevokedAt != null ||
            existingToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw AppException.Unauthorized(
                "The refresh token is invalid or has expired.");
        }

        // Token rotation: revoke the old refresh token
        existingToken.RevokedAt = DateTime.UtcNow;

        _unitOfWork.RefreshTokens.Update(existingToken);

        // Fetch the associated account and user
        var account = await _unitOfWork.Accounts.GetByIdAsync(
            existingToken.AccountId,
            cancellationToken);

        if (account == null || !account.IsActive)
        {
            throw AppException.Unauthorized(
                "The account does not exist or has been disabled.");
        }

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.AccountId == account.Id,
            cancellationToken);

        if (user == null)
        {
            throw AppException.Unauthorized(
                "User information could not be found.");
        }

        // Issue a new access token and refresh token
        var (accessToken, expiresIn) =
            _jwtTokenGenerator.GenerateAccessToken(account, user);

        var newRawRefreshToken =
            _jwtTokenGenerator.GenerateRefreshToken();

        var newHashedRefreshToken =
            _jwtTokenGenerator.HashRefreshToken(newRawRefreshToken);

        var newRefreshTokenEntity = new RefreshToken
        {
            AccountId = account.Id,
            TokenHash = newHashedRefreshToken,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await _unitOfWork.RefreshTokens.AddAsync(
            newRefreshTokenEntity,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TokenRefreshResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRawRefreshToken,
            ExpiresIn = expiresIn
        };
    }

    public async Task LogoutAsync(
        LogoutRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var hashedToken =
            _jwtTokenGenerator.HashRefreshToken(request.RefreshToken);

        var existingToken =
            await _unitOfWork.RefreshTokens.FirstOrDefaultAsync(
                rt => rt.TokenHash == hashedToken,
                cancellationToken);

        if (existingToken != null &&
            existingToken.RevokedAt == null)
        {
            existingToken.RevokedAt = DateTime.UtcNow;

            _unitOfWork.RefreshTokens.Update(existingToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<AuthMeResponseDto> GetMeAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(
            userId,
            cancellationToken);

        if (user == null)
        {
            throw AppException.NotFound(
                "User could not be found.");
        }

        var account = await _unitOfWork.Accounts.GetByIdAsync(
            user.AccountId,
            cancellationToken);

        // Fetch the user's companies
        var companyMembers = await _unitOfWork.CompanyMembers.Query()
            .Include(cm => cm.Company)
            .Include(cm => cm.Role)
            .Where(cm => cm.UserId == userId && cm.IsActive)
            .ToListAsync(cancellationToken);

        var companies = companyMembers.Select(cm => new UserCompanyDto
        {
            CompanyId = cm.CompanyId,
            CompanyName = cm.Company.Name,
            RoleName = cm.Role.Name
        }).ToList();

        // Fetch the user's workspaces
        var workspaceMembers = await _unitOfWork.WorkspaceMembers.Query()
            .Include(wm => wm.Workspace)
            .Where(wm => wm.UserId == userId)
            .ToListAsync(cancellationToken);

        var workspaces = workspaceMembers.Select(wm => new UserWorkspaceDto
        {
            WorkspaceId = wm.WorkspaceId,
            WorkspaceName = wm.Workspace.Name,
            CompanyId = wm.Workspace.CompanyId
        }).ToList();

        return new AuthMeResponseDto
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
        };
    }

    public async Task<bool> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenEntity =
            await _unitOfWork.EmailVerificationTokens.FirstOrDefaultAsync(
                t => t.Token == token,
                cancellationToken);

        if (tokenEntity == null)
        {
            throw AppException.BadRequest(
                "TOKEN_INVALID",
                "The verification token is invalid.");
        }

        if (tokenEntity.ExpiresAt <= DateTime.UtcNow)
        {
            throw AppException.BadRequest(
                "TOKEN_EXPIRED",
                "The verification token has expired.");
        }

        if (tokenEntity.UsedAt != null)
        {
            throw AppException.BadRequest(
                "TOKEN_INVALID",
                "The verification token has already been used.");
        }

        tokenEntity.UsedAt = DateTime.UtcNow;

        _unitOfWork.EmailVerificationTokens.Update(tokenEntity);

        var account = await _unitOfWork.Accounts.GetByIdAsync(
            tokenEntity.AccountId,
            cancellationToken);

        if (account != null)
        {
            account.EmailVerified = true;
            account.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Accounts.Update(account);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task ResendEmailVerificationAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var account = await _unitOfWork.Accounts.FirstOrDefaultAsync(
            a => a.Email == email,
            cancellationToken);

        if (account != null && !account.EmailVerified)
        {
            var token = GenerateSecureToken();

            var emailVerificationToken = new EmailVerificationToken
            {
                AccountId = account.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.EmailVerificationTokens.AddAsync(
                emailVerificationToken,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _emailSender.SendEmailVerificationAsync(account.Email, token, cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var account = await _unitOfWork.Accounts.FirstOrDefaultAsync(
            a => a.Email == email,
            cancellationToken);

        if (account != null)
        {
            var token = GenerateSecureToken();

            var passwordResetToken = new PasswordResetToken
            {
                AccountId = account.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.PasswordResetTokens.AddAsync(
                passwordResetToken,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _emailSender.SendPasswordResetAsync(account.Email, token, cancellationToken);
        }
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = new Dictionary<string, string[]>();

        ValidatePasswordRules(
            request.NewPassword,
            validationErrors);

        if (validationErrors.Count > 0)
        {
            throw AppException.Validation(validationErrors);
        }

        var tokenEntity =
            await _unitOfWork.PasswordResetTokens.FirstOrDefaultAsync(
                t => t.Token == request.Token,
                cancellationToken);

        if (tokenEntity == null)
        {
            throw AppException.BadRequest(
                "TOKEN_INVALID",
                "The password reset token is invalid.");
        }

        if (tokenEntity.ExpiresAt <= DateTime.UtcNow)
        {
            throw AppException.BadRequest(
                "TOKEN_EXPIRED",
                "The password reset token has expired.");
        }

        if (tokenEntity.UsedAt != null)
        {
            throw AppException.BadRequest(
                "TOKEN_INVALID",
                "The password reset token has already been used.");
        }

        tokenEntity.UsedAt = DateTime.UtcNow;

        _unitOfWork.PasswordResetTokens.Update(tokenEntity);

        var account = await _unitOfWork.Accounts.GetByIdAsync(
            tokenEntity.AccountId,
            cancellationToken);

        if (account != null)
        {
            account.Password =
                _passwordHasher.HashPassword(request.NewPassword);

            account.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Accounts.Update(account);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePasswordRules(
        string password,
        Dictionary<string, string[]> errors)
    {
        var passwordErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < 8)
        {
            passwordErrors.Add(
                "Password must contain at least 8 characters.");
        }

        if (!password.Any(char.IsDigit))
        {
            passwordErrors.Add(
                "Password must contain at least one digit.");
        }

        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower))
        {
            passwordErrors.Add(
                "Password must contain both uppercase and lowercase letters.");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            passwordErrors.Add(
                "Password must contain at least one special character.");
        }

        if (passwordErrors.Count > 0)
        {
            errors["password"] = passwordErrors.ToArray();
        }
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];

        using var rng = RandomNumberGenerator.Create();

        rng.GetBytes(bytes);

        return Convert.ToHexString(bytes).ToLower();
    }
}

