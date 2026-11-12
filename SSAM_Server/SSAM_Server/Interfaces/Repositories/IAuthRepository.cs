using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IAuthRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<Account?> GetAccountByEmailAsync(string email, CancellationToken ct = default);
    Task<Account?> GetAccountByIdAsync(long accountId, CancellationToken ct = default);
    Task AddAccountAsync(Account account, CancellationToken ct = default);
    void UpdateAccount(Account account);

    Task<User?> GetUserByAccountIdAsync(long accountId, CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(long userId, CancellationToken ct = default);
    Task AddUserAsync(User user, CancellationToken ct = default);

    Task<List<CompanyMember>> GetUserCompanyMembershipsAsync(long userId, CancellationToken ct = default);
    Task<List<WorkspaceMember>> GetUserWorkspaceMembershipsAsync(long userId, IEnumerable<long> companyIds, CancellationToken ct = default);

    Task<bool> ExternalLoginExistsAsync(string provider, string providerUserId, CancellationToken ct = default);
    Task AddExternalLoginAsync(ExternalLogin externalLogin, CancellationToken ct = default);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default);
    void UpdateRefreshToken(RefreshToken refreshToken);

    Task<EmailVerificationToken?> GetEmailVerificationTokenAsync(string token, CancellationToken ct = default);
    Task AddEmailVerificationTokenAsync(EmailVerificationToken tokenEntity, CancellationToken ct = default);
    void UpdateEmailVerificationToken(EmailVerificationToken tokenEntity);

    Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token, CancellationToken ct = default);
    Task AddPasswordResetTokenAsync(PasswordResetToken tokenEntity, CancellationToken ct = default);
    void UpdatePasswordResetToken(PasswordResetToken tokenEntity);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default);
}
