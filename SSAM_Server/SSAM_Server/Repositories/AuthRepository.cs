using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _db;

    public AuthRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => _db.Accounts.AsNoTracking().AnyAsync(a => a.Email == email, ct);

    public Task<Account?> GetAccountByEmailAsync(string email, CancellationToken ct = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.Email == email, ct);

    public async Task<Account?> GetAccountByIdAsync(long accountId, CancellationToken ct = default)
        => await _db.Accounts.FindAsync(new object[] { accountId }, ct);

    public async Task AddAccountAsync(Account account, CancellationToken ct = default)
        => await _db.Accounts.AddAsync(account, ct);

    public void UpdateAccount(Account account)
    {
        if (_db.Entry(account).State == EntityState.Detached)
            _db.Accounts.Update(account);
    }

    public Task<User?> GetUserByAccountIdAsync(long accountId, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.AccountId == accountId, ct);

    public async Task<User?> GetUserByIdAsync(long userId, CancellationToken ct = default)
        => await _db.Users.FindAsync(new object[] { userId }, ct);

    public async Task AddUserAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);

    public Task<List<CompanyMember>> GetUserCompanyMembershipsAsync(long userId, CancellationToken ct = default)
        => _db.CompanyMembers
            .AsNoTracking()
            .Include(cm => cm.Company)
            .Include(cm => cm.Department)
            .Include(cm => cm.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(cm => cm.UserId == userId && cm.IsActive)
            .OrderBy(cm => cm.JoinedAt)
            .ToListAsync(ct);

    public Task<List<WorkspaceMember>> GetUserWorkspaceMembershipsAsync(long userId, IEnumerable<long> companyIds, CancellationToken ct = default)
        => _db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.Workspace)
            .Include(wm => wm.Role)
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(wm =>
                wm.UserId == userId &&
                wm.IsActive &&
                wm.Workspace.CompanyId != null &&
                companyIds.Contains(wm.Workspace.CompanyId.Value))
            .OrderBy(wm => wm.JoinedAt)
            .ToListAsync(ct);

    public Task<bool> ExternalLoginExistsAsync(string provider, string providerUserId, CancellationToken ct = default)
        => _db.ExternalLogins.AsNoTracking().AnyAsync(el => el.Provider == provider && el.ProviderUserId == providerUserId, ct);

    public async Task AddExternalLoginAsync(ExternalLogin externalLogin, CancellationToken ct = default)
        => await _db.ExternalLogins.AddAsync(externalLogin, ct);

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default)
        => await _db.RefreshTokens.AddAsync(refreshToken, ct);

    public void UpdateRefreshToken(RefreshToken refreshToken)
    {
        if (_db.Entry(refreshToken).State == EntityState.Detached)
            _db.RefreshTokens.Update(refreshToken);
    }

    public Task<EmailVerificationToken?> GetEmailVerificationTokenAsync(string token, CancellationToken ct = default)
        => _db.EmailVerificationTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task AddEmailVerificationTokenAsync(EmailVerificationToken tokenEntity, CancellationToken ct = default)
        => await _db.EmailVerificationTokens.AddAsync(tokenEntity, ct);

    public void UpdateEmailVerificationToken(EmailVerificationToken tokenEntity)
    {
        if (_db.Entry(tokenEntity).State == EntityState.Detached)
            _db.EmailVerificationTokens.Update(tokenEntity);
    }

    public Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token, CancellationToken ct = default)
        => _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task AddPasswordResetTokenAsync(PasswordResetToken tokenEntity, CancellationToken ct = default)
        => await _db.PasswordResetTokens.AddAsync(tokenEntity, ct);

    public void UpdatePasswordResetToken(PasswordResetToken tokenEntity)
    {
        if (_db.Entry(tokenEntity).State == EntityState.Detached)
            _db.PasswordResetTokens.Update(tokenEntity);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(work, ct);
}
