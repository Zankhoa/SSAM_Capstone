using SSAM_Server.DTOs.Documents;

namespace SSAM_Server.Interfaces.Services;

public interface IAiService
{
    Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamTextAsync(string prompt, CancellationToken cancellationToken = default);
    Task<List<ExtractedRequirementDto>> GenerateRequirementDraftsAsync(
        NormalizedFieldsDto fields,
        string? rawNotes = null,
        CancellationToken cancellationToken = default);
}