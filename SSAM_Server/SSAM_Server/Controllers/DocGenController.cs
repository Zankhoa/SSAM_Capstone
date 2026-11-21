using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Models;
using SSAM_Server.Security;
using SSAM_Server.Services;

namespace SSAM_Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocGenController : ControllerBase
{
    private readonly AiService _aiService;
    private readonly IPromptRepository _promptRepository;
    private readonly AppDbContext _db;
    private readonly IProjectAccessGuard _accessGuard;

    public DocGenController(
        AiService aiService,
        IPromptRepository promptRepository,
        AppDbContext db,
        IProjectAccessGuard accessGuard)
    {
        _aiService = aiService;
        _promptRepository = promptRepository;
        _db = db;
        _accessGuard = accessGuard;
    }

    /// <summary>
    /// Generates a structured SRS, URD, or BRD document from raw text input.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateDocument([FromBody] GenerateDocRequest request, CancellationToken ct)
    {
        var documentAccess = await _accessGuard.EnsureProjectPermissionAsync(
            request.ProjectId, PermissionKeys.DocumentCreate, ct);
        if (!documentAccess.IsSuccess) return documentAccess.ToActionResult(this);

        var aiAccess = await _accessGuard.EnsureProjectPermissionAsync(
            request.ProjectId, PermissionKeys.AiUse, ct);
        if (!aiAccess.IsSuccess) return aiAccess.ToActionResult(this);

        var aiEnabled = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == request.ProjectId)
            .Select(p => (bool?)p.AiEnabled)
            .SingleOrDefaultAsync(ct);
        if (aiEnabled != true)
        {
            return Conflict(new ErrorResponseDto(
                "AI_DISABLED",
                "AI features are disabled for this project.",
                traceId: HttpContext.TraceIdentifier));
        }

        try
        {
            var markdownResult = await _aiService.GenerateDocumentAsync(request, ct);
            return Ok(new { documentType = request.DocumentType, result = markdownResult });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponseDto(
                "VALIDATION_ERROR",
                ex.Message,
                traceId: HttpContext.TraceIdentifier));
        }
    }

    /// <summary>
    /// Fetch the prompt template for a document type to inspect in the backend.
    /// </summary>
    [HttpGet("prompts/{docType}")]
    public IActionResult GetPromptTemplate(string docType)
    {
        var prompt = _promptRepository.GetPrompt(docType);
        return Ok(prompt);
    }

    /// <summary>
    /// Edit the system instructions or prompt template for a document type.
    /// </summary>
    [HttpPut("prompts")]
    public IActionResult UpdatePromptTemplate([FromBody] PromptTemplate template)
    {
        _promptRepository.UpdatePrompt(template);
        return Ok(new { message = $"Prompt for {template.DocumentType} updated successfully." });
    }
}