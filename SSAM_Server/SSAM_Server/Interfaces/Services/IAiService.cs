using SSAM_Server.DTOs.Documents;
using SSAM_Server.DTOs.Testing;

namespace SSAM_Server.Interfaces.Services;

public record TestCaseDesignInput(int DesignIndex, int RequirementIndex, string Name, byte[] ImageBytes, string MimeType);

public interface IAiService
{
    Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamTextAsync(string prompt, CancellationToken cancellationToken = default);
    Task<List<ExtractedRequirementDto>> GenerateRequirementDraftsAsync(
        NormalizedFieldsDto fields,
        string? rawNotes = null,
        CancellationToken cancellationToken = default);
    Task<List<AiGeneratedTestCaseDraftDto>> GenerateTestCaseDraftsAsync(
        IReadOnlyList<TestCaseGenerationContextDto> requirements,
        CancellationToken cancellationToken = default);
    Task<List<AiGeneratedTestCaseDraftDto>> GenerateTestCaseDraftsAsync(
        IReadOnlyList<TestCaseGenerationContextDto> requirements,
        IReadOnlyList<TestCaseDesignInput> designs,
        CancellationToken cancellationToken = default);
}