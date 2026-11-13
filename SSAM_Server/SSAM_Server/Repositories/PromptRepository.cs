using SSAM_Server.Models;
using SSAM_Server.Interfaces.Repositories;

namespace SSAM_Server.Repositories;

public class PromptRepository : IPromptRepository
{
    // In production, this can read from SQL Server or a database.
    private readonly Dictionary<string, PromptTemplate> _prompts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SRS"] = new PromptTemplate
        {
            DocumentType = "SRS",
            SystemInstruction = @"You are a Lead Software Architect. Generate a formal Software Requirements Specification (SRS) in Markdown format. 
Include sections: 1. Introduction, 2. Overall Description (User Classes, Operating Environment), 3. System Features & Functional Requirements, 4. Non-Functional Requirements (Performance, Security).",
            UserPromptFormat = "Generate a full SRS document based on the following input:\n\n{0}"
        },
        ["URD"] = new PromptTemplate
        {
            DocumentType = "URD",
            SystemInstruction = @"You are a UX Business Analyst. Generate a User Requirements Document (URD) in Markdown. 
Include sections: 1. User Profiles & Personas, 2. User Goals, 3. High-Level User Scenarios & Workflows, 4. Usability Requirements.",
            UserPromptFormat = "Generate a comprehensive URD based on the following raw project input:\n\n{0}"
        },
        ["BRD"] = new PromptTemplate
        {
            DocumentType = "BRD",
            SystemInstruction = @"You are a Senior Product Manager. Generate a Business Requirements Document (BRD) in Markdown. 
Include sections: 1. Executive Summary, 2. Business Objectives & Success Metrics (KPIs), 3. Project Scope (In-Scope/Out-of-Scope), 4. Financial & Operational Assumptions.",
            UserPromptFormat = "Generate an executive-level BRD based on the following project context:\n\n{0}"
        }
    };

    public PromptTemplate GetPrompt(string documentType)
    {
        if (_prompts.TryGetValue(documentType, out var template))
        {
            return template;
        }

        throw new ArgumentException($"Unsupported document type: {documentType}");
    }

    public void UpdatePrompt(PromptTemplate updatedTemplate)
    {
        _prompts[updatedTemplate.DocumentType] = updatedTemplate;
    }
}