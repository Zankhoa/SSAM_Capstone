using System.Text.Json.Serialization;

namespace SSAM.Application.Features.Workspaces.DTOs;

public class CreateWorkspaceRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("companyId")]
    public long? CompanyId { get; set; }
}

public class WorkspaceSummaryDto
{
    [JsonPropertyName("workspaceId")]
    public long WorkspaceId { get; set; }

    [JsonPropertyName("workspaceName")]
    public string WorkspaceName { get; set; } = string.Empty;

    [JsonPropertyName("companyId")]
    public long? CompanyId { get; set; }
}
