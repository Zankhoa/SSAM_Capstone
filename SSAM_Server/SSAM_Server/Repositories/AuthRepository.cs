using Microsoft.EntityFrameworkCore;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Auth;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Repositories;

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

    Task<List<UserCompanyDto>> GetUserCompaniesAsync(long userId, CancellationToken ct = default);
    Task<List<UserWorkspaceDto>> GetUserWorkspacesAsync(long userId, CancellationToken ct = default);

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

    public async Task<List<UserCompanyDto>> GetUserCompaniesAsync(long userId, CancellationToken ct = default)
    {
        var memberships = await _db.CompanyMembers
            .AsNoTracking()
            .Include(cm => cm.Company)
            .Include(cm => cm.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(cm => cm.UserId == userId && cm.IsActive)
            .OrderBy(cm => cm.JoinedAt)
            .ToListAsync(ct);

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
                Permissions = perms
            };
        }).ToList();
    }

    public async Task<List<UserWorkspaceDto>> GetUserWorkspacesAsync(long userId, CancellationToken ct = default)
    {
        var activeCompanyRoles = await _db.CompanyMembers
            .AsNoTracking()
            .Include(cm => cm.Role)
            .Where(cm => cm.UserId == userId && cm.IsActive)
            .ToDictionaryAsync(cm => cm.CompanyId, cm => cm.Role, ct);

        var activeCompanyIds = activeCompanyRoles.Keys.ToHashSet();

        var personalWorkspaces = await _db.Workspaces
            .AsNoTracking()
            .Where(w => w.CompanyId == null && w.CreatedBy == userId)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);

        var memberWorkspaces = await _db.WorkspaceMembers
            .AsNoTracking()
            .Include(wm => wm.Workspace)
            .Include(wm => wm.Role)
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(wm =>
                wm.UserId == userId &&
                wm.IsActive &&
                wm.Workspace.CompanyId != null &&
                activeCompanyIds.Contains(wm.Workspace.CompanyId.Value))
            .OrderBy(wm => wm.JoinedAt)
            .ToListAsync(ct);

        var result = new List<UserWorkspaceDto>();

        foreach (var w in personalWorkspaces)
        {
            result.Add(new UserWorkspaceDto
            {
                WorkspaceId = w.Id,
                WorkspaceName = w.Name,
                Code = w.Code,
                CompanyId = null,
                Status = w.IsActive ? "ACTIVE" : "ARCHIVED",
                Role = null,
                Permissions = PermissionKeys.AllWorkspaceKeys.ToList()
            });
        }

        foreach (var wm in memberWorkspaces)
        {
            var ws = wm.Workspace;
            var companyId = ws.CompanyId!.Value;
            var tenantRole = activeCompanyRoles[companyId];

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
