using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents)
    {
        _documents = documents;
    }

    // #1 GET /projects/{projectId}/documents?search=
    [HttpGet("projects/{projectId:long}/documents")]
    public async Task<IActionResult> GetProjectDocuments(
        long projectId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _documents.GetProjectDocumentsAsync(projectId, search, ct);
        return result.ToActionResult(this);
    }

    // #2 POST /projects/{projectId}/documents
    [HttpPost("projects/{projectId:long}/documents")]
    public async Task<IActionResult> CreateDocumentNode(
        long projectId,
        [FromBody] CreateDocumentNodeRequest request,
        CancellationToken ct)
    {
        var result = await _documents.CreateDocumentNodeAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #3 PATCH /documents/{documentId}
    [HttpPatch("documents/{documentId:long}")]
    public async Task<IActionResult> UpdateDocumentNode(
        long documentId,
        [FromBody] UpdateDocumentNodeRequest request,
        CancellationToken ct)
    {
        var result = await _documents.UpdateDocumentNodeAsync(documentId, request, ct);
        return result.ToActionResult(this);
    }

    // #4 DELETE /documents/{documentId}
    [HttpDelete("documents/{documentId:long}")]
    public async Task<IActionResult> DeleteDocumentNode(
        long documentId,
        CancellationToken ct)
    {
        var result = await _documents.DeleteDocumentNodeAsync(documentId, ct);
        return result.ToActionResult(this);
    }

    // #5 GET /documents/{documentId}?versionId=
    [HttpGet("documents/{documentId:long}")]
    public async Task<IActionResult> GetDocumentDetail(
        long documentId,
        [FromQuery] long? versionId,
        CancellationToken ct)
    {
        var result = await _documents.GetDocumentDetailAsync(documentId, versionId, ct);
        return result.ToActionResult(this);
    }

    // #6 PUT /documents/{documentId}/versions/{versionId}/content
    [HttpPut("documents/{documentId:long}/versions/{versionId:long}/content")]
    public async Task<IActionResult> SaveVersionContent(
        long documentId,
        long versionId,
        [FromBody] SaveDocumentContentRequest request,
        CancellationToken ct)
    {
        var result = await _documents.SaveVersionContentAsync(documentId, versionId, request, ct);
        return result.ToActionResult(this);
    }

    // #7 POST /documents/{documentId}/versions/{versionId}/submit
    [HttpPost("documents/{documentId:long}/versions/{versionId:long}/submit")]
    public async Task<IActionResult> SubmitVersion(
        long documentId,
        long versionId,
        CancellationToken ct)
    {
        var result = await _documents.SubmitVersionAsync(documentId, versionId, ct);
        return result.ToActionResult(this);
    }

    // #8 POST /documents/{documentId}/versions/{versionId}/approve
    [HttpPost("documents/{documentId:long}/versions/{versionId:long}/approve")]
    public async Task<IActionResult> ApproveVersion(
        long documentId,
        long versionId,
        [FromBody] ApproveDocumentRequest? request,
        CancellationToken ct)
    {
        var result = await _documents.ApproveVersionAsync(documentId, versionId, request ?? new ApproveDocumentRequest(), ct);
        return result.ToActionResult(this);
    }

    // #9 POST /documents/{documentId}/versions/{versionId}/request-changes
    [HttpPost("documents/{documentId:long}/versions/{versionId:long}/request-changes")]
    public async Task<IActionResult> RequestChanges(
        long documentId,
        long versionId,
        [FromBody] RequestChangesRequest request,
        CancellationToken ct)
    {
        var result = await _documents.RequestChangesAsync(documentId, versionId, request, ct);
        return result.ToActionResult(this);
    }

    // #10 POST /documents/{documentId}/revisions
    [HttpPost("documents/{documentId:long}/revisions")]
    public async Task<IActionResult> CreateRevision(
        long documentId,
        [FromBody] CreateRevisionRequest? request,
        CancellationToken ct)
    {
        var result = await _documents.CreateRevisionAsync(documentId, request, ct);
        return result.ToActionResult(this);
    }

    // #11 GET /documents/{documentId}/generation
    [HttpGet("documents/{documentId:long}/generation")]
    public async Task<IActionResult> GetDocumentGeneration(
        long documentId,
        CancellationToken ct)
    {
        var result = await _documents.GetDocumentGenerationAsync(documentId, ct);
        return result.ToActionResult(this);
    }

    // T10 GET /documents/{documentId}/sections?versionId=
    [HttpGet("documents/{documentId:long}/sections")]
    public async Task<IActionResult> GetDocumentSections(
        long documentId,
        [FromQuery] long? versionId,
        CancellationToken ct)
    {
        var result = await _documents.GetDocumentSectionsAsync(documentId, versionId, ct);
        return result.ToActionResult(this);
    }

    // #12 GET /documents/{documentId}/requirements?versionId=
    [HttpGet("documents/{documentId:long}/requirements")]
    public async Task<IActionResult> GetDocumentRequirements(
        long documentId,
        [FromQuery] long? versionId,
        CancellationToken ct)
    {
        var result = await _documents.GetDocumentRequirementsAsync(documentId, versionId, ct);
        return result.ToActionResult(this);
    }

    // #12b PATCH /requirements/{requirementId}
    [HttpPatch("requirements/{requirementId:long}")]
    public async Task<IActionResult> UpdateRequirement(
        long requirementId,
        [FromBody] UpdateRequirementRequest request,
        CancellationToken ct)
    {
        var result = await _documents.UpdateRequirementAsync(requirementId, request, ct);
        return result.ToActionResult(this);
    }

    // #13a POST /requirements/{requirementId}/confirm-fresh
    [HttpPost("requirements/{requirementId:long}/confirm-fresh")]
    public async Task<IActionResult> ConfirmRequirementFreshById(
        long requirementId,
        CancellationToken ct)
    {
        var result = await _documents.ConfirmRequirementFreshByIdAsync(requirementId, ct);
        return result.ToActionResult(this);
    }

    // #13 POST /documents/{documentId}/requirements/{reqUid}/confirm-fresh
    [HttpPost("documents/{documentId:long}/requirements/{reqUid}/confirm-fresh")]
    public async Task<IActionResult> ConfirmRequirementFresh(
        long documentId,
        string reqUid,
        CancellationToken ct)
    {
        var result = await _documents.ConfirmRequirementFreshAsync(documentId, reqUid, ct);
        return result.ToActionResult(this);
    }

    // #14 GET /documents/{documentId}/diff?fromVersionId=&toVersionId=
    [HttpGet("documents/{documentId:long}/diff")]
    public async Task<IActionResult> GetDocumentDiff(
        long documentId,
        [FromQuery] long fromVersionId,
        [FromQuery] long toVersionId,
        CancellationToken ct)
    {
        var result = await _documents.GetDocumentDiffAsync(documentId, fromVersionId, toVersionId, ct);
        return result.ToActionResult(this);
    }

    // #15 GET /documents/{documentId}/comments?versionId=&includeResolved=false
    [HttpGet("documents/{documentId:long}/comments")]
    public async Task<IActionResult> GetDocumentComments(
        long documentId,
        [FromQuery] long? versionId,
        [FromQuery] bool includeResolved = false,
        CancellationToken ct = default)
    {
        var result = await _documents.GetDocumentCommentsAsync(documentId, versionId, includeResolved, ct);
        return result.ToActionResult(this);
    }

    // #16 POST /documents/{documentId}/comments
    [HttpPost("documents/{documentId:long}/comments")]
    public async Task<IActionResult> CreateDocumentComment(
        long documentId,
        [FromBody] CreateDocumentCommentRequest request,
        CancellationToken ct)
    {
        var result = await _documents.CreateDocumentCommentAsync(documentId, request, ct);
        return result.ToActionResult(this);
    }

    // #17 POST /comments/{commentId}/resolve
    [HttpPost("comments/{commentId:long}/resolve")]
    public async Task<IActionResult> ResolveComment(
        long commentId,
        CancellationToken ct)
    {
        var result = await _documents.ResolveCommentAsync(commentId, ct);
        return result.ToActionResult(this);
    }

    // #18 GET /documents/{documentId}/activities (and alias /activity)
    [HttpGet("documents/{documentId:long}/activities")]
    [HttpGet("documents/{documentId:long}/activity")]
    public async Task<IActionResult> GetDocumentActivities(
        long documentId,
        CancellationToken ct)
    {
        var result = await _documents.GetDocumentActivitiesAsync(documentId, ct);
        return result.ToActionResult(this);
    }

    // #19 POST /projects/{projectId}/documents/generator/normalize (and alias document-generation/normalize)
    [HttpPost("projects/{projectId:long}/documents/generator/normalize")]
    [HttpPost("projects/{projectId:long}/document-generation/normalize")]
    public async Task<IActionResult> NormalizeNotes(
        long projectId,
        [FromBody] NormalizeNotesRequest request,
        CancellationToken ct)
    {
        var result = await _documents.NormalizeNotesAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #20 POST /projects/{projectId}/documents/generator/extract (and alias document-generation/extract)
    [HttpPost("projects/{projectId:long}/documents/generator/extract")]
    [HttpPost("projects/{projectId:long}/document-generation/extract")]
    public async Task<IActionResult> ExtractRequirements(
        long projectId,
        [FromBody] ExtractRequirementsRequest request,
        CancellationToken ct)
    {
        var result = await _documents.ExtractRequirementsAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Creates a draft document from reviewed AI candidates. Every item must be accepted or rejected;
    /// only accepted items are inserted into the document.
    /// </summary>
    // #21 POST /projects/{projectId}/documents/generator/create (and alias document-generation/create)
    [HttpPost("projects/{projectId:long}/documents/generator/create")]
    [HttpPost("projects/{projectId:long}/document-generation/create")]
    public async Task<IActionResult> GenerateDocument(
        long projectId,
        [FromBody] GenerateDocumentRequest request,
        CancellationToken ct)
    {
        var result = await _documents.GenerateDocumentAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    // #22 POST /projects/{projectId}/diagrams/generate (and alias document-ai/diagram)
    [HttpPost("projects/{projectId:long}/diagrams/generate")]
    [HttpPost("projects/{projectId:long}/document-ai/diagram")]
    public async Task<IActionResult> GenerateDiagram(
        long projectId,
        [FromBody] GenerateDiagramRequest request,
        CancellationToken ct)
    {
        var result = await _documents.GenerateDiagramAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }
}
