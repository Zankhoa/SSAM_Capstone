using System.Text.Json.Serialization;

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
