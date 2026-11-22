using System.Text.Json.Serialization;

namespace SSAM_Server.DTOs.Notifications;

public class NotificationDto
{
    [JsonPropertyName("notificationId")]
    public long NotificationId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "INFO";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("linkUrl")]
    public string? LinkUrl { get; set; }

    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public class NotificationsResponseDto
{
    [JsonPropertyName("items")]
    public List<NotificationDto> Items { get; set; } = new();

    [JsonPropertyName("unreadCount")]
    public int UnreadCount { get; set; }
}
