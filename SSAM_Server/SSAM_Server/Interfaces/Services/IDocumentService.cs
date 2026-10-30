using SSAM_Server.Common;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Documents;

namespace SSAM_Server.Services;

public interface IDocumentService
{
    Task<ServiceResult<ItemsDto<DocumentNodeDto>>> GetProjectDocumentsAsync(long projectId, string? search, CancellationToken ct = default);
    Task<ServiceResult<object>> CreateDocumentNodeAsync(long projectId, CreateDocumentNodeRequest request, CancellationToken ct = default);
    Task<ServiceResult<DocumentNodeDto>> UpdateDocumentNodeAsync(long documentId, UpdateDocumentNodeRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteDocumentNodeAsync(long documentId, CancellationToken ct = default);
    Task<ServiceResult<DocumentDetailDto>> GetDocumentDetailAsync(long documentId, long? versionId, CancellationToken ct = default);
    Task<ServiceResult<SaveContentResultDto>> SaveVersionContentAsync(long documentId, long versionId, SaveDocumentContentRequest request, CancellationToken ct = default);
    Task<ServiceResult<DocumentDetailDto>> SubmitVersionAsync(long documentId, long versionId, CancellationToken ct = default);
    Task<ServiceResult<DocumentDetailDto>> ApproveVersionAsync(long documentId, long versionId, ApproveDocumentRequest request, CancellationToken ct = default);
    Task<ServiceResult<DocumentDetailDto>> RequestChangesAsync(long documentId, long versionId, RequestChangesRequest request, CancellationToken ct = default);
    Task<ServiceResult<DocumentDetailDto>> CreateRevisionAsync(long documentId, CancellationToken ct = default);
    Task<ServiceResult<DocumentGenerationDto>> GetDocumentGenerationAsync(long documentId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<RequirementItemDto>>> GetDocumentRequirementsAsync(long documentId, long? versionId, CancellationToken ct = default);
    Task<ServiceResult> ConfirmRequirementFreshAsync(long documentId, string reqUid, CancellationToken ct = default);
    Task<ServiceResult<DocumentDiffDto>> GetDocumentDiffAsync(long documentId, long fromVersionId, long toVersionId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<DocumentCommentDto>>> GetDocumentCommentsAsync(long documentId, long? versionId, bool includeResolved, CancellationToken ct = default);
    Task<ServiceResult<DocumentCommentDto>> CreateDocumentCommentAsync(long documentId, CreateDocumentCommentRequest request, CancellationToken ct = default);
    Task<ServiceResult<DocumentCommentDto>> ResolveCommentAsync(long commentId, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<DocumentActivityDto>>> GetDocumentActivitiesAsync(long documentId, CancellationToken ct = default);
    Task<ServiceResult<NormalizedFieldsDto>> NormalizeNotesAsync(long projectId, NormalizeNotesRequest request, CancellationToken ct = default);
    Task<ServiceResult<ItemsDto<ExtractedRequirementDto>>> ExtractRequirementsAsync(long projectId, ExtractRequirementsRequest request, CancellationToken ct = default);
    Task<ServiceResult<DocumentDetailDto>> GenerateDocumentAsync(long projectId, GenerateDocumentRequest request, CancellationToken ct = default);
    Task<ServiceResult<DiagramProposalDto>> GenerateDiagramAsync(long projectId, GenerateDiagramRequest request, CancellationToken ct = default);
}
