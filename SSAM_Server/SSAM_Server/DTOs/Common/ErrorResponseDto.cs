using System.Text.Json.Serialization;
using SSAM_Server.Security;

namespace SSAM_Server.DTOs.Common;

public class ErrorResponseDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }

    [JsonPropertyName("requiredPermission")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiredPermission { get; set; }

    [JsonPropertyName("workflowIssues")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? WorkflowIssues { get; set; }

    [JsonPropertyName("current")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Current { get; set; }

    [JsonPropertyName("unmetCriteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? UnmetCriteria { get; set; }

    [JsonPropertyName("traceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }

    public ErrorResponseDto() { }

    public ErrorResponseDto(
        string code,
        string message,
        Dictionary<string, string[]>? errors = null,
        string? traceId = null,
        string? requiredPermission = null,
        object? workflowIssues = null,
        object? current = null,
        object? unmetCriteria = null)
    {
        Code = code;
        Message = message;
        Errors = errors;
        TraceId = traceId;
        RequiredPermission = requiredPermission;
        WorkflowIssues = workflowIssues;
        Current = current;
        UnmetCriteria = unmetCriteria;
    }
}

public class VersionConflictCurrentDto
{
    [JsonPropertyName("rowVersion")]
    public int RowVersion { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedBy")]
    public UserLiteDto UpdatedBy { get; set; } = new();
}

public class ItemsDto<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    public ItemsDto() { }

    public ItemsDto(IEnumerable<T> items)
    {
        Items = items.ToList();
    }
}

public class PageDto<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class RoleRefDto
{
    [JsonPropertyName("roleId")]
    public long RoleId { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = RoleKinds.Other;
}

public class UserRefDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }
}

public class UserLiteDto
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;
}
