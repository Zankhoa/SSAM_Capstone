using Microsoft.AspNetCore.Mvc;
using SSAM_Server.Services;
using SSAM_Server.Models;

namespace SSAM_Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocGenController : ControllerBase
{
    private readonly AiService _aiService;
    private readonly IPromptRepository _promptRepository;

    public DocGenController(AiService aiService, IPromptRepository promptRepository)
    {
        _aiService = aiService;
        _promptRepository = promptRepository;
    }

    /// <summary>
    /// Generates a structured SRS, URD, or BRD document from raw text input.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateDocument([FromBody] GenerateDocRequest request)
    {
        try
        {
            var markdownResult = await _aiService.GenerateDocumentAsync(request);
            return Ok(new { documentType = request.DocumentType, result = markdownResult });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            // Returns exact error message to Postman for debugging
            return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
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