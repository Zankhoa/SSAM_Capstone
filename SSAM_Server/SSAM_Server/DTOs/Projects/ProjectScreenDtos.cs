using System.Text.Json.Serialization;
using SSAM_Server.DTOs.Common;

namespace SSAM_Server.DTOs.Projects;

public class ProjectScreenDto
{
    [JsonPropertyName("screenId")]
    public long ScreenId { get; set; }

    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "SCREEN";

    [JsonPropertyName("figmaUrl")]
    public string? FigmaUrl { get; set; }

    [JsonPropertyName("hasImage")]
    public bool HasImage { get; set; }

    [JsonPropertyName("imageFileName")]
    public string? ImageFileName { get; set; }

    [JsonPropertyName("documentId")]
    public long? DocumentId { get; set; }

    [JsonPropertyName("documentTitle")]
    public string? DocumentTitle { get; set; }

    [JsonPropertyName("reqUids")]
    public List<string> ReqUids { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("createdBy")]
    public UserRefDto CreatedBy { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public class UpsertProjectScreenForm
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "SCREEN";

    [JsonPropertyName("figmaUrl")]
    public string? FigmaUrl { get; set; }

    [JsonPropertyName("documentId")]
    public long? DocumentId { get; set; }

    public string? ReqUidsJson { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    public IFormFile? Image { get; set; }

    public bool RemoveImage { get; set; }
}

public class GenerateLayoutRequestDto
{
    public long DocumentId { get; set; }

    public List<long>? DocumentIds { get; set; }

    public string? DocumentIdsJson { get; set; }

    public bool? GenerateFlow { get; set; }

    public int? MaxScreens { get; set; }

    public List<string>? ReqUids { get; set; }

    public string? ReqUidsJson { get; set; }

    public string? Title { get; set; }

    public string? Notes { get; set; }

    public IFormFile? ReferenceImage { get; set; }
}

public class GenerateLayoutItemDto
{
    [JsonPropertyName("screen")]
    public ProjectScreenDto Screen { get; set; } = new();

    [JsonPropertyName("svgContent")]
    public string SvgContent { get; set; } = string.Empty;

    [JsonPropertyName("usedAi")]
    public bool UsedAi { get; set; }

    [JsonPropertyName("stepOrder")]
    public int StepOrder { get; set; } = 1;

    [JsonPropertyName("workflowStep")]
    public string? WorkflowStep { get; set; }
}

public class GenerateLayoutResponseDto
{
    [JsonPropertyName("screen")]
    public ProjectScreenDto Screen { get; set; } = new();

    [JsonPropertyName("svgContent")]
    public string SvgContent { get; set; } = string.Empty;

    [JsonPropertyName("usedAi")]
    public bool UsedAi { get; set; }

    [JsonPropertyName("items")]
    public List<GenerateLayoutItemDto> Items { get; set; } = new();
}
