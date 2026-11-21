using SSAM_Server.Common;
using SSAM_Server.DTOs.Designs;

namespace SSAM_Server.Interfaces.Services;

public interface IFigmaWatchService
{
    Task RegisterFileAsync(string figmaUrlOrFileKey, string? fileName = null, CancellationToken ct = default);
    Task RegisterFilesFromContentAsync(string? documentContent, CancellationToken ct = default);
    Task LinkTestCaseAsync(long testCaseId, string figmaUrlOrFileKey, CancellationToken ct = default);
    Task<FigmaCheckUpdatesResponse> CheckAllWatchedFilesAsync(CancellationToken ct = default);
    Task<ServiceResult<FigmaCheckUpdatesResponse>> HandleWebhookAsync(FigmaWebhookPayload payload, CancellationToken ct = default);
}
