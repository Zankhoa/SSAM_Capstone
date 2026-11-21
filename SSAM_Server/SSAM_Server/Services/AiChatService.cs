using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SSAM_Server.Common;
using SSAM_Server.Configurations;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Ai;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class AiChatService : IAiChatService
{
    private const string FallbackModelName = "SSAM-AI-Assistant";

    private readonly AppDbContext _db;
    private readonly IChatClient _chatClient;
    private readonly ICurrentUser _currentUser;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly AiOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        AppDbContext db,
        IChatClient chatClient,
        ICurrentUser currentUser,
        IProjectAccessGuard accessGuard,
        IOptions<AiOptions> options,
        ILogger<AiChatService> logger)
    {
        _db = db;
        _chatClient = chatClient;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
        _options = options.Value;
        _logger = logger;
    }

    public AiStatusDto GetStatus()
    {
        var hasApiKey = !string.IsNullOrWhiteSpace(_options.ApiKey);
        return new AiStatusDto
        {
            Enabled = true,
            Provider = _options.Provider,
            Model = hasApiKey ? _options.DefaultModel : FallbackModelName,
            HasApiKey = hasApiKey,
            SupportedFeatures = new List<string>
            {
                "ai-chat-toolcall",
                "document-generation-normalize",
                "document-generation-extract",
                "document-ai-diagram",
                "testcases-ai-suggest"
            }
        };
    }

    public async Task<ServiceResult<AiChatResponseDto>> ChatAsync(
        long? routeProjectId,
        AiChatRequestDto request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<AiChatResponseDto>.Unauthorized("Your session is invalid or has expired.");
        }

        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(message))
        {
            return ServiceResult<AiChatResponseDto>.Validation(new Dictionary<string, string[]>
            {
                ["message"] = new[] { "Message must not be empty." }
            });
        }

        var projectId = routeProjectId ?? request.ProjectId;
        var usedTools = new List<AiToolCallRecordDto>();
        ProjectSnapshot? snapshot = null;

        if (projectId.HasValue)
        {
            var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId.Value, PermissionKeys.ProjectView, ct);
            if (!auth.IsSuccess) return auth.Cast<AiChatResponseDto>();

            snapshot = await LoadProjectSnapshotAsync(projectId.Value, ct);
            if (snapshot != null)
            {
                usedTools.Add(new AiToolCallRecordDto
                {
                    Name = "GetProjectSummary",
                    Arguments = JsonSerializer.Serialize(new { projectId = projectId.Value }),
                    Result = JsonSerializer.Serialize(new
                    {
                        projectName = snapshot.ProjectName,
                        projectKey = snapshot.ProjectKey,
                        totalIssues = snapshot.TotalIssues,
                        doneIssues = snapshot.DoneIssues,
                        inProgressIssues = snapshot.InProgressIssues,
                        todoIssues = snapshot.TodoIssues,
                        totalTestCases = snapshot.TotalTestCases,
                        totalDocuments = snapshot.TotalDocuments
                    })
                });

                if (snapshot.LatestPlanName != null)
                {
                    usedTools.Add(new AiToolCallRecordDto
                    {
                        Name = "GetProjectTestPlanSummary",
                        Arguments = JsonSerializer.Serialize(new { planId = snapshot.LatestPlanId }),
                        Result = JsonSerializer.Serialize(new
                        {
                            planName = snapshot.LatestPlanName,
                            status = snapshot.LatestPlanStatus,
                            passed = snapshot.PassedResults,
                            failed = snapshot.FailedResults,
                            untested = snapshot.UntestedResults
                        })
                    });
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            try
            {
                var messages = BuildChatMessages(message, request.History, snapshot);
                var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
                var replyText = response.Text?.Trim();
                if (!string.IsNullOrEmpty(replyText))
                {
                    return ServiceResult<AiChatResponseDto>.Ok(new AiChatResponseDto
                    {
                        Reply = replyText,
                        UsedToolCalls = usedTools,
                        Model = _options.DefaultModel,
                        IsRealAi = true
                    });
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "External AI chat call failed; falling back to structured assistant response.");
            }
        }

        return ServiceResult<AiChatResponseDto>.Ok(new AiChatResponseDto
        {
            Reply = BuildFallbackReply(message, snapshot),
            UsedToolCalls = usedTools,
            Model = FallbackModelName,
            IsRealAi = false
        });
    }

    private async Task<ProjectSnapshot?> LoadProjectSnapshotAsync(long projectId, CancellationToken ct)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Name, p.Key })
            .FirstOrDefaultAsync(ct);

        if (project == null) return null;

        var issueCategories = await _db.Issues
            .AsNoTracking()
            .Where(i => i.ProjectId == projectId)
            .Select(i => i.Status.Category)
            .ToListAsync(ct);

        var totalDocuments = await _db.DocumentNodes
            .AsNoTracking()
            .CountAsync(d => d.ProjectId == projectId && d.Kind == DocumentNodeKinds.Page, ct);

        var totalTestCases = await _db.TestCases
            .AsNoTracking()
            .CountAsync(t => t.ProjectId == projectId, ct);

        var latestPlan = await _db.TestPlans
            .AsNoTracking()
            .Where(tp => tp.ProjectId == projectId)
            .OrderByDescending(tp => tp.Id)
            .Select(tp => new { tp.Id, tp.Name, tp.Status })
            .FirstOrDefaultAsync(ct);

        int passed = 0, failed = 0, untested = 0;
        if (latestPlan != null)
        {
            var results = await _db.TestResults
                .AsNoTracking()
                .Where(r => r.TestCase.PlanId == latestPlan.Id)
                .Select(r => r.Result)
                .ToListAsync(ct);

            passed = results.Count(r => string.Equals(r, TestResultValues.Pass, StringComparison.OrdinalIgnoreCase));
            failed = results.Count(r => string.Equals(r, TestResultValues.Fail, StringComparison.OrdinalIgnoreCase));
            untested = results.Count(r => string.Equals(r, TestResultValues.Untest, StringComparison.OrdinalIgnoreCase));
        }

        return new ProjectSnapshot
        {
            ProjectName = project.Name,
            ProjectKey = project.Key,
            TotalIssues = issueCategories.Count,
            DoneIssues = issueCategories.Count(c => string.Equals(c, StatusCategories.Done, StringComparison.OrdinalIgnoreCase)),
            InProgressIssues = issueCategories.Count(c => string.Equals(c, StatusCategories.InProgress, StringComparison.OrdinalIgnoreCase)),
            TodoIssues = issueCategories.Count(c => string.Equals(c, StatusCategories.Todo, StringComparison.OrdinalIgnoreCase)),
            TotalDocuments = totalDocuments,
            TotalTestCases = totalTestCases,
            LatestPlanId = latestPlan?.Id,
            LatestPlanName = latestPlan?.Name,
            LatestPlanStatus = latestPlan?.Status,
            PassedResults = passed,
            FailedResults = failed,
            UntestedResults = untested
        };
    }

    private static List<ChatMessage> BuildChatMessages(
        string userMessage,
        List<AiChatMessageDto>? history,
        ProjectSnapshot? snapshot)
    {
        var systemPrompt =
            "You are the SSAM AI Assistant for a software project management platform (documents, requirements, Kanban issues, and test plans). " +
            "Answer concisely in the same language as the user (Vietnamese by default if unclear). Use only the provided project data when citing numbers.";

        if (snapshot != null)
        {
            systemPrompt +=
                $"\n\nCurrent Project Context ({snapshot.ProjectKey} - {snapshot.ProjectName}):\n" +
                $"- Issues: {snapshot.TotalIssues} total (Done: {snapshot.DoneIssues}, In Progress: {snapshot.InProgressIssues}, Todo: {snapshot.TodoIssues})\n" +
                $"- Documents (pages): {snapshot.TotalDocuments}\n" +
                $"- Test Cases: {snapshot.TotalTestCases}\n" +
                (snapshot.LatestPlanName != null
                    ? $"- Latest Test Plan: {snapshot.LatestPlanName} ({snapshot.LatestPlanStatus}) — Pass: {snapshot.PassedResults}, Fail: {snapshot.FailedResults}, Untested: {snapshot.UntestedResults}"
                    : "- No test plans yet.");
        }

        var messages = new List<ChatMessage> { new(ChatRole.System, systemPrompt) };

        if (history != null)
        {
            foreach (var item in history.TakeLast(10))
            {
                if (string.IsNullOrWhiteSpace(item.Content)) continue;
                var role = item.Role?.ToLowerInvariant() switch
                {
                    "assistant" or "model" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User
                };
                messages.Add(new ChatMessage(role, item.Content));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, userMessage));
        return messages;
    }

    private static string BuildFallbackReply(string message, ProjectSnapshot? snapshot)
    {
        if (snapshot != null)
        {
            var pct = snapshot.TotalIssues > 0
                ? (int)Math.Round(snapshot.DoneIssues * 100.0 / snapshot.TotalIssues)
                : 0;
            var testLine = snapshot.LatestPlanName != null
                ? $"- **Đợt kiểm thử gần nhất ({snapshot.LatestPlanName}):** Pass {snapshot.PassedResults}, Fail {snapshot.FailedResults}, Chưa chạy {snapshot.UntestedResults}"
                : "- **Kiểm thử:** Chưa có đợt kiểm thử nào.";

            return $"""
                ### 📊 Tổng quan dự án {snapshot.ProjectName} (`{snapshot.ProjectKey}`)

                - **Công việc (Issues):** {snapshot.TotalIssues} tổng cộng ({snapshot.DoneIssues} Done, {snapshot.InProgressIssues} In Progress, {snapshot.TodoIssues} Todo — hoàn thành ~**{pct}%**)
                - **Tài liệu:** {snapshot.TotalDocuments} trang tài liệu
                - **Test cases:** {snapshot.TotalTestCases} test cases
                {testLine}
                """;
        }

        return """
            ### 🤖 Trợ lý AI SSAM

            Tôi có thể hỗ trợ bạn tra cứu tiến độ dự án, tài liệu đặc tả, bảng Kanban và kế hoạch kiểm thử. Hãy mở một dự án cụ thể hoặc đặt câu hỏi về tài liệu, sơ đồ, hay test case để tôi hỗ trợ chi tiết hơn.
            """;
    }

    private sealed class ProjectSnapshot
    {
        public string ProjectName { get; init; } = string.Empty;
        public string ProjectKey { get; init; } = string.Empty;
        public int TotalIssues { get; init; }
        public int DoneIssues { get; init; }
        public int InProgressIssues { get; init; }
        public int TodoIssues { get; init; }
        public int TotalDocuments { get; init; }
        public int TotalTestCases { get; init; }
        public long? LatestPlanId { get; init; }
        public string? LatestPlanName { get; init; }
        public string? LatestPlanStatus { get; init; }
        public int PassedResults { get; init; }
        public int FailedResults { get; init; }
        public int UntestedResults { get; init; }
    }
}
