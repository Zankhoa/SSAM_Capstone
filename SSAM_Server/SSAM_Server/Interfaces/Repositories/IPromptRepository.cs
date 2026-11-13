using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IPromptRepository
{
    PromptTemplate GetPrompt(string documentType);
    void UpdatePrompt(PromptTemplate updatedTemplate);
}