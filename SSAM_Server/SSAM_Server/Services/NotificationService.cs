using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Notifications;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly NotificationBroadcaster _broadcaster;

    public NotificationService(AppDbContext db, ICurrentUser currentUser, NotificationBroadcaster broadcaster)
    {
        _db = db;
        _currentUser = currentUser;
        _broadcaster = broadcaster;
    }

    public async Task<ServiceResult<NotificationsResponseDto>> ListAsync(bool unreadOnly = false, int limit = 50, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult<NotificationsResponseDto>.Unauthorized("Your session is invalid or has expired.");

        var safeLimit = Math.Clamp(limit, 1, 100);
        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId.Value);

        var unreadCount = await query.CountAsync(n => !n.IsRead, ct);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(safeLimit)
            .Select(n => new NotificationDto
            {
                NotificationId = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                LinkUrl = n.LinkUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt.ToString("O", CultureInfo.InvariantCulture)
            })
            .ToListAsync(ct);

        return ServiceResult<NotificationsResponseDto>.Ok(new NotificationsResponseDto
        {
            Items = items,
            UnreadCount = unreadCount
        });
    }

    public async Task<ServiceResult> MarkReadAsync(long notificationId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult.Unauthorized("Your session is invalid or has expired.");

        var entity = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId.Value, ct);
        if (entity == null)
            return ServiceResult.NotFound("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo.");

        if (!entity.IsRead)
        {
            entity.IsRead = true;
            await _db.SaveChangesAsync(ct);
        }

        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
            return ServiceResult.Unauthorized("Your session is invalid or has expired.");

        await _db.Notifications
            .Where(n => n.UserId == userId.Value && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        return ServiceResult.NoContent();
    }

    public async Task NotifyAsync(long userId, string type, string title, string message, string? linkUrl = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var entity = new Notification
        {
            UserId = userId,
            Type = type.Length > 40 ? type[..40] : type,
            Title = title.Length > 200 ? title[..200] : title,
            Message = message.Length > 500 ? message[..500] : message,
            LinkUrl = linkUrl?.Length > 500 ? linkUrl[..500] : linkUrl,
            IsRead = false,
            CreatedAt = now
        };

        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync(ct);

        _broadcaster.Publish(userId, new NotificationDto
        {
            NotificationId = entity.Id,
            Type = entity.Type,
            Title = entity.Title,
            Message = entity.Message,
            LinkUrl = entity.LinkUrl,
            IsRead = false,
            CreatedAt = entity.CreatedAt.ToString("O", CultureInfo.InvariantCulture)
        });
    }
}
