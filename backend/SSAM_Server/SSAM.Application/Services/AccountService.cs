using SSAM.Application.Common.Interfaces.Repositories;
using SSAM.Application.Common.Interfaces.Services;
using SSAM.Application.DTOs.Auth;
using SSAM.Domain.Entities.Identity;

namespace SSAM.Application.Services;

public class AccountService : IAccountService
{
    private readonly IUnitOfWork _unitOfWork;

    public AccountService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        // 1. Check if email exists
        var emailExists = await _unitOfWork.Accounts.ExistsAsync(a => a.Email == request.Email, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException($"An account with email '{request.Email}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 2. Create Account
            var account = new Account
            {
                Email = request.Email,
                Password = request.Password, // Can integrate BCrypt / ASP.NET PasswordHasher
                EmailVerified = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Accounts.AddAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 3. Create associated User profile (1-to-1)
            var user = new User
            {
                AccountId = account.Id,
                Email = request.Email,
                FullName = request.FullName,
                Phone = request.Phone,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new AuthResponseDto
            {
                AccountId = account.Id,
                UserId = user.Id,
                Email = account.Email,
                FullName = user.FullName,
                AccessToken = "sample_jwt_access_token"
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var account = await _unitOfWork.Accounts.FirstOrDefaultAsync(
            a => a.Email == request.Email && a.IsActive, 
            cancellationToken);

        if (account == null || account.Password != request.Password)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.AccountId == account.Id, cancellationToken);

        return new AuthResponseDto
        {
            AccountId = account.Id,
            UserId = user?.Id ?? 0,
            Email = account.Email,
            FullName = user?.FullName ?? string.Empty,
            AccessToken = "sample_jwt_access_token"
        };
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var verificationToken = await _unitOfWork.EmailVerificationTokens.FirstOrDefaultAsync(
            t => t.Token == token && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow,
            cancellationToken);

        if (verificationToken == null)
        {
            return false;
        }

        verificationToken.UsedAt = DateTime.UtcNow;

        var account = await _unitOfWork.Accounts.GetByIdAsync(verificationToken.AccountId, cancellationToken);
        if (account != null)
        {
            account.EmailVerified = true;
            account.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Accounts.Update(account);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
