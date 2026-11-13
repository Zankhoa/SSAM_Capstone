using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SSAM_Server.DTOs.Documents;

namespace SSAM_Server.Services;

public static partial class DocumentGeneratorHelper
{
    [GeneratedRegex(@"^[-*•+]\s*|\d+[.)]\s*")]
    private static partial Regex BulletPrefixRegex();

    public static NormalizedFieldsDto NormalizeNotes(string rawNotes)
    {
        var lines = (rawNotes ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var scopeParts = new List<string>();
        var actors = new List<string>();
        var mainFlows = new List<string>();
        var businessRules = new List<string>();

        string currentSection = "scope";

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower.StartsWith("phạm vi") || lower.StartsWith("scope") || lower.StartsWith("mục tiêu") || lower.StartsWith("bối cảnh"))
            {
                currentSection = "scope";
                var afterColon = AfterColon(line);
                if (!string.IsNullOrWhiteSpace(afterColon)) scopeParts.Add(afterColon);
                continue;
            }
            if (lower.StartsWith("tác nhân") || lower.StartsWith("actor") || lower.StartsWith("người dùng") || lower.StartsWith("vai trò"))
            {
                currentSection = "actors";
                var afterColon = AfterColon(line);
                if (!string.IsNullOrWhiteSpace(afterColon))
                {
                    foreach (var a in afterColon.Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var clean = a.Trim();
                        if (!string.IsNullOrEmpty(clean)) actors.Add(clean);
                    }
                }
                continue;
            }
            if (lower.StartsWith("luồng") || lower.StartsWith("flow") || lower.StartsWith("chức năng") || lower.StartsWith("tính năng"))
            {
                currentSection = "flows";
                var afterColon = AfterColon(line);
                if (!string.IsNullOrWhiteSpace(afterColon)) mainFlows.Add(afterColon);
                continue;
            }
            if (lower.StartsWith("quy tắc") || lower.StartsWith("rule") || lower.StartsWith("ràng buộc") || lower.StartsWith("điều kiện"))
            {
                currentSection = "rules";
                var afterColon = AfterColon(line);
                if (!string.IsNullOrWhiteSpace(afterColon)) businessRules.Add(afterColon);
                continue;
            }

            var cleaned = BulletPrefixRegex().Replace(line, "").Trim();
            if (string.IsNullOrEmpty(cleaned)) continue;

            switch (currentSection)
            {
                case "actors":
                    actors.Add(cleaned);
                    break;
                case "flows":
                    mainFlows.Add(cleaned);
                    break;
                case "rules":
                    businessRules.Add(cleaned);
                    break;
                default:
                    if (line.StartsWith('-') || line.StartsWith('*') || line.StartsWith('•') || char.IsDigit(line[0]))
                    {
                        mainFlows.Add(cleaned);
                    }
                    else
                    {
                        scopeParts.Add(cleaned);
                    }
                    break;
            }
        }

        if (scopeParts.Count == 0 && lines.Count > 0)
        {
            scopeParts.Add(BulletPrefixRegex().Replace(lines[0], "").Trim());
        }

        if (actors.Count == 0)
        {
            actors.Add("Người dùng (User)");
            actors.Add("Quản trị viên (Admin)");
        }

        if (mainFlows.Count == 0)
        {
            foreach (var l in lines.Skip(1).Take(4))
            {
                var c = BulletPrefixRegex().Replace(l, "").Trim();
                if (!string.IsNullOrEmpty(c)) mainFlows.Add(c);
            }
            if (mainFlows.Count == 0)
            {
                mainFlows.Add("Xử lý luồng nghiệp vụ chính theo phạm vi tài liệu");
            }
        }

        if (businessRules.Count == 0)
        {
            businessRules.Add("Kiểm tra phân quyền và tính hợp lệ của dữ liệu đầu vào trước khi lưu.");
            businessRules.Add("Ghi nhận lịch sử thao tác (audit log) cho các thay đổi quan trọng.");
        }

        return new NormalizedFieldsDto
        {
            Scope = string.Join(" ", scopeParts),
            Actors = string.Join(", ", actors.Distinct()),
            MainFlows = string.Join("\n", mainFlows.Select(f => $"• {f}")),
            BusinessRules = string.Join("\n", businessRules.Select(r => $"• {r}"))
        };
    }

    private static List<string> SplitFieldLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        return text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => BulletPrefixRegex().Replace(l.Trim(), "").Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }

    public static List<ExtractedRequirementDto> ExtractRequirementsFromFields(NormalizedFieldsDto fields)
    {
        var items = new List<ExtractedRequirementDto>();
        var flows = SplitFieldLines(fields.MainFlows);
        var rules = SplitFieldLines(fields.BusinessRules);
        var firstActor = (fields.Actors ?? string.Empty)
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(a => BulletPrefixRegex().Replace(a.Trim(), "").Trim())
            .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? "người dùng";

        if (flows.Count == 0)
        {
            flows.Add("Quản lý và xử lý luồng nghiệp vụ chính");
        }

        for (int i = 0; i < flows.Count; i++)
        {
            var flow = flows[i].Trim();
            var title = flow.Length > 90 ? flow[..90] : flow;
            var ac = new List<string>
            {
                $"Cho phép thực hiện: {flow}.",
                "Hiển thị thông báo lỗi rõ ràng khi dữ liệu không hợp lệ."
            };
            if (i < rules.Count)
            {
                ac.Add(rules[i]);
            }
            else if (rules.Count > 0)
            {
                ac.Add(rules[0]);
            }

            items.Add(new ExtractedRequirementDto
            {
                TempId = $"tmp-{i + 1}",
                Title = title,
                Description = $"Hệ thống cung cấp chức năng {flow.ToLowerInvariant()} phục vụ {firstActor}.",
                Priority = i < 2 ? "HIGH" : "MEDIUM",
                AcceptanceCriteria = ac,
                SourceLines = new List<int> { i }
            });
        }

        return items;
    }

    public static string BuildGeneratedContentJson(
        NormalizedFieldsDto fields,
        IReadOnlyList<GeneratedItemDto> generatedItems)
    {
        var content = new JsonArray();

        // 1. Phạm vi & Mục tiêu
        content.Add(DocumentContentHelper.MakeHeading(2, "1. Phạm vi & Mục tiêu"));
        content.Add(DocumentContentHelper.MakeParagraph(
            string.IsNullOrWhiteSpace(fields.Scope)
                ? "Tài liệu định nghĩa phạm vi và các yêu cầu nghiệp vụ của phân hệ."
                : fields.Scope));

        // 2. Tác nhân (Actors)
        var actorList = (fields.Actors ?? string.Empty)
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(a => BulletPrefixRegex().Replace(a.Trim(), "").Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();
        if (actorList.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeHeading(2, "2. Tác nhân (Actors)"));
            content.Add(DocumentContentHelper.MakeBulletList(actorList));
        }

        // 3. Quy tắc nghiệp vụ chung
        var ruleList = SplitFieldLines(fields.BusinessRules);
        if (ruleList.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeHeading(2, "3. Quy tắc nghiệp vụ"));
            content.Add(DocumentContentHelper.MakeBulletList(ruleList));
        }

        // 4. Danh sách yêu cầu
        content.Add(DocumentContentHelper.MakeHeading(2, "4. Yêu cầu chi tiết"));
        foreach (var item in generatedItems)
        {
            content.Add(DocumentContentHelper.MakeReqHeading(2, $"{item.ReqUid} — {item.Title}", item.ReqUid, item.Priority));
            content.Add(DocumentContentHelper.MakeParagraph(item.Description));
            if (item.AcceptanceCriteria.Count > 0)
            {
                content.Add(DocumentContentHelper.MakeTaskList(item.AcceptanceCriteria));
            }

            if (item.Diagram is not null)
            {
                content.Add(new JsonObject
                {
                    ["type"] = "mermaidBlock",
                    ["attrs"] = new JsonObject
                    {
                        ["code"] = item.Diagram.Mermaid,
                        ["kind"] = item.Diagram.Kind
                    }
                });
            }
        }

        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["content"] = content
        };

        return doc.ToJsonString();
    }

    private static string AfterColon(string line)
    {
        var idx = line.IndexOf(':');
        if (idx < 0 || idx + 1 >= line.Length) return string.Empty;
        return line[(idx + 1)..].Trim();
    }
}
