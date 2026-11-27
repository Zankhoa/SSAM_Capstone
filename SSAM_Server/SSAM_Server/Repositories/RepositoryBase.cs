using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;

namespace SSAM_Server.Repositories;

public abstract class RepositoryBase : IRepositoryBase
{
    protected readonly AppDbContext _db;

    protected RepositoryBase(AppDbContext db)
    {
        _db = db;
    }

    public void Add<T>(T entity) where T : class => _db.Set<T>().Add(entity);

    public void AddRange<T>(IEnumerable<T> entities) where T : class => _db.Set<T>().AddRange(entities);

    public void Update<T>(T entity) where T : class => _db.Set<T>().Update(entity);

    public void Remove<T>(T entity) where T : class => _db.Set<T>().Remove(entity);

    public void RemoveRange<T>(IEnumerable<T> entities) where T : class => _db.Set<T>().RemoveRange(entities);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(work, ct);

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
        => _db.ExecuteInTransactionAsync(async innerCt =>
        {
            await work(innerCt);
            return true;
        }, ct);
}
