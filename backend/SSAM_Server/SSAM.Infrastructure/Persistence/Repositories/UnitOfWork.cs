using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Storage;
using SSAM.Application.Common.Interfaces.Repositories;
using SSAM.Domain.Entities.Companies;
using SSAM.Domain.Entities.Identity;

namespace SSAM.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;

    // Explicit repository backing fields
    private IGenericRepository<Account>? _accounts;
    private IGenericRepository<User>? _users;
    private IGenericRepository<Company>? _companies;
    private IGenericRepository<CompanyMember>? _companyMembers;
    private IGenericRepository<CompanyRole>? _companyRoles;
    private IGenericRepository<ExternalLogin>? _externalLogins;
    private IGenericRepository<EmailVerificationToken>? _emailVerificationTokens;
    private IGenericRepository<PasswordResetToken>? _passwordResetTokens;
    private IGenericRepository<RefreshToken>? _refreshTokens;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<T> Repository<T>() where T : class
    {
        return (IGenericRepository<T>)_repositories.GetOrAdd(
            typeof(T),
            _ => new GenericRepository<T>(_context));
    }

    public IGenericRepository<Account> Accounts => _accounts ??= Repository<Account>();
    public IGenericRepository<User> Users => _users ??= Repository<User>();
    public IGenericRepository<Company> Companies => _companies ??= Repository<Company>();
    public IGenericRepository<CompanyMember> CompanyMembers => _companyMembers ??= Repository<CompanyMember>();
    public IGenericRepository<CompanyRole> CompanyRoles => _companyRoles ??= Repository<CompanyRole>();
    public IGenericRepository<ExternalLogin> ExternalLogins => _externalLogins ??= Repository<ExternalLogin>();
    public IGenericRepository<EmailVerificationToken> EmailVerificationTokens => _emailVerificationTokens ??= Repository<EmailVerificationToken>();
    public IGenericRepository<PasswordResetToken> PasswordResetTokens => _passwordResetTokens ??= Repository<PasswordResetToken>();
    public IGenericRepository<RefreshToken> RefreshTokens => _refreshTokens ??= Repository<RefreshToken>();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (_transaction != null)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
