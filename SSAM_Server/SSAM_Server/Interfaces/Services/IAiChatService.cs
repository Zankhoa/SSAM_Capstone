using SSAM_Server.Common;
using SSAM_Server.DTOs.Ai;

namespace SSAM_Server.Interfaces.Services;

public interface IAiChatService
{
    AiStatusDto GetStatus();
    Task<ServiceResult<AiChatResponseDto>> ChatAsync(long? routeProjectId, AiChatRequestDto request, CancellationToken ct = default);
}
