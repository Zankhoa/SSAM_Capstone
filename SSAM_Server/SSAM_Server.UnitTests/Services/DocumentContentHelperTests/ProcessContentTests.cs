using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>ProcessContent: root handling, heading anchor ids (EnsureHeadingAnchorIds) and plain text.</summary>
public class ProcessContentTests : DocumentContentHelperTestBase
{
    private const string EmptyDocJson = "{\"type\":\"doc\",\"content\":[]}";

    private static JsonObject Heading(string normalizedJson, int index = 0)
    {
        var root = (JsonObject)JsonNode.Parse(normalizedJson)!;
        var arr = (JsonArray)root["content"]!;
        return (JsonObject)arr[index]!;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Valid doc with heading + paragraph -> Normalized JSON keeps type/content, plain text joined, no requirements
    [Fact]
    public void ProcessContent_WithValidDoc_ReturnsNormalizedJsonPlainTextAndNoRequirements()
    {
        var result = Process(H(1, "Overview", "\"id\":\"intro\""), P("Hello world"));

        var root = (JsonObject)JsonNode.Parse(result.NormalizedContentJson)!;
        Assert.Equal("doc", root["type"]!.GetValue<string>());
        Assert.Equal(2, ((JsonArray)root["content"]!).Count);
        Assert.Equal("Overview Hello world", result.PlainText);
        Assert.Empty(result.Requirements);
    }

    // UTC02: Heading with existing attrs.id -> id and anchorId both set to that id
    [Fact]
    public void ProcessContent_HeadingWithId_MirrorsIdToAnchorId()
    {
        var result = Process(H(1, "Intro", "\"id\":\"my-id\""));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Equal("my-id", attrs["id"]!.GetValue<string>());
        Assert.Equal("my-id", attrs["anchorId"]!.GetValue<string>());
        Assert.Equal(1, attrs["level"]!.GetValue<int>());
    }

    // UTC03: Heading with only attrs.anchorId -> id copied from anchorId
    [Fact]
    public void ProcessContent_HeadingWithOnlyAnchorId_CopiesAnchorIdToId()
    {
        var result = Process(H(1, "Intro", "\"anchorId\":\"anc-1\""));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Equal("anc-1", attrs["id"]!.GetValue<string>());
        Assert.Equal("anc-1", attrs["anchorId"]!.GetValue<string>());
    }

    // UTC04: Heading with both id and anchorId -> id wins for both
    [Fact]
    public void ProcessContent_HeadingWithIdAndAnchorId_IdTakesPrecedence()
    {
        var result = Process(H(1, "Intro", "\"id\":\"first\",\"anchorId\":\"second\""));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Equal("first", attrs["id"]!.GetValue<string>());
        Assert.Equal("first", attrs["anchorId"]!.GetValue<string>());
    }

    // UTC05: Heading without any id -> Generated "h-" + 8 hex chars (10 chars), same in id and anchorId
    [Fact]
    public void ProcessContent_HeadingWithoutId_GeneratesTenCharAnchorId()
    {
        var result = Process(H(2, "Intro"));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        var id = attrs["id"]!.GetValue<string>();
        Assert.Equal(10, id.Length);
        Assert.Matches("^h-[0-9a-f]{8}$", id);
        Assert.Equal(id, attrs["anchorId"]!.GetValue<string>());
    }

    // UTC06: Heading without attrs object -> attrs object created with id and anchorId
    [Fact]
    public void ProcessContent_HeadingWithoutAttrs_CreatesAttrsWithGeneratedId()
    {
        var json = "{\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"content\":[" + TextNode("No attrs") + "]}]}";

        var result = DocumentContentHelper.ProcessContent(Json(json));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Matches("^h-[0-9a-f]{8}$", attrs["id"]!.GetValue<string>());
        Assert.Equal(attrs["id"]!.GetValue<string>(), attrs["anchorId"]!.GetValue<string>());
        Assert.Null(attrs["level"]);
        Assert.Empty(result.Requirements);
    }

    // UTC07: Two headings without ids -> Each gets its own generated id
    [Fact]
    public void ProcessContent_TwoHeadingsWithoutIds_GetDistinctGeneratedIds()
    {
        var result = Process(H(1, "A"), H(1, "B"));

        var id1 = Heading(result.NormalizedContentJson, 0)["attrs"]!["id"]!.GetValue<string>();
        var id2 = Heading(result.NormalizedContentJson, 1)["attrs"]!["id"]!.GetValue<string>();
        Assert.NotEqual(id1, id2);
    }

    // UTC08: Heading nested inside another node (blockquote) -> Anchor ids ensured recursively
    [Fact]
    public void ProcessContent_NestedHeading_GetsAnchorIdRecursively()
    {
        var nested = "{\"type\":\"blockquote\",\"content\":[" + H(2, "Nested") + "]}";

        var result = Process(nested);

        var quote = Heading(result.NormalizedContentJson);
        var inner = (JsonObject)((JsonArray)quote["content"]!)[0]!;
        Assert.Matches("^h-[0-9a-f]{8}$", inner["attrs"]!["id"]!.GetValue<string>());
        Assert.Equal(inner["attrs"]!["id"]!.GetValue<string>(), inner["attrs"]!["anchorId"]!.GetValue<string>());
    }

    // UTC09: Nested heading is not extracted as a requirement (only top-level blocks) -> No requirements
    [Fact]
    public void ProcessContent_NestedRequirementHeading_IsNotExtracted()
    {
        var nested = "{\"type\":\"blockquote\",\"content\":[" + ReqH(2, "REQ-01 — Nested") + "]}";

        var result = Process(nested);

        Assert.Empty(result.Requirements);
    }

    // UTC10: Non-heading blocks are left unchanged -> Paragraph has no attrs added
    [Fact]
    public void ProcessContent_ParagraphBlock_IsNotModified()
    {
        var result = Process(P("Plain"));

        var para = Heading(result.NormalizedContentJson);
        Assert.Equal("paragraph", para["type"]!.GetValue<string>());
        Assert.Null(para["attrs"]);
    }

    // UTC11: Plain text from nested inline nodes -> Pieces joined with single space and trimmed
    [Fact]
    public void ProcessContent_PlainText_JoinsBlocksAndInlineChildrenWithSpace()
    {
        var para = "{\"type\":\"paragraph\",\"content\":[" + TextNode("foo") + "," + TextNode("bar") + "]}";

        var result = Process(para, P("baz"));

        Assert.Equal("foo bar baz", result.PlainText);
    }

    // UTC12: Requirement heading + sections in same doc -> Requirements extracted from same normalized blocks
    [Fact]
    public void ProcessContent_WithRequirementHeading_ReturnsRequirementAndNormalizesAttrs()
    {
        var result = Process(H(2, "REQ-01 — Login"), P("Body"));

        var req = Assert.Single(result.Requirements);
        Assert.Equal("REQ-01", req.ReqUid);
        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.True(attrs["req"]!.GetValue<bool>());
        Assert.Equal("REQ-01", attrs["reqUid"]!.GetValue<string>());
        Assert.Equal("MEDIUM", attrs["priority"]!.GetValue<string>());
        Assert.Equal(req.AnchorId, attrs["id"]!.GetValue<string>());
    }

    // UTC13: Existing priority attr -> Preserved (not overwritten with MEDIUM)
    [Fact]
    public void ProcessContent_RequirementWithPriority_KeepsExistingPriority()
    {
        var result = Process(H(2, "REQ-01 — Login", "\"priority\":\"HIGH\""));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Equal("HIGH", attrs["priority"]!.GetValue<string>());
    }

    // UTC14: Doc with extra root properties -> Properties preserved in normalized JSON
    [Fact]
    public void ProcessContent_RootWithExtraProperties_PreservesThem()
    {
        var json = "{\"type\":\"doc\",\"version\":3,\"content\":[]}";

        var result = DocumentContentHelper.ProcessContent(Json(json));

        var root = (JsonObject)JsonNode.Parse(result.NormalizedContentJson)!;
        Assert.Equal(3, root["version"]!.GetValue<int>());
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC15: Default JsonElement (GetRawText throws) -> Catch path returns empty doc, empty text, no requirements
    [Fact]
    public void ProcessContent_WhenJsonElementIsUndefined_ReturnsEmptyDoc()
    {
        var result = DocumentContentHelper.ProcessContent(default);

        Assert.Equal(EmptyDocJson, result.NormalizedContentJson);
        Assert.Equal(string.Empty, result.PlainText);
        Assert.Empty(result.Requirements);
    }

    // UTC16: Root is a JSON array (non-object) -> Replaced with empty doc
    [Fact]
    public void ProcessContent_WhenRootIsArray_ReturnsEmptyDoc()
    {
        var result = DocumentContentHelper.ProcessContent(Json("[1,2,3]"));

        Assert.Equal(EmptyDocJson, result.NormalizedContentJson);
        Assert.Equal(string.Empty, result.PlainText);
        Assert.Empty(result.Requirements);
    }

    // UTC17: Root is a JSON string -> Replaced with empty doc
    [Fact]
    public void ProcessContent_WhenRootIsString_ReturnsEmptyDoc()
    {
        var result = DocumentContentHelper.ProcessContent(Json("\"hello\""));

        Assert.Equal(EmptyDocJson, result.NormalizedContentJson);
    }

    // UTC18: Root is JSON number -> Replaced with empty doc
    [Fact]
    public void ProcessContent_WhenRootIsNumber_ReturnsEmptyDoc()
    {
        var result = DocumentContentHelper.ProcessContent(Json("42"));

        Assert.Equal(EmptyDocJson, result.NormalizedContentJson);
    }

    // UTC19: Root is JSON null (Parse returns null) -> Replaced with empty doc
    [Fact]
    public void ProcessContent_WhenRootIsNull_ReturnsEmptyDoc()
    {
        var result = DocumentContentHelper.ProcessContent(Json("null"));

        Assert.Equal(EmptyDocJson, result.NormalizedContentJson);
        Assert.Empty(result.Requirements);
    }

    // UTC20: Object root without content property -> content: [] added, other props kept
    [Fact]
    public void ProcessContent_WhenContentMissing_AddsEmptyContentArray()
    {
        var result = DocumentContentHelper.ProcessContent(Json("{\"type\":\"doc\"}"));

        var root = (JsonObject)JsonNode.Parse(result.NormalizedContentJson)!;
        Assert.Equal("doc", root["type"]!.GetValue<string>());
        Assert.Empty((JsonArray)root["content"]!);
        Assert.Equal(string.Empty, result.PlainText);
        Assert.Empty(result.Requirements);
    }

    // UTC21: content is not an array (object) -> Replaced by empty array
    [Fact]
    public void ProcessContent_WhenContentIsObject_ReplacesWithEmptyArray()
    {
        var result = DocumentContentHelper.ProcessContent(Json("{\"type\":\"doc\",\"content\":{\"a\":1}}"));

        var root = (JsonObject)JsonNode.Parse(result.NormalizedContentJson)!;
        Assert.Empty((JsonArray)root["content"]!);
        Assert.Empty(result.Requirements);
    }

    // UTC22: Empty object root -> content: [] added
    [Fact]
    public void ProcessContent_WhenRootIsEmptyObject_ReturnsDocWithEmptyContent()
    {
        var result = DocumentContentHelper.ProcessContent(Json("{}"));

        Assert.Equal("{\"content\":[]}", result.NormalizedContentJson);
        Assert.Equal(string.Empty, result.PlainText);
    }

    // UTC23: Non-object entries in content (string/number/null) -> Skipped without error
    [Fact]
    public void ProcessContent_WhenContentHasNonObjectEntries_SkipsThem()
    {
        var json = "{\"type\":\"doc\",\"content\":[\"text\",5,null," + P("ok") + "]}";

        var result = DocumentContentHelper.ProcessContent(Json(json));

        Assert.Equal("ok", result.PlainText);
        Assert.Empty(result.Requirements);
    }

    // UTC24: Heading whose attrs.id is whitespace only -> Replaced by generated id
    [Fact]
    public void ProcessContent_HeadingWithWhitespaceId_GeneratesNewId()
    {
        var result = Process(H(1, "A", "\"id\":\"   \""));

        var id = Heading(result.NormalizedContentJson)["attrs"]!["id"]!.GetValue<string>();
        Assert.Matches("^h-[0-9a-f]{8}$", id);
    }

    // UTC25: Heading whose attrs.id is empty string -> Replaced by generated id
    [Fact]
    public void ProcessContent_HeadingWithEmptyId_GeneratesNewId()
    {
        var result = Process(H(1, "A", "\"id\":\"\""));

        var id = Heading(result.NormalizedContentJson)["attrs"]!["id"]!.GetValue<string>();
        Assert.Matches("^h-[0-9a-f]{8}$", id);
    }

    // UTC26: Heading attrs.id is a number (not string) -> Throws InvalidOperationException (current behaviour, not caught)
    [Fact]
    public void ProcessContent_HeadingWithNumericId_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => Process(H(1, "A", "\"id\":123")));
    }

    // UTC27: Block with non-string type -> Throws InvalidOperationException (current behaviour, not caught)
    [Fact]
    public void ProcessContent_BlockWithNumericType_ThrowsInvalidOperationException()
    {
        var json = "{\"type\":\"doc\",\"content\":[{\"type\":5}]}";

        Assert.Throws<InvalidOperationException>(() => DocumentContentHelper.ProcessContent(Json(json)));
    }

    // UTC28: Heading with attrs not an object (string) -> attrs replaced by new object with generated id
    [Fact]
    public void ProcessContent_HeadingWithNonObjectAttrs_ReplacesAttrs()
    {
        var json = "{\"type\":\"doc\",\"content\":[{\"type\":\"heading\",\"attrs\":\"oops\",\"content\":[" + TextNode("X") + "]}]}";

        var result = DocumentContentHelper.ProcessContent(Json(json));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Matches("^h-[0-9a-f]{8}$", attrs["id"]!.GetValue<string>());
    }

    // UTC29: Doc with only whitespace text -> PlainText is trimmed to empty
    [Fact]
    public void ProcessContent_WhenOnlyWhitespaceText_PlainTextIsEmpty()
    {
        var result = Process(P("   "));

        Assert.Equal(string.Empty, result.PlainText);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Anchor id length: 35, 36, 37)
    // =========================================================================

    // UTC30: Anchor id length 35 -> Preserved
    [Fact]
    public void ProcessContent_WhenAnchorIdLengthIs35_Preserved()
    {
        var id = new string('a', 35);

        var result = Process(H(1, "A", "\"id\":\"" + id + "\""));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Equal(id, attrs["id"]!.GetValue<string>());
        Assert.Equal(id, attrs["anchorId"]!.GetValue<string>());
    }

    // UTC31: Anchor id length 36 -> Preserved (no truncation)
    [Fact]
    public void ProcessContent_WhenAnchorIdLengthIs36_Preserved()
    {
        var id = new string('b', 36);

        var result = Process(H(1, "A", "\"id\":\"" + id + "\""));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Equal(id, attrs["id"]!.GetValue<string>());
        Assert.Equal(id, attrs["anchorId"]!.GetValue<string>());
    }

    // UTC32: Anchor id length 37 -> Truncated to 36 in both id and anchorId
    [Fact]
    public void ProcessContent_WhenAnchorIdLengthIs37_TruncatedTo36()
    {
        var id = new string('c', 36) + "X";

        var result = Process(H(1, "A", "\"id\":\"" + id + "\""));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Equal(new string('c', 36), attrs["id"]!.GetValue<string>());
        Assert.Equal(new string('c', 36), attrs["anchorId"]!.GetValue<string>());
    }

    // UTC33: Long anchor id supplied via anchorId only (length 100) -> Truncated to 36
    [Fact]
    public void ProcessContent_WhenAnchorIdAttrIsVeryLong_TruncatedTo36()
    {
        var result = Process(H(1, "A", "\"anchorId\":\"" + new string('d', 100) + "\""));

        var attrs = (JsonObject)Heading(result.NormalizedContentJson)["attrs"]!;
        Assert.Equal(new string('d', 36), attrs["id"]!.GetValue<string>());
    }

    // UTC34: Single-character anchor id -> Preserved
    [Fact]
    public void ProcessContent_WhenAnchorIdLengthIs1_Preserved()
    {
        var result = Process(H(1, "A", "\"id\":\"z\""));

        Assert.Equal("z", Heading(result.NormalizedContentJson)["attrs"]!["id"]!.GetValue<string>());
    }

    // UTC35: Truncated anchor id flows into requirement AnchorId (37 -> 36)
    [Fact]
    public void ProcessContent_RequirementWith37CharAnchorId_RequirementAnchorIdIs36()
    {
        var id = new string('e', 37);

        var result = Process(H(2, "REQ-01 — T", "\"id\":\"" + id + "\""));

        var req = Assert.Single(result.Requirements);
        Assert.Equal(new string('e', 36), req.AnchorId);
    }

    // UTC36: Empty content array -> Empty plain text and no requirements, content stays []
    [Fact]
    public void ProcessContent_WhenContentArrayEmpty_ReturnsEmptyResults()
    {
        var result = Process();

        Assert.Equal(EmptyDocJson, result.NormalizedContentJson);
        Assert.Equal(string.Empty, result.PlainText);
        Assert.Empty(result.Requirements);
    }
}
