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
                content.Add(DocumentContentHelper.MakeCodeBlock("mermaid", item.Diagram.Mermaid));
            }
        }

        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["content"] = content
        };
        return doc.ToJsonString();
    }

    public static string BuildSrsGeneratedContentJson(
        NormalizedFieldsDto fields,
        IReadOnlyList<GeneratedItemDto> generatedItems)
    {
        var content = new JsonArray();
        var flows = SplitFieldLines(fields.MainFlows);
        var actors = (fields.Actors ?? string.Empty)
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(actor => BulletPrefixRegex().Replace(actor.Trim(), "").Trim())
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .ToList();
        var businessRules = SplitFieldLines(fields.BusinessRules);

        content.Add(DocumentContentHelper.MakeHeading(1, "I. Overall Requirements"));
        if (!string.IsNullOrWhiteSpace(fields.Scope))
        {
            content.Add(DocumentContentHelper.MakeParagraph(fields.Scope.Trim()));
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "1. Context Diagram"));
        content.Add(DocumentContentHelper.MakeHeading(2, "2. Main Business Flow"));
        for (var i = 0; i < flows.Count; i++)
        {
            content.Add(DocumentContentHelper.MakeHeading(3, $"2.{i + 1} {flows[i]}"));
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "3. Conceptual Data Model"));
        content.Add(DocumentContentHelper.MakeHeading(3, "3.1 Entity Relationship Diagram"));
        content.Add(DocumentContentHelper.MakeHeading(3, "3.2 Entity Descriptions"));
        content.Add(DocumentContentHelper.MakeHeading(3, "3.3 Data Business Rules"));
        if (businessRules.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(businessRules));
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "4. User Requirements"));
        content.Add(DocumentContentHelper.MakeHeading(3, "4.1 Actors"));
        if (actors.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(actors));
        }
        content.Add(DocumentContentHelper.MakeHeading(3, "4.2 Use Cases (UC)"));
        content.Add(DocumentContentHelper.MakeHeading(3, "4.3 Use Case Diagrams (one per human actor or workflow)"));
        content.Add(DocumentContentHelper.MakeHeading(3, "4.4 Permission Matrix"));

        content.Add(DocumentContentHelper.MakeHeading(2, "5. System Functionalities"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.1 Screen Inventory"));
        content.Add(DocumentContentHelper.MakeHeading(4, "a. Screen Flow"));
        content.Add(DocumentContentHelper.MakeHeading(4, "b. Screen List"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.2 External API Inventory"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.3 Background Job Inventory"));

        content.Add(DocumentContentHelper.MakeHeading(1, "II. Use Case Specifications"));
        content.Add(DocumentContentHelper.MakeHeading(2, "1. Feature Name"));
        content.Add(DocumentContentHelper.MakeHeading(3, "1.1 UC Name (one per use case)"));
        content.Add(DocumentContentHelper.MakeParagraph("Primary Actors:"));
        content.Add(DocumentContentHelper.MakeParagraph("Secondary Actors:"));
        content.Add(DocumentContentHelper.MakeParagraph("Description:"));
        content.Add(DocumentContentHelper.MakeParagraph("Preconditions:"));
        content.Add(DocumentContentHelper.MakeParagraph("Normal Flow:"));
        content.Add(DocumentContentHelper.MakeParagraph("Alternative Flows:"));
        content.Add(DocumentContentHelper.MakeParagraph("Postconditions:"));
        content.Add(DocumentContentHelper.MakeHeading(2, "2. Next Feature Name"));

        content.Add(DocumentContentHelper.MakeHeading(1, "III. Functional Requirements"));
        for (var i = 0; i < generatedItems.Count; i++)
        {
            var item = generatedItems[i];
            content.Add(DocumentContentHelper.MakeReqHeading(
                2,
                $"{item.ReqUid} — {item.Title}",
                item.ReqUid,
                item.Priority));
            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                content.Add(DocumentContentHelper.MakeParagraph(item.Description.Trim()));
            }

            if (item.AcceptanceCriteria.Count > 0)
            {
                content.Add(DocumentContentHelper.MakeTaskList(item.AcceptanceCriteria));
            }

            if (item.Diagram is not null && !string.IsNullOrWhiteSpace(item.Diagram.Mermaid))
            {
                content.Add(DocumentContentHelper.MakeCodeBlock("mermaid", item.Diagram.Mermaid));
            }

            content.Add(DocumentContentHelper.MakeHeading(3, $"{i + 1}.1 SubFeature Name"));
            content.Add(DocumentContentHelper.MakeHeading(4, $"{i + 1}.1.1 Screen Name"));
            content.Add(DocumentContentHelper.MakeParagraph("UI layout (wireframe/mockup):"));
            content.Add(DocumentContentHelper.MakeParagraph("Brief description mapped to use cases:"));
            content.Add(DocumentContentHelper.MakeParagraph("Field description table:"));
        }
        content.Add(DocumentContentHelper.MakeHeading(2, "4. Background Job Inventory"));
        content.Add(DocumentContentHelper.MakeParagraph("JOB-ID:"));
        content.Add(DocumentContentHelper.MakeParagraph("Trigger:"));
        content.Add(DocumentContentHelper.MakeParagraph("Purpose:"));
        content.Add(DocumentContentHelper.MakeParagraph("Processing steps:"));
        content.Add(DocumentContentHelper.MakeParagraph("Failure behavior:"));
        content.Add(DocumentContentHelper.MakeHeading(2, "5. External API Inventory"));
        content.Add(DocumentContentHelper.MakeParagraph("API-ID:"));
        content.Add(DocumentContentHelper.MakeParagraph("URL:"));
        content.Add(DocumentContentHelper.MakeParagraph("Purpose:"));
        content.Add(DocumentContentHelper.MakeParagraph("Caller:"));
        content.Add(DocumentContentHelper.MakeParagraph("Auth:"));

        content.Add(DocumentContentHelper.MakeHeading(1, "IV. Non-Functional Requirements"));
        content.Add(DocumentContentHelper.MakeHeading(2, "1. External Interfaces"));
        content.Add(DocumentContentHelper.MakeHeading(2, "2. Quality Attributes"));
        content.Add(DocumentContentHelper.MakeHeading(3, "2.1 Usability"));
        content.Add(DocumentContentHelper.MakeHeading(3, "2.2 Performance"));
        content.Add(DocumentContentHelper.MakeHeading(3, "2.3 Security"));

        content.Add(DocumentContentHelper.MakeHeading(1, "V. Requirement Appendix"));
        content.Add(DocumentContentHelper.MakeHeading(2, "1. Business Rules"));
        if (businessRules.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(businessRules));
        }
        content.Add(DocumentContentHelper.MakeHeading(2, "2. System Messages"));
        content.Add(DocumentContentHelper.MakeHeading(2, "3. Other Requirements"));

        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["content"] = content
        };
        return doc.ToJsonString();
    }

    public static string BuildBrdGeneratedContentJson(
        NormalizedFieldsDto fields,
        IReadOnlyList<GeneratedItemDto> generatedItems)
    {
        var content = new JsonArray();
        var actors = (fields.Actors ?? string.Empty)
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(actor => BulletPrefixRegex().Replace(actor.Trim(), "").Trim())
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .ToList();
        var businessRules = SplitFieldLines(fields.BusinessRules);

        content.Add(DocumentContentHelper.MakeHeading(2, "1. Business Problem"));
        content.Add(DocumentContentHelper.MakeHeading(2, "2. Business Objective"));
        content.Add(DocumentContentHelper.MakeHeading(2, "3. Scope"));
        if (!string.IsNullOrWhiteSpace(fields.Scope))
        {
            content.Add(DocumentContentHelper.MakeParagraph(fields.Scope.Trim()));
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "4. Stakeholders"));
        if (actors.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(actors));
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "5. Business Requirements"));
        foreach (var item in generatedItems)
        {
            content.Add(DocumentContentHelper.MakeReqHeading(
                3,
                $"{item.ReqUid} — {item.Title}",
                item.ReqUid,
                item.Priority));
            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                content.Add(DocumentContentHelper.MakeParagraph(item.Description.Trim()));
            }

            if (item.AcceptanceCriteria.Count > 0)
            {
                content.Add(DocumentContentHelper.MakeTaskList(item.AcceptanceCriteria));
            }

            if (item.Diagram is not null && !string.IsNullOrWhiteSpace(item.Diagram.Mermaid))
            {
                content.Add(DocumentContentHelper.MakeCodeBlock("mermaid", item.Diagram.Mermaid));
            }
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "6. Business Rules"));
        if (businessRules.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(businessRules));
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "7. Assumptions & Dependencies"));
        content.Add(DocumentContentHelper.MakeHeading(2, "8. Constraints & Risks"));
        content.Add(DocumentContentHelper.MakeHeading(2, "9. Success Criteria"));
        var successCriteria = generatedItems
            .SelectMany(item => item.AcceptanceCriteria.Select(criteria => $"{item.Title}: {criteria}"))
            .ToList();
        if (successCriteria.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(successCriteria));
        }

        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["content"] = content
        };
        return doc.ToJsonString();
    }

    public static string BuildUrdGeneratedContentJson(
        NormalizedFieldsDto fields,
        IReadOnlyList<GeneratedItemDto> generatedItems)
    {
        var content = new JsonArray();
        var actors = (fields.Actors ?? string.Empty)
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(actor => BulletPrefixRegex().Replace(actor.Trim(), "").Trim())
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .ToList();
        var flows = SplitFieldLines(fields.MainFlows);

        content.Add(DocumentContentHelper.MakeHeading(2, "1. Introduction"));
        content.Add(DocumentContentHelper.MakeHeading(3, "1.1 Purpose of the Document"));
        content.Add(DocumentContentHelper.MakeHeading(3, "1.2 Intended Audience"));
        content.Add(DocumentContentHelper.MakeHeading(3, "1.3 Project Background"));
        content.Add(DocumentContentHelper.MakeHeading(3, "1.4 Scope"));
        if (!string.IsNullOrWhiteSpace(fields.Scope))
        {
            content.Add(DocumentContentHelper.MakeParagraph(fields.Scope.Trim()));
        }
        content.Add(DocumentContentHelper.MakeHeading(3, "1.5 References"));

        content.Add(DocumentContentHelper.MakeHeading(2, "2. System Overview"));
        content.Add(DocumentContentHelper.MakeHeading(3, "2.1 System Purpose and Objectives"));
        content.Add(DocumentContentHelper.MakeHeading(3, "2.2 Business Context"));
        content.Add(DocumentContentHelper.MakeHeading(3, "2.3 Current System / Process (As-Is)"));
        content.Add(DocumentContentHelper.MakeHeading(3, "2.4 Proposed System (To-Be)"));
        content.Add(DocumentContentHelper.MakeHeading(3, "2.5 Context Diagram"));

        content.Add(DocumentContentHelper.MakeHeading(2, "3. User Description"));
        content.Add(DocumentContentHelper.MakeHeading(3, "3.1 User Groups / Actors"));
        if (actors.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(actors));
        }
        content.Add(DocumentContentHelper.MakeHeading(3, "3.2 User Roles and Responsibilities"));
        content.Add(DocumentContentHelper.MakeHeading(3, "3.3 User Characteristics (skills, experience, access level)"));
        content.Add(DocumentContentHelper.MakeHeading(3, "3.4 User Personas (optional)"));

        content.Add(DocumentContentHelper.MakeHeading(2, "4. User Requirements"));
        content.Add(DocumentContentHelper.MakeHeading(3, "4.1 Functional Requirements"));
        foreach (var item in generatedItems)
        {
            content.Add(DocumentContentHelper.MakeReqHeading(
                3,
                $"{item.ReqUid} — {item.Title}",
                item.ReqUid,
                item.Priority));
            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                content.Add(DocumentContentHelper.MakeParagraph(item.Description.Trim()));
            }

            if (item.AcceptanceCriteria.Count > 0)
            {
                content.Add(DocumentContentHelper.MakeTaskList(item.AcceptanceCriteria));
            }

            if (item.Diagram is not null && !string.IsNullOrWhiteSpace(item.Diagram.Mermaid))
            {
                content.Add(DocumentContentHelper.MakeCodeBlock("mermaid", item.Diagram.Mermaid));
            }
        }
        content.Add(DocumentContentHelper.MakeHeading(3, "4.2 Data Requirements"));
        content.Add(DocumentContentHelper.MakeHeading(3, "4.3 Reporting and Output Requirements"));
        content.Add(DocumentContentHelper.MakeHeading(3, "4.4 Interface Requirements (user interface, external systems)"));
        content.Add(DocumentContentHelper.MakeHeading(3, "4.5 Use Cases / User Stories"));
        content.Add(DocumentContentHelper.MakeHeading(4, "4.5.1 Use Case List"));
        content.Add(DocumentContentHelper.MakeHeading(4, "4.5.2 Use Case Diagrams"));
        content.Add(DocumentContentHelper.MakeHeading(3, "4.6 Business Process Flows"));
        if (flows.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(flows));
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "5. Non-Functional User Requirements"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.1 Usability"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.2 Performance"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.3 Security and Access Control"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.4 Availability and Reliability"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.5 Compatibility / Supported Platforms"));
        content.Add(DocumentContentHelper.MakeHeading(3, "5.6 Legal and Compliance"));

        content.Add(DocumentContentHelper.MakeHeading(2, "6. Constraints, Assumptions and Dependencies"));
        content.Add(DocumentContentHelper.MakeHeading(3, "6.1 Constraints"));
        content.Add(DocumentContentHelper.MakeHeading(3, "6.2 Assumptions"));
        content.Add(DocumentContentHelper.MakeHeading(3, "6.3 Dependencies"));

        content.Add(DocumentContentHelper.MakeHeading(2, "7. Requirement Priority"));
        content.Add(DocumentContentHelper.MakeHeading(3, "7.1 Prioritization Method (e.g. MoSCoW)"));
        content.Add(DocumentContentHelper.MakeHeading(3, "7.2 Requirements Priority List"));
        var priorities = generatedItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Priority))
            .Select(item => $"{item.ReqUid} — {item.Title}: {item.Priority}")
            .ToList();
        if (priorities.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(priorities));
        }

        content.Add(DocumentContentHelper.MakeHeading(2, "8. Acceptance Criteria"));
        content.Add(DocumentContentHelper.MakeHeading(3, "8.1 Acceptance Conditions per Requirement"));
        var acceptanceConditions = generatedItems
            .SelectMany(item => item.AcceptanceCriteria.Select(criteria => $"{item.ReqUid} — {item.Title}: {criteria}"))
            .ToList();
        if (acceptanceConditions.Count > 0)
        {
            content.Add(DocumentContentHelper.MakeBulletList(acceptanceConditions));
        }
        content.Add(DocumentContentHelper.MakeHeading(3, "8.2 Validation and Sign-off Approach"));

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
