using SSAM_Server.Common;
using SSAM_Server.DTOs.Notifications;

namespace SSAM_Server.Interfaces.Services;

public interface INotificationService
{
    Task<ServiceResult<NotificationsResponseDto>> ListAsync(bool unreadOnly = false, int limit = 50, CancellationToken ct = default);
    Task<ServiceResult> MarkReadAsync(long notificationId, CancellationToken ct = default);
    Task<ServiceResult> MarkAllReadAsync(CancellationToken ct = default);
    Task NotifyAsync(long userId, string type, string title, string message, string? linkUrl = null, CancellationToken ct = default);
}
