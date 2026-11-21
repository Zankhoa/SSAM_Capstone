using SSAM_Server.Common;
using SSAM_Server.DTOs.Designs;

namespace SSAM_Server.Interfaces.Services;

public record ResolvedFigmaImage(string Name, string? ThumbnailUrl, byte[] ImageBytes, string MimeType);

public interface IFigmaService
{
    Task<ServiceResult<ResolveDesignResponse>> ResolveAsync(ResolveDesignRequest request, CancellationToken ct = default);
    Task<ResolvedFigmaImage?> FetchNodeImageForSuggestAsync(string url, CancellationToken ct = default);
}
