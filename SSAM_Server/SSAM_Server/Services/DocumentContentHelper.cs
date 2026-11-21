using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Models;

namespace SSAM_Server.Services;

public static partial class DocumentContentHelper
{
    [GeneratedRegex(@"^(REQ-\d+)\s*[—–\-:·]\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReqHeadingRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public sealed record ExtractedRequirementNode(
        string ReqUid,
        string AnchorId,
        string Title,
        string BodyPreview,
        int BodyLength,
        string? Actor,
        string? Preconditions,
        List<ConstraintRowDto> Constraints,
        List<string> AcceptanceCriteria,
        bool HasTable,
        bool HasDiagram,
        bool HasFigma,
        string ContentHash,
        int SortOrder
    );

    public sealed record ReqChangeSummary(
        List<string> Added,
        List<string> Modified,
        List<string> Removed,
        List<string> Unchanged
    );

    public static (string NormalizedContentJson, string PlainText, List<ExtractedRequirementNode> Requirements)
        ProcessContent(JsonElement contentElement)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(contentElement.GetRawText());
        }
        catch
        {
            root = new JsonObject { ["type"] = "doc", ["content"] = new JsonArray() };
        }

        if (root is not JsonObject docObj)
        {
            docObj = new JsonObject { ["type"] = "doc", ["content"] = new JsonArray() };
        }

        var blocks = docObj["content"] as JsonArray;
        if (blocks == null)
        {
            blocks = new JsonArray();
            docObj["content"] = blocks;
        }

        // 1. Ensure every heading has attrs.anchorId (UUID v4)
        EnsureHeadingAnchorIds(blocks);

        // 2. Extract PlainText
        var plainText = ExtractPlainText(docObj).Trim();

        // 3. Extract Requirements
        var requirements = ExtractRequirementItems(blocks);

        return (docObj.ToJsonString(), plainText, requirements);
    }

    public static List<DTOs.Documents.DocSectionDto> ExtractDocSections(string? contentJson)
    {
        var sections = new List<DTOs.Documents.DocSectionDto>();
        if (string.IsNullOrWhiteSpace(contentJson))
            return sections;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(contentJson);
        }
        catch
        {
            return sections;
        }

        if (root is not JsonObject docObj || docObj["content"] is not JsonArray nodes)
            return sections;

        DTOs.Documents.DocSectionDto? current = null;
        foreach (var node in nodes)
        {
            if (node is not JsonObject obj) continue;
            if (obj["type"]?.GetValue<string>() != "heading") continue;

            var attrs = obj["attrs"] as JsonObject;
            var title = ExtractHeadingInlineText(obj).Trim();
            if (string.IsNullOrEmpty(title)) continue;

            var isReq = false;
            if (attrs?["req"] is JsonValue reqVal && reqVal.TryGetValue<bool>(out var bReq))
            {
                isReq = bReq;
            }

            if (isReq)
            {
                var reqUid = attrs?["reqUid"]?.GetValue<string>()?.Trim();
                if (current != null && !string.IsNullOrEmpty(reqUid))
                {
                    current.ReqUids.Add(reqUid);
                }
            }
            else
            {
                var id = attrs?["id"]?.GetValue<string>()?.Trim()
                         ?? attrs?["anchorId"]?.GetValue<string>()?.Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    var level = 1;
                    if (attrs?["level"] is JsonValue lvlVal && lvlVal.TryGetValue<int>(out var parsedLvl))
                    {
                        level = parsedLvl;
                    }
                    current = new DTOs.Documents.DocSectionDto
                    {
                        AnchorId = id,
                        Title = title,
                        Level = level,
                        ReqUids = new List<string>()
                    };
                    sections.Add(current);
                }
            }
        }

        return sections;
    }

    private static string ExtractHeadingInlineText(JsonObject headingNode)
    {
        if (headingNode["content"] is not JsonArray children || children.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var c in children)
        {
            if (c is JsonObject childObj && childObj["text"] is JsonValue tv && tv.TryGetValue<string>(out var s))
            {
                sb.Append(s);
            }
            else
            {
                sb.Append(ExtractPlainText(c));
            }
        }
        return sb.ToString();
    }

    private static void EnsureHeadingAnchorIds(JsonArray nodes)
    {
        foreach (var node in nodes)
        {
            if (node is not JsonObject obj) continue;

            var type = obj["type"]?.GetValue<string>();
            if (type == "heading")
            {
                var attrs = obj["attrs"] as JsonObject;
                if (attrs == null)
                {
                    attrs = new JsonObject();
                    obj["attrs"] = attrs;
                }

                var anchorId = attrs["id"]?.GetValue<string>() ?? attrs["anchorId"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(anchorId))
                {
                    anchorId = $"h-{Guid.NewGuid():N}"[..10];
                }
                else if (anchorId.Length > 36)
                {
                    anchorId = anchorId[..36];
                }

                attrs["id"] = anchorId;
                attrs["anchorId"] = anchorId;
            }

            if (obj["content"] is JsonArray childContent)
            {
                EnsureHeadingAnchorIds(childContent);
            }
        }
    }

    public static string ExtractPlainText(JsonNode? node)
    {
        if (node is not JsonObject obj) return string.Empty;

        var type = obj["type"]?.GetValue<string>();
        if (type == "text")
        {
            return obj["text"]?.GetValue<string>() ?? string.Empty;
        }

        if (obj["content"] is not JsonArray children || children.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var child in children)
        {
            var childText = ExtractPlainText(child);
            if (!string.IsNullOrEmpty(childText))
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(childText);
            }
        }
        return sb.ToString();
    }

    private static List<ExtractedRequirementNode> ExtractRequirementItems(JsonArray topBlocks)
    {
        var results = new List<ExtractedRequirementNode>();
        var seenReqUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < topBlocks.Count; i++)
        {
            if (topBlocks[i] is not JsonObject block) continue;
            if (block["type"]?.GetValue<string>() != "heading") continue;

            var attrs = block["attrs"] as JsonObject;
            var level = attrs?["level"]?.GetValue<int>() ?? 0;
            if (level is not (1 or 2 or 3)) continue;

            var headingText = CollapseSpaces(ExtractPlainText(block));
            var isAttrReq = false;
            if (attrs?["req"] is JsonValue reqVal && reqVal.TryGetValue<bool>(out var bReq))
            {
                isAttrReq = bReq;
            }

            var match = ReqHeadingRegex().Match(headingText);
            if (!isAttrReq && !match.Success) continue;

            var attrReqUid = attrs?["reqUid"]?.GetValue<string>()?.Trim();
            string reqUid;
            if (!string.IsNullOrWhiteSpace(attrReqUid))
            {
                reqUid = attrReqUid.Length > 30 ? attrReqUid[..30] : attrReqUid;
            }
            else if (match.Success)
            {
                reqUid = match.Groups[1].Value.ToUpperInvariant();
            }
            else
            {
                reqUid = $"REQ-{results.Count + 1:D2}";
            }

            var rawTitle = match.Success ? match.Groups[2].Value.Trim() : headingText.Trim();
            if (string.IsNullOrWhiteSpace(rawTitle)) rawTitle = reqUid;
            var title = rawTitle.Length > 200 ? rawTitle[..200] : rawTitle;

            var anchorId = attrs?["id"]?.GetValue<string>() ?? attrs?["anchorId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = $"h-{Guid.NewGuid():N}"[..10];
            }
            else if (anchorId.Length > 36)
            {
                anchorId = anchorId[..36];
            }

            string? actor = null;
            string? preconditions = null;

            if (attrs != null)
            {
                attrs["id"] = anchorId;
                attrs["anchorId"] = anchorId;
                attrs["req"] = true;
                attrs["reqUid"] = reqUid;
                if (attrs["priority"] == null)
                {
                    attrs["priority"] = "MEDIUM";
                }
                actor = TrimOrNull(GetStringAttr(attrs["actor"]), 200);
                preconditions = TrimOrNull(GetStringAttr(attrs["preconditions"]), 200);
            }

            // Collect section blocks until next heading with level <= current level
            var sectionBlocks = new List<JsonObject>();
            for (int j = i + 1; j < topBlocks.Count; j++)
            {
                if (topBlocks[j] is not JsonObject nextBlock) continue;
                if (nextBlock["type"]?.GetValue<string>() == "heading")
                {
                    var nextLevel = (nextBlock["attrs"] as JsonObject)?["level"]?.GetValue<int>() ?? 0;
                    if (nextLevel > 0 && nextLevel <= level)
                    {
                        break;
                    }
                }
                sectionBlocks.Add(nextBlock);
            }

            // Deduplicate if same reqUid appears twice in same version (keep first)
            if (!seenReqUids.Add(reqUid)) continue;

            var bodyParts = new List<string>();
            var acList = new List<string>();
            var constraintsList = new List<ConstraintRowDto>();
            bool hasTable = false;
            bool hasDiagram = false;
            bool hasFigma = false;
            var tableSerializedParts = new List<string>();
            var diagramMermaidParts = new List<string>();
            var figmaParts = new List<string>();

            foreach (var secBlock in sectionBlocks)
            {
                InspectBlock(secBlock, bodyParts, acList, constraintsList, ref hasTable, ref hasDiagram, ref hasFigma, tableSerializedParts, diagramMermaidParts, figmaParts);
            }

            var fullBody = CollapseSpaces(string.Join(" ", bodyParts));
            var bodyLength = fullBody.Length;
            var bodyPreview = fullBody.Length > 280 ? fullBody[..280] : fullBody;

            // Compute contentHash SHA-256 hex lowercase
            var canonicalPayload = new StringBuilder();
            canonicalPayload.Append(CollapseSpaces(title).ToLowerInvariant()).Append('\n');
            canonicalPayload.Append((actor ?? string.Empty).ToLowerInvariant()).Append('\n');
            canonicalPayload.Append((preconditions ?? string.Empty).ToLowerInvariant()).Append('\n');
            canonicalPayload.Append(fullBody.ToLowerInvariant()).Append('\n');
            canonicalPayload.Append(string.Join("\n", acList.Select(a => CollapseSpaces(a).ToLowerInvariant()))).Append('\n');
            canonicalPayload.Append(string.Join("\n", tableSerializedParts)).Append('\n');
            canonicalPayload.Append(string.Join("\n", diagramMermaidParts)).Append('\n');
            canonicalPayload.Append(string.Join("\n", figmaParts));

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload.ToString()));
            var contentHash = Convert.ToHexStringLower(hashBytes);

            results.Add(new ExtractedRequirementNode(
                ReqUid: reqUid,
                AnchorId: anchorId,
                Title: title,
                BodyPreview: bodyPreview,
                BodyLength: bodyLength,
                Actor: actor,
                Preconditions: preconditions,
                Constraints: constraintsList,
                AcceptanceCriteria: acList,
                HasTable: hasTable,
                HasDiagram: hasDiagram,
                HasFigma: hasFigma,
                ContentHash: contentHash,
                SortOrder: results.Count
            ));
        }

        return results;
    }

    private static void InspectBlock(
        JsonObject block,
        List<string> bodyParts,
        List<string> acList,
        List<ConstraintRowDto> constraintsList,
        ref bool hasTable,
        ref bool hasDiagram,
        ref bool hasFigma,
        List<string> tableSerializedParts,
        List<string> diagramMermaidParts,
        List<string> figmaParts)
    {
        var type = block["type"]?.GetValue<string>() ?? string.Empty;

        switch (type)
        {
            case "constraints":
                hasTable = true;
                var extractedRows = ExtractConstraintRows(block);
                if (extractedRows.Count > 0)
                {
                    constraintsList.AddRange(extractedRows);
                    tableSerializedParts.Add(JsonSerializer.Serialize(extractedRows).ToLowerInvariant());
                }
                break;
            case "taskList":
                if (block["content"] is JsonArray taskItems)
                {
                    foreach (var ti in taskItems)
                    {
                        var text = CollapseSpaces(ExtractPlainText(ti));
                        if (!string.IsNullOrEmpty(text))
                        {
                            acList.Add(text);
                        }
                    }
                }
                break;

            case "table":
                hasTable = true;
                var tableText = CollapseSpaces(ExtractPlainText(block));
                if (!string.IsNullOrEmpty(tableText))
                {
                    tableSerializedParts.Add(tableText.ToLowerInvariant());
                    bodyParts.Add(tableText);
                }
                break;

            case "mermaidBlock":
            case "diagram":
                hasDiagram = true;
                var code = (block["attrs"] as JsonObject)?["code"]?.GetValue<string>()
                           ?? (block["attrs"] as JsonObject)?["mermaid"]?.GetValue<string>()
                           ?? ExtractPlainText(block);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    diagramMermaidParts.Add(CollapseSpaces(code));
                }
                break;

            case "codeBlock":
                var lang = ((block["attrs"] as JsonObject)?["language"]?.GetValue<string>() ?? string.Empty).Trim();
                var cbText = ExtractPlainText(block);
                if (lang.Equals("mermaid", StringComparison.OrdinalIgnoreCase)
                    || lang.Equals("plantuml", StringComparison.OrdinalIgnoreCase)
                    || lang.Equals("puml", StringComparison.OrdinalIgnoreCase))
                {
                    hasDiagram = true;
                    diagramMermaidParts.Add(CollapseSpaces(cbText));
                }
                else if (!string.IsNullOrWhiteSpace(cbText))
                {
                    bodyParts.Add(cbText);
                }
                break;

            case "figmaEmbed":
            case "figma":
                hasFigma = true;
                if (block["attrs"] is JsonNode figmaAttrs)
                {
                    figmaParts.Add(figmaAttrs.ToJsonString().ToLowerInvariant());
                }
                break;

            default:
                var plain = ExtractPlainText(block);
                if (!string.IsNullOrWhiteSpace(plain))
                {
                    bodyParts.Add(plain);
                    if (plain.Contains("figma.com/", StringComparison.OrdinalIgnoreCase))
                    {
                        hasFigma = true;
                    }
                }
                if (HasFigmaLinkInMarks(block))
                {
                    hasFigma = true;
                }
                break;
        }
    }

    private static bool HasFigmaLinkInMarks(JsonNode? node)
    {
        if (node is not JsonObject obj) return false;
        if (obj["marks"] is JsonArray marks)
        {
            foreach (var m in marks)
            {
                if (m is JsonObject mObj && mObj["type"]?.GetValue<string>() == "link")
                {
                    var href = (mObj["attrs"] as JsonObject)?["href"]?.GetValue<string>();
                    if (href != null && href.Contains("figma.com/", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        if (obj["content"] is JsonArray children)
        {
            foreach (var child in children)
            {
                if (HasFigmaLinkInMarks(child)) return true;
            }
        }
        return false;
    }

    public static string CollapseSpaces(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return WhitespaceRegex().Replace(input.Trim(), " ");
    }

    public static ReqChangeSummary SummarizeReqChanges(
        IReadOnlyList<RequirementItem> beforeItems,
        IReadOnlyList<RequirementItem> afterItems)
    {
        var beforeMap = beforeItems.ToDictionary(r => r.ReqUid, StringComparer.OrdinalIgnoreCase);
        var afterMap = afterItems.ToDictionary(r => r.ReqUid, StringComparer.OrdinalIgnoreCase);

        var added = new List<string>();
        var modified = new List<string>();
        var removed = new List<string>();
        var unchanged = new List<string>();

        foreach (var after in afterItems)
        {
            if (!beforeMap.TryGetValue(after.ReqUid, out var before))
            {
                added.Add(after.ReqUid);
            }
            else if (!string.Equals(before.ContentHash, after.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                modified.Add(after.ReqUid);
            }
            else
            {
                unchanged.Add(after.ReqUid);
            }
        }

        foreach (var before in beforeItems)
        {
            if (!afterMap.ContainsKey(before.ReqUid))
            {
                removed.Add(before.ReqUid);
            }
        }

        return new ReqChangeSummary(added, modified, removed, unchanged);
    }

    public static string FormatReqSummary(ReqChangeSummary s)
    {
        var parts = new List<string>();
        if (s.Modified.Count > 0) parts.Add($"Sửa {string.Join(", ", s.Modified)}");
        if (s.Added.Count > 0) parts.Add($"Thêm {string.Join(", ", s.Added)}");
        if (s.Removed.Count > 0) parts.Add($"Xoá {string.Join(", ", s.Removed)}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "Không thay đổi yêu cầu nào";
    }

    public static string BuildInitialTemplateContentJson(string docType, string title, string? reqUid = null)
    {
        var content = new JsonArray();

        if (docType is DocumentTypes.Urd or DocumentTypes.Brd or DocumentTypes.Srs)
        {
            content.Add(MakeHeading(2, "1. Bối cảnh & Mục tiêu"));
            content.Add(MakeParagraph($"Tài liệu {title} mô tả phạm vi, tác nhân và các yêu cầu nghiệp vụ cốt lõi."));

            var uid = reqUid ?? "REQ-01";
            content.Add(MakeReqHeading(2, $"{uid} — Yêu cầu chức năng mẫu", uid));
            content.Add(MakeParagraph("Mô tả chi tiết luồng xử lý chính và quy tắc nghiệp vụ cho yêu cầu này."));
            content.Add(MakeTaskList(new[]
            {
                "Hệ thống kiểm tra điều kiện đầu vào hợp lệ.",
                "Ghi nhận lịch sử thao tác và trả về trạng thái thành công."
            }));
        }
        else
        {
            content.Add(MakeHeading(2, "1. Tổng quan"));
            content.Add(MakeParagraph($"Nội dung khởi tạo cho tài liệu {title}."));
        }

        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["content"] = content
        };
        return doc.ToJsonString();
    }

    public static JsonObject MakeHeading(int level, string text, string? anchorId = null)
    {
        var id = anchorId ?? $"h-{Guid.NewGuid():N}"[..10];
        return new JsonObject
        {
            ["type"] = "heading",
            ["attrs"] = new JsonObject
            {
                ["level"] = level,
                ["id"] = id,
                ["anchorId"] = id
            },
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = text
                }
            }
        };
    }

    public static JsonObject MakeReqHeading(int level, string text, string reqUid, string priority = "MEDIUM", string? anchorId = null)
    {
        var id = anchorId ?? $"h-{Guid.NewGuid():N}"[..10];
        return new JsonObject
        {
            ["type"] = "heading",
            ["attrs"] = new JsonObject
            {
                ["level"] = level,
                ["id"] = id,
                ["anchorId"] = id,
                ["req"] = true,
                ["reqUid"] = reqUid,
                ["priority"] = priority
            },
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = text
                }
            }
        };
    }

    public static JsonObject MakeParagraph(string text)
    {
        var content = new JsonArray();
        if (!string.IsNullOrEmpty(text))
        {
            content.Add(new JsonObject
            {
                ["type"] = "text",
                ["text"] = text
            });
        }
        return new JsonObject
        {
            ["type"] = "paragraph",
            ["content"] = content
        };
    }

    public static JsonObject MakeBulletList(IEnumerable<string> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
        {
            arr.Add(new JsonObject
            {
                ["type"] = "listItem",
                ["content"] = new JsonArray { MakeParagraph(item) }
            });
        }
        return new JsonObject
        {
            ["type"] = "bulletList",
            ["content"] = arr
        };
    }

    public static JsonObject MakeTaskList(IEnumerable<string> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
        {
            arr.Add(new JsonObject
            {
                ["type"] = "taskItem",
                ["attrs"] = new JsonObject { ["checked"] = false },
                ["content"] = new JsonArray { MakeParagraph(item) }
            });
        }
        return new JsonObject
        {
            ["type"] = "taskList",
            ["content"] = arr
        };
    }

    private static string? TrimOrNull(string? value, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string? GetStringAttr(JsonNode? node)
    {
        if (node is JsonValue jv && jv.TryGetValue<string>(out var s))
            return s;
        return null;
    }

    private static bool GetBoolAttr(JsonNode? node)
    {
        if (node is JsonValue jv && jv.TryGetValue<bool>(out var b))
            return b;
        return false;
    }

    private static double? GetDoubleAttr(JsonNode? node)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<double>(out var d) && !double.IsNaN(d) && !double.IsInfinity(d))
                return d;
            if (jv.TryGetValue<int>(out var i))
                return i;
            if (jv.TryGetValue<long>(out var l))
                return l;
            if (jv.TryGetValue<string>(out var s) &&
                double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                !double.IsNaN(parsed) && !double.IsInfinity(parsed))
                return parsed;
        }
        return null;
    }

    private static List<ConstraintRowDto> ExtractConstraintRows(JsonObject block)
    {
        var list = new List<ConstraintRowDto>();
        if ((block["attrs"] as JsonObject)?["rows"] is not JsonArray rows)
            return list;

        foreach (var item in rows)
        {
            if (item is not JsonObject row) continue;

            var field = TrimOrNull(GetStringAttr(row["field"]), 200);
            if (field == null) continue;

            var kind = TrimOrNull(GetStringAttr(row["kind"]), 50) ?? "text";
            var options = new List<string>();
            if (row["options"] is JsonArray optArr)
            {
                foreach (var opt in optArr)
                {
                    var optStr = TrimOrNull(GetStringAttr(opt), 200);
                    if (optStr != null) options.Add(optStr);
                }
            }
            else if (GetStringAttr(row["options"]) is { } optText)
            {
                foreach (var part in optText.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var optStr = TrimOrNull(part, 200);
                    if (optStr != null) options.Add(optStr);
                }
            }

            list.Add(new ConstraintRowDto
            {
                Field = field,
                Kind = kind,
                Required = GetBoolAttr(row["required"]),
                Min = GetDoubleAttr(row["min"]),
                Max = GetDoubleAttr(row["max"]),
                LettersAndDigits = GetBoolAttr(row["lettersAndDigits"]),
                Options = options,
                Message = TrimOrNull(GetStringAttr(row["message"]), 500)
            });
        }

        return list;
    }
}
