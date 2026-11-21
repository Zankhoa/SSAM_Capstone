using System.Globalization;
using System.Text.Json;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Models;

namespace SSAM_Server.Services.Common;

/// <summary>
/// Shared mapping, date formatting, and safe JSON deserialization helpers across application services.
/// </summary>
public static class ServiceMappingExtensions
{
    public static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Maps a <see cref="User"/> entity to a lightweight <see cref="UserRefDto"/>.
    /// </summary>
    public static UserRefDto MapUserRef(User u) => new()
    {
        UserId = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        AvatarUrl = u.AvatarUrl
    };

    /// <summary>
    /// Extension overload for mapping a <see cref="User"/> entity to a <see cref="UserRefDto"/>.
    /// </summary>
    public static UserRefDto ToUserRef(this User u) => MapUserRef(u);

    /// <summary>
    /// Formats a <see cref="DateTime"/> as an ISO-8601 UTC timestamp string with millisecond precision.
    /// </summary>
    public static string FormatUtc(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// Extension overload for formatting a <see cref="DateTime"/> as an ISO-8601 UTC string.
    /// </summary>
    public static string ToUtcIsoString(this DateTime dt) => FormatUtc(dt);

    /// <summary>
    /// Deserializes JSON into <typeparamref name="T"/>, returning <see langword="default"/> when blank or malformed.
    /// </summary>
    public static T? SafeDeserialize<T>(string? json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, options ?? DefaultJsonOptions);
        }
        catch
        {
            return default;
        }
    }
}
