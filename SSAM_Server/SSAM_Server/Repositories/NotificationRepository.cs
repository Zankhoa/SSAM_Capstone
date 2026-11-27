using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class NotificationRepository : RepositoryBase, INotificationRepository
{
    public NotificationRepository(AppDbContext db) : base(db) { }

    public IQueryable<Notification> Notifications => _db.Notifications;
}
