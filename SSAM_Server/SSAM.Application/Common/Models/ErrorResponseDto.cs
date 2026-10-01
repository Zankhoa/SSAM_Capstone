using System.Text.Json.Serialization;

namespace SSAM.Application.Common.Models;

public class ErrorResponseDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }

    public ErrorResponseDto() { }

    public ErrorResponseDto(string code, string message, Dictionary<string, string[]>? errors = null)
    {
        Code = code;
        Message = message;
        Errors = errors;
    }
}
