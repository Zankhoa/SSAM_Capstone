using SSAM_Server.Common;
using SSAM_Server.DTOs.Ai;

namespace SSAM_Server.Interfaces.Services;

public interface IAiChatService
{
    AiStatusDto GetStatus();
    Task<ServiceResult<AiChatResponseDto>> ChatAsync(long? routeProjectId, AiChatRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<AiConversationSummaryDto>>> ListConversationsAsync(
        long projectId,
        long? documentId = null,
        CancellationToken ct = default);
    Task<ServiceResult<AiConversationDetailsDto>> GetConversationAsync(
        long projectId,
        long conversationId,
        long? documentId = null,
        CancellationToken ct = default);
}
