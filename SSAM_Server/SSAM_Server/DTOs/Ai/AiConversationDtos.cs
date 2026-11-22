namespace SSAM_Server.DTOs.Ai;

/// <summary>Summary of a saved AI conversation in a project.</summary>
public sealed record AiConversationSummaryDto(
    long Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A saved AI conversation together with its persisted messages.</summary>
public sealed record AiConversationDetailsDto(
    long Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AiConversationMessageDto> Messages);

/// <summary>A message saved as part of an AI conversation.</summary>
public sealed record AiConversationMessageDto(
    long Id,
    string Role,
    string Content,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AiCitationDto> Citations,
    string? Model,
    bool? IsRealAi);
