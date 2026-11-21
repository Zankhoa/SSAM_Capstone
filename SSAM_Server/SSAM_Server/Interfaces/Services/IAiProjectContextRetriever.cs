using SSAM_Server.DTOs.Ai;

namespace SSAM_Server.Interfaces.Services;

public interface IAiProjectContextRetriever
{
    Task<IReadOnlyList<AiRetrievedSourceDto>> RetrieveAsync(
        long projectId,
        string query,
        bool canViewDocuments,
        bool canViewIssues,
        bool canViewTests,
        long? documentId = null,
        CancellationToken ct = default);
}
