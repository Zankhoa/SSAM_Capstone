namespace SSAM_Server.Common;

public class ServiceResultDto
{
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    public string? RequiredPermission { get; set; }
    public object? WorkflowIssues { get; set; }
    public object? Current { get; set; }
    public object? UnmetCriteria { get; set; }
}
