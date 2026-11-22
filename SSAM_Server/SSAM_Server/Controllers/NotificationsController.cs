using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Security;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly NotificationBroadcaster _broadcaster;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(
        INotificationService notifications,
        NotificationBroadcaster broadcaster,
        ICurrentUser currentUser)
    {
        _notifications = notifications;
        _broadcaster = broadcaster;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var result = await _notifications.ListAsync(unreadOnly, limit, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("{notificationId:long}/read")]
    public async Task<IActionResult> MarkRead(long notificationId, CancellationToken ct = default)
    {
        var result = await _notifications.MarkReadAsync(notificationId, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var result = await _notifications.MarkAllReadAsync(ct);
        return result.ToActionResult(this);
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var (subId, reader) = _broadcaster.Subscribe(userId.Value);
        try
        {
            await Response.WriteAsync(": connected\n\n", ct);
            await Response.Body.FlushAsync(ct);

            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(25));
            while (!ct.IsCancellationRequested)
            {
                var readTask = reader.WaitToReadAsync(ct).AsTask();
                var tickTask = heartbeat.WaitForNextTickAsync(ct).AsTask();
                var completed = await Task.WhenAny(readTask, tickTask);

                if (completed == tickTask)
                {
                    await Response.WriteAsync(": ping\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    continue;
                }

                if (!await readTask) break;

                while (reader.TryRead(out var dto))
                {
                    var json = JsonSerializer.Serialize(dto);
                    await Response.WriteAsync($"event: notification\ndata: {json}\n\n", ct);
                }
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            _broadcaster.Unsubscribe(userId.Value, subId);
        }
    }
}
