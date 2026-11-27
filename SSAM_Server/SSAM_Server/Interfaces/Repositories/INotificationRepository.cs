using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface INotificationRepository : IRepositoryBase
{
    IQueryable<Notification> Notifications { get; }
}
