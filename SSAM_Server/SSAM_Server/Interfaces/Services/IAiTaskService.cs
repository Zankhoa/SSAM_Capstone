using SSAM_Server.Common;
using SSAM_Server.DTOs.Ai;

namespace SSAM_Server.Interfaces.Services;

public interface IAiTaskService
{
    Task<ServiceResult<AiTaskResponseDto>> RunAsync(
        long projectId,
        AiTaskRequestDto request,
        CancellationToken ct = default);
}
