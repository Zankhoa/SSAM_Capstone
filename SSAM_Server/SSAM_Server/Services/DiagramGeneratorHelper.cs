using System.Text;
using System.Text.RegularExpressions;
using SSAM_Server.DTOs.Documents;

namespace SSAM_Server.Services;

public static partial class DiagramGeneratorHelper
{
    [GeneratedRegex(@"^[-*•+]\s*|\d+[.)]\s*")]
    private static partial Regex StepPrefixRegex();

    public static DiagramProposalDto GenerateDiagram(string text, string? preferredKind)
    {
        var cleanText = (text ?? string.Empty).Trim();
        var kind = NormalizeKind(preferredKind, cleanText);
        var steps = ExtractSteps(cleanText);

        if (steps.Count == 0)
        {
            steps.Add("Bắt đầu quy trình");
            steps.Add("Xử lý yêu cầu");
            steps.Add("Hoàn tất");
        }

        return BuildDiagram(steps, kind);
    }

    public static DiagramProposalDto? TryGenerateDiagram(string text, string? preferredKind)
    {
        var cleanText = (text ?? string.Empty).Trim();
        var steps = ExtractSteps(cleanText);
        if (steps.Count < 2)
        {
            return null;
        }

        return BuildDiagram(steps, NormalizeKind(preferredKind, cleanText));
    }

    private static List<string> ExtractSteps(string text) =>
        text
            .Split(new[] { '\r', '\n', '.', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => StepPrefixRegex().Replace(s.Trim(), "").Trim())
            .Where(s => s.Length >= 3)
            .Take(7)
            .ToList();

    private static DiagramProposalDto BuildDiagram(List<string> steps, string kind)
    {
        return kind switch
        {
            "sequence" => BuildSequence(steps),
            "state" => BuildState(steps),
            _ => BuildFlow(steps)
        };
    }

    private static string NormalizeKind(string? preferredKind, string text)
    {
        if (!string.IsNullOrWhiteSpace(preferredKind))
        {
            var upper = preferredKind.Trim().ToUpperInvariant();
            if (upper is "SEQUENCE" or "SEQ") return "sequence";
            if (upper is "STATE" or "ER") return "state";
            if (upper is "FLOW" or "FLOWCHART") return "flowchart";
        }

        var lower = text.ToLowerInvariant();
        if (lower.Contains("trạng thái") || lower.Contains("state") || lower.Contains("status") || lower.Contains("chuyển sang"))
            return "state";
        if (lower.Contains("api") || lower.Contains("gửi") || lower.Contains("phản hồi") || lower.Contains("request") || lower.Contains("client"))
            return "sequence";
        return "flowchart";
    }

    private static DiagramProposalDto BuildFlow(List<string> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        for (int i = 0; i < steps.Count; i++)
        {
            var nodeId = $"N{i + 1}";
            var label = EscapeLabel(steps[i]);
            sb.AppendLine($"    {nodeId}[\"{label}\"]");
            if (i > 0)
            {
                sb.AppendLine($"    N{i} --> {nodeId}");
            }
        }

        return new DiagramProposalDto
        {
            Kind = "flowchart",
            Mermaid = sb.ToString().TrimEnd(),
            Notes = new List<string>
            {
                $"Sơ đồ luồng (Flowchart) gồm {steps.Count} bước được trích xuất từ mô tả."
            }
        };
    }

    private static DiagramProposalDto BuildSequence(List<string> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("sequenceDiagram");
        sb.AppendLine("    actor User as Người dùng");
        sb.AppendLine("    participant System as Hệ thống");
        sb.AppendLine("    participant DB as Cơ sở dữ liệu");

        for (int i = 0; i < steps.Count; i++)
        {
            var label = EscapeLabel(steps[i]);
            if (i % 2 == 0)
            {
                sb.AppendLine($"    User->>System: {label}");
            }
            else
            {
                sb.AppendLine($"    System->>DB: {label}");
                sb.AppendLine("    DB-->>System: Kết quả");
            }
        }
        sb.AppendLine("    System-->>User: Phản hồi hoàn tất");

        return new DiagramProposalDto
        {
            Kind = "sequence",
            Mermaid = sb.ToString().TrimEnd(),
            Notes = new List<string>
            {
                "Sơ đồ tuần tự (Sequence Diagram) minh hoạ tương tác giữa Người dùng, Hệ thống và CSDL."
            }
        };
    }

    private static DiagramProposalDto BuildState(List<string> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("stateDiagram-v2");
        sb.AppendLine("    [*] --> S1");
        for (int i = 0; i < steps.Count; i++)
        {
            var id = $"S{i + 1}";
            var label = EscapeLabel(steps[i]);
            sb.AppendLine($"    {id} : {label}");
            if (i > 0)
            {
                sb.AppendLine($"    S{i} --> {id}");
            }
        }
        sb.AppendLine($"    S{steps.Count} --> [*]");

        return new DiagramProposalDto
        {
            Kind = "state",
            Mermaid = sb.ToString().TrimEnd(),
            Notes = new List<string>
            {
                $"Sơ đồ trạng thái gồm {steps.Count} trạng thái chuyển tiếp."
            }
        };
    }

    private static string EscapeLabel(string s)
    {
        var clean = s.Replace("\"", "'").Trim();
        return clean.Length > 60 ? clean[..60] + "..." : clean;
    }
}
