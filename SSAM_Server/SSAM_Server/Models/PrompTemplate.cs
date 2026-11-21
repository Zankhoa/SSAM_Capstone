namespace SSAM_Server.Models;

public record GenerateDocRequest(
    long ProjectId,
    string InputText,          // Requirements overview, meeting transcript, or user story
    string DocumentType,       // "SRS", "URD", or "BRD"
    string? RawNote = null     // Additional instructions, constraints, or raw notes
);

public class PromptTemplate
{
    public string DocumentType { get; set; } = string.Empty;
    public string SystemInstruction { get; set; } = string.Empty;
    public string UserPromptFormat { get; set; } = string.Empty;
}