using System.Text.Json;
using System.Text.Json.Nodes;
using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>
/// Shared JSON builders for DocumentContentHelper tests (ProseMirror/TipTap style documents).
/// </summary>
public abstract class DocumentContentHelperTestBase
{
    protected static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    protected static string Quote(string s) => JsonSerializer.Serialize(s);

    protected static string Doc(params string[] blocks)
        => "{\"type\":\"doc\",\"content\":[" + string.Join(",", blocks) + "]}";

    protected static string TextNode(string text) => "{\"type\":\"text\",\"text\":" + Quote(text) + "}";

    protected static string P(string text) => "{\"type\":\"paragraph\",\"content\":[" + TextNode(text) + "]}";

    /// <summary>Heading block. extraAttrs is a raw JSON fragment such as "\"id\":\"a\"" (no braces).</summary>
    protected static string H(int level, string text, string? extraAttrs = null)
        => "{\"type\":\"heading\",\"attrs\":{\"level\":" + level
           + (extraAttrs == null ? "" : "," + extraAttrs)
           + "},\"content\":[" + TextNode(text) + "]}";

    /// <summary>Requirement heading (attrs.req = true).</summary>
    protected static string ReqH(int level, string text, string? extraAttrs = null)
        => H(level, text, "\"req\":true" + (extraAttrs == null ? "" : "," + extraAttrs));

    protected static string Constraints(string rowsJson)
        => "{\"type\":\"constraints\",\"attrs\":{\"rows\":" + rowsJson + "}}";

    protected static string TaskList(params string[] items)
        => "{\"type\":\"taskList\",\"content\":[" + string.Join(",", items.Select(i =>
            "{\"type\":\"taskItem\",\"content\":[" + P(i) + "]}")) + "]}";

    protected static (string NormalizedContentJson, string PlainText, List<DocumentContentHelper.ExtractedRequirementNode> Requirements)
        Process(params string[] blocks)
        => DocumentContentHelper.ProcessContent(Json(Doc(blocks)));

    protected static List<DocumentContentHelper.ExtractedRequirementNode> Extract(params string[] blocks)
        => Process(blocks).Requirements;

    /// <summary>Extracts the single requirement "REQ-01 — Title" followed by the given section blocks.</summary>
    protected static DocumentContentHelper.ExtractedRequirementNode ExtractOne(params string[] sectionBlocks)
    {
        var blocks = new List<string> { H(2, "REQ-01 — Title") };
        blocks.AddRange(sectionBlocks);
        return Assert.Single(Extract(blocks.ToArray()));
    }

    protected static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;
}
