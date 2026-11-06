using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSAM_Server.Configurations;
using SSAM_Server.Interfaces;

namespace SSAM_Server.Services;

public class GeminiAiService : IAiService
{
    private readonly AiOptions _options;
    private readonly ILogger<GeminiAiService> _logger;

    public GeminiAiService(IOptions<AiOptions> options, ILogger<GeminiAiService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI service called with model: {Model}", _options.DefaultModel);
        
        // Placeholder for future LLM integration
        return Task.FromResult($"[AI Response Placeholder] Received prompt: {prompt}");
    }

    public async IAsyncEnumerable<string> StreamTextAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI streaming called with model: {Model}", _options.DefaultModel);
        
        yield return "[AI Chunk 1] ";
        await Task.Delay(100, cancellationToken);
        yield return "[AI Chunk 2]";
    }
}