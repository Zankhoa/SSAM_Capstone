using System.ComponentModel.DataAnnotations;

namespace SSAM_Server.DTOs.Ai;

public class AiChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public string? Timestamp { get; set; }
}

public class AiChatRequestDto
{
    [Required]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    public long? ProjectId { get; set; }

    public List<AiChatMessageDto>? History { get; set; }
}

public class AiToolCallRecordDto
{
    public string Name { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? Result { get; set; }
}

public class AiChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
    public List<AiToolCallRecordDto> UsedToolCalls { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    public bool IsRealAi { get; set; }
}

public class AiStatusDto
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public List<string> SupportedFeatures { get; set; } = new();
}
