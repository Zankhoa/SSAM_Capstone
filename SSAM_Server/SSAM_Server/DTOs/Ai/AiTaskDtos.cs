using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SSAM_Server.DTOs.Ai;

/// <summary>Input for running an allow-listed AI task within a project.</summary>
public class AiTaskRequestDto
{
    [Required]
    [MaxLength(40)]
    public string Task { get; set; } = string.Empty;

    [Required]
    public JsonElement Input { get; set; }
}

/// <summary>Structured JSON output returned by an AI task.</summary>
public class AiTaskResponseDto
{
    public JsonElement Output { get; set; }
}
