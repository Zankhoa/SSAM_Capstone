using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSAM_Server.Configurations;
using SSAM_Server.Interfaces.Services;
using Google.GenAI;
using Google.GenAI.Types;
using GeminiType = Google.GenAI.Types.Type;

namespace SSAM_Server.Services;

public class GeminiAiService : IAiService
{
    private readonly AiOptions _options;
    private readonly ILogger<GeminiAiService> _logger;
    private readonly ISqlServerDatabaseService _dbService;

    public GeminiAiService(IOptions<AiOptions> options, ILogger<GeminiAiService> logger, ISqlServerDatabaseService dbService)
    {
        _options = options.Value;
        _logger = logger;
        _dbService = dbService;
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

    public async Task<string> AskDatabaseAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        var client = new Client(apiKey: _options.ApiKey);

        // 1. Fetch current DB schema
        string schema = await _dbService.GetDatabaseSchemaAsync();

        // 2. Define function declaration tool for Gemini
        var sqlTool = new Tool
        {
            FunctionDeclarations = new List<FunctionDeclaration>
            {
                new FunctionDeclaration
                {
                    Name = "ExecuteSqlQuery",
                    Description = "Executes a read-only T-SQL SELECT query against the SQL Server database and returns results.",
                    Parameters = new Schema
                    {
                        Type = GeminiType.Object,
                        Properties = new Dictionary<string, Schema>
                        {
                            ["query"] = new Schema
                            {
                                Type = GeminiType.String,
                                Description = "The T-SQL query string to execute. Example: SELECT TOP 10 * FROM Orders;"
                            }
                        },
                        Required = new List<string> { "query" }
                    }
                }
            }
        };

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = new List<Part> 
                { 
                    new Part { Text = $"You are a SQL Server assistant. Here is the database schema:\n{schema}" } 
                }
            },
            Tools = new List<Tool> { sqlTool }
        };

        // 3. Initial call to Gemini
        var response = await client.Models.GenerateContentAsync(
            model: _options.DefaultModel,
            contents: userPrompt,
            config: config
        );

        // 4. Check if Gemini requested to run a database query
        var functionCall = response.FunctionCalls?.FirstOrDefault();
        if (functionCall != null && functionCall.Name == "ExecuteSqlQuery")
        {
            string generatedSql = functionCall.Args["query"].ToString();

            // Run query on SQL Server safely
            string dbResultJson = await _dbService.ExecuteReadOnlyQueryAsync(generatedSql);

            // Send query result back to Gemini for final natural language synthesis
            var finalResponse = await client.Models.GenerateContentAsync(
                model: _options.DefaultModel,
                contents: new List<Content>
                {
                    new Content { Role = "user", Parts = new List<Part> { new Part { Text = userPrompt } } },
                    new Content { Role = "model", Parts = new List<Part> { new Part { FunctionCall = functionCall } } },
                    new Content { Role = "user", Parts = new List<Part> { new Part { FunctionResponse = new FunctionResponse { Name = "ExecuteSqlQuery", Response = new Dictionary<string, object> { ["result"] = dbResultJson } } } } }
                }
            );

            return finalResponse.Text;
        }

        return response.Text;
    }
}