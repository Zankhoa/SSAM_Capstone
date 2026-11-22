using System.Text.Json.Nodes;
using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>MakeHeading / MakeReqHeading / MakeParagraph / MakeBulletList / MakeTaskList.</summary>
public class MakeBuildersTests
{
    private static string Text(JsonNode? node) => node!["text"]!.GetValue<string>();

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: MakeHeading with explicit anchor id -> Heading with level, id, anchorId and text child
    [Fact]
    public void MakeHeading_WithAnchorId_ReturnsHeadingNode()
    {
        var h = DocumentContentHelper.MakeHeading(2, "Title", "anc-1");

        Assert.Equal("heading", h["type"]!.GetValue<string>());
        Assert.Equal(2, h["attrs"]!["level"]!.GetValue<int>());
        Assert.Equal("anc-1", h["attrs"]!["id"]!.GetValue<string>());
        Assert.Equal("anc-1", h["attrs"]!["anchorId"]!.GetValue<string>());
        Assert.Null(h["attrs"]!["req"]);
        var child = Assert.Single((JsonArray)h["content"]!);
        Assert.Equal("text", child!["type"]!.GetValue<string>());
        Assert.Equal("Title", Text(child));
    }

    // UTC02: MakeHeading without anchor id -> Generated "h-" + 8 hex chars mirrored in id/anchorId
    [Fact]
    public void MakeHeading_WithoutAnchorId_GeneratesId()
    {
        var h = DocumentContentHelper.MakeHeading(1, "T");

        var id = h["attrs"]!["id"]!.GetValue<string>();
        Assert.Matches("^h-[0-9a-f]{8}$", id);
        Assert.Equal(id, h["attrs"]!["anchorId"]!.GetValue<string>());
    }

    // UTC03: MakeHeading generates unique ids across calls
    [Fact]
    public void MakeHeading_TwoCalls_GenerateDifferentIds()
    {
        var a = DocumentContentHelper.MakeHeading(1, "T")["attrs"]!["id"]!.GetValue<string>();
        var b = DocumentContentHelper.MakeHeading(1, "T")["attrs"]!["id"]!.GetValue<string>();

        Assert.NotEqual(a, b);
    }

    // UTC04: MakeReqHeading defaults -> req true, priority MEDIUM, reqUid set
    [Fact]
    public void MakeReqHeading_WithDefaults_ReturnsRequirementHeading()
    {
        var h = DocumentContentHelper.MakeReqHeading(2, "REQ-01 — X", "REQ-01");

        Assert.Equal("heading", h["type"]!.GetValue<string>());
        var attrs = h["attrs"]!;
        Assert.Equal(2, attrs["level"]!.GetValue<int>());
        Assert.True(attrs["req"]!.GetValue<bool>());
        Assert.Equal("REQ-01", attrs["reqUid"]!.GetValue<string>());
        Assert.Equal("MEDIUM", attrs["priority"]!.GetValue<string>());
        Assert.Matches("^h-[0-9a-f]{8}$", attrs["id"]!.GetValue<string>());
        Assert.Equal(attrs["id"]!.GetValue<string>(), attrs["anchorId"]!.GetValue<string>());
        Assert.Equal("REQ-01 — X", Text(((JsonArray)h["content"]!)[0]));
    }

    // UTC05: MakeReqHeading with explicit priority and anchor -> Used
    [Fact]
    public void MakeReqHeading_WithPriorityAndAnchor_UsesThem()
    {
        var h = DocumentContentHelper.MakeReqHeading(3, "T", "R-1", "HIGH", "my-anchor");

        Assert.Equal("HIGH", h["attrs"]!["priority"]!.GetValue<string>());
        Assert.Equal("my-anchor", h["attrs"]!["id"]!.GetValue<string>());
        Assert.Equal("my-anchor", h["attrs"]!["anchorId"]!.GetValue<string>());
        Assert.Equal(3, h["attrs"]!["level"]!.GetValue<int>());
    }

    // UTC06: MakeParagraph with text -> Paragraph with one text child
    [Fact]
    public void MakeParagraph_WithText_ReturnsParagraphWithTextChild()
    {
        var p = DocumentContentHelper.MakeParagraph("Hello");

        Assert.Equal("paragraph", p["type"]!.GetValue<string>());
        var child = Assert.Single((JsonArray)p["content"]!);
        Assert.Equal("text", child!["type"]!.GetValue<string>());
        Assert.Equal("Hello", Text(child));
        Assert.Null(p["attrs"]);
    }

    // UTC07: MakeBulletList with items -> bulletList of listItem > paragraph in order
    [Fact]
    public void MakeBulletList_WithItems_ReturnsListItemsInOrder()
    {
        var list = DocumentContentHelper.MakeBulletList(new[] { "a", "b", "c" });

        Assert.Equal("bulletList", list["type"]!.GetValue<string>());
        var items = (JsonArray)list["content"]!;
        Assert.Equal(3, items.Count);
        var texts = new List<string>();
        foreach (var item in items)
        {
            Assert.Equal("listItem", item!["type"]!.GetValue<string>());
            var para = Assert.Single((JsonArray)item["content"]!);
            Assert.Equal("paragraph", para!["type"]!.GetValue<string>());
            texts.Add(Text(((JsonArray)para["content"]!)[0]));
        }
        Assert.Equal(new[] { "a", "b", "c" }, texts.ToArray());
    }

    // UTC08: MakeTaskList with items -> taskList of unchecked taskItem > paragraph in order
    [Fact]
    public void MakeTaskList_WithItems_ReturnsUncheckedTaskItems()
    {
        var list = DocumentContentHelper.MakeTaskList(new[] { "x", "y" });

        Assert.Equal("taskList", list["type"]!.GetValue<string>());
        var items = (JsonArray)list["content"]!;
        Assert.Equal(2, items.Count);
        var texts = new List<string>();
        foreach (var item in items)
        {
            Assert.Equal("taskItem", item!["type"]!.GetValue<string>());
            Assert.False(item["attrs"]!["checked"]!.GetValue<bool>());
            var para = Assert.Single((JsonArray)item["content"]!);
            texts.Add(Text(((JsonArray)para!["content"]!)[0]));
        }
        Assert.Equal(new[] { "x", "y" }, texts.ToArray());
    }

    // UTC09: MakeTaskList output is read back as acceptance criteria by ProcessContent
    [Fact]
    public void MakeTaskList_IsReadAsAcceptanceCriteria()
    {
        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["content"] = new JsonArray
            {
                DocumentContentHelper.MakeReqHeading(2, "REQ-01 — T", "REQ-01"),
                DocumentContentHelper.MakeTaskList(new[] { "one", "two" })
            }
        };

        using var parsed = System.Text.Json.JsonDocument.Parse(doc.ToJsonString());
        var req = Assert.Single(DocumentContentHelper.ProcessContent(parsed.RootElement).Requirements);

        Assert.Equal(new[] { "one", "two" }, req.AcceptanceCriteria.ToArray());
    }

    // UTC10: MakeHeading text with special characters -> Preserved
    [Fact]
    public void MakeHeading_WithSpecialCharacters_Preserved()
    {
        var h = DocumentContentHelper.MakeHeading(1, "Tiếng \"Việt\" <b> & 😀");

        Assert.Equal("Tiếng \"Việt\" <b> & 😀", Text(((JsonArray)h["content"]!)[0]));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC11: MakeParagraph with empty string -> Paragraph with empty content array
    [Fact]
    public void MakeParagraph_WithEmptyString_ReturnsEmptyContent()
    {
        var p = DocumentContentHelper.MakeParagraph("");

        Assert.Equal("paragraph", p["type"]!.GetValue<string>());
        Assert.Empty((JsonArray)p["content"]!);
    }

    // UTC12: MakeParagraph with null -> Paragraph with empty content array
    [Fact]
    public void MakeParagraph_WithNull_ReturnsEmptyContent()
    {
        var p = DocumentContentHelper.MakeParagraph(null!);

        Assert.Empty((JsonArray)p["content"]!);
    }

    // UTC13: MakeParagraph with whitespace -> Text child preserved (not treated as empty)
    [Fact]
    public void MakeParagraph_WithWhitespace_KeepsTextChild()
    {
        var p = DocumentContentHelper.MakeParagraph("  ");

        Assert.Equal("  ", Text(((JsonArray)p["content"]!)[0]));
    }

    // UTC14: MakeHeading with empty text -> Text child with empty string
    [Fact]
    public void MakeHeading_WithEmptyText_KeepsEmptyTextChild()
    {
        var h = DocumentContentHelper.MakeHeading(1, "");

        Assert.Equal("", Text(((JsonArray)h["content"]!)[0]));
    }

    // UTC15: MakeHeading with null anchor and empty text -> Still generates id
    [Fact]
    public void MakeHeading_WithEmptyTextAndNullAnchor_GeneratesId()
    {
        var h = DocumentContentHelper.MakeHeading(1, "", null);

        Assert.Matches("^h-[0-9a-f]{8}$", h["attrs"]!["id"]!.GetValue<string>());
    }

    // UTC16: MakeHeading with empty-string anchor -> Used as is (not replaced)
    [Fact]
    public void MakeHeading_WithEmptyStringAnchor_UsedAsIs()
    {
        var h = DocumentContentHelper.MakeHeading(1, "T", "");

        Assert.Equal("", h["attrs"]!["id"]!.GetValue<string>());
        Assert.Equal("", h["attrs"]!["anchorId"]!.GetValue<string>());
    }

    // UTC17: MakeReqHeading with empty reqUid -> Stored as empty string
    [Fact]
    public void MakeReqHeading_WithEmptyReqUid_StoredAsEmpty()
    {
        var h = DocumentContentHelper.MakeReqHeading(2, "T", "");

        Assert.Equal("", h["attrs"]!["reqUid"]!.GetValue<string>());
    }

    // UTC18: MakeBulletList with no items -> Empty content array
    [Fact]
    public void MakeBulletList_WithNoItems_ReturnsEmptyContent()
    {
        var list = DocumentContentHelper.MakeBulletList(Array.Empty<string>());

        Assert.Equal("bulletList", list["type"]!.GetValue<string>());
        Assert.Empty((JsonArray)list["content"]!);
    }

    // UTC19: MakeTaskList with no items -> Empty content array
    [Fact]
    public void MakeTaskList_WithNoItems_ReturnsEmptyContent()
    {
        var list = DocumentContentHelper.MakeTaskList(Array.Empty<string>());

        Assert.Equal("taskList", list["type"]!.GetValue<string>());
        Assert.Empty((JsonArray)list["content"]!);
    }

    // UTC20: MakeBulletList with an empty item -> listItem with empty paragraph
    [Fact]
    public void MakeBulletList_WithEmptyItem_ReturnsEmptyParagraph()
    {
        var list = DocumentContentHelper.MakeBulletList(new[] { "" });

        var item = Assert.Single((JsonArray)list["content"]!);
        var para = Assert.Single((JsonArray)item!["content"]!);
        Assert.Empty((JsonArray)para!["content"]!);
    }

    // UTC21: MakeTaskList with an empty item -> taskItem with empty paragraph
    [Fact]
    public void MakeTaskList_WithEmptyItem_ReturnsEmptyParagraph()
    {
        var list = DocumentContentHelper.MakeTaskList(new[] { "" });

        var item = Assert.Single((JsonArray)list["content"]!);
        var para = Assert.Single((JsonArray)item!["content"]!);
        Assert.Empty((JsonArray)para!["content"]!);
    }

    // UTC22: MakeBulletList with null items -> Throws ArgumentNullException (foreach over null)
    [Fact]
    public void MakeBulletList_WithNullItems_Throws()
    {
        Assert.ThrowsAny<Exception>(() => DocumentContentHelper.MakeBulletList(null!));
    }

    // UTC23: MakeTaskList with null items -> Throws
    [Fact]
    public void MakeTaskList_WithNullItems_Throws()
    {
        Assert.ThrowsAny<Exception>(() => DocumentContentHelper.MakeTaskList(null!));
    }

    // =========================================================================
    // 3. BOUNDARY CASES (heading level 0, 1, 6, negative)
    // =========================================================================

    // UTC24: MakeHeading level 0 -> Stored as 0 (no validation)
    // UTC25: Level 1
    // UTC26: Level 6
    // UTC27: Level 7
    // UTC28: Level -1
    [Theory]
    [InlineData(0)]   // UTC24
    [InlineData(1)]   // UTC25
    [InlineData(6)]   // UTC26
    [InlineData(7)]   // UTC27
    [InlineData(-1)]  // UTC28
    public void MakeHeading_WithLevel_StoresLevelAsIs(int level)
    {
        var h = DocumentContentHelper.MakeHeading(level, "T", "a");

        Assert.Equal(level, h["attrs"]!["level"]!.GetValue<int>());
    }

    // UTC29: MakeReqHeading level boundary values stored as is
    [Theory]
    [InlineData(0)]   // UTC29
    [InlineData(1)]   // UTC30
    [InlineData(4)]   // UTC31
    public void MakeReqHeading_WithLevel_StoresLevelAsIs(int level)
    {
        var h = DocumentContentHelper.MakeReqHeading(level, "T", "R");

        Assert.Equal(level, h["attrs"]!["level"]!.GetValue<int>());
    }

    // UTC32: MakeBulletList with a single item -> One listItem
    [Fact]
    public void MakeBulletList_WithSingleItem_ReturnsOneListItem()
    {
        Assert.Single((JsonArray)DocumentContentHelper.MakeBulletList(new[] { "only" })["content"]!);
    }

    // UTC33: MakeTaskList with a single item -> One taskItem
    [Fact]
    public void MakeTaskList_WithSingleItem_ReturnsOneTaskItem()
    {
        Assert.Single((JsonArray)DocumentContentHelper.MakeTaskList(new[] { "only" })["content"]!);
    }

    // UTC34: MakeTaskList with 1000 items -> All created in order
    [Fact]
    public void MakeTaskList_WithManyItems_CreatesAll()
    {
        var items = Enumerable.Range(0, 1000).Select(i => "i" + i).ToArray();

        var list = DocumentContentHelper.MakeTaskList(items);

        var arr = (JsonArray)list["content"]!;
        Assert.Equal(1000, arr.Count);
        Assert.Equal("i999", Text(((JsonArray)((JsonArray)arr[999]!["content"]!)[0]!["content"]!)[0]));
    }

    // UTC35: MakeBulletList accepts lazily evaluated enumerable -> Enumerated once
    [Fact]
    public void MakeBulletList_WithLazyEnumerable_EnumeratesOnce()
    {
        var enumerations = 0;
        IEnumerable<string> Gen()
        {
            enumerations++;
            yield return "a";
            yield return "b";
        }

        var list = DocumentContentHelper.MakeBulletList(Gen());

        Assert.Equal(2, ((JsonArray)list["content"]!).Count);
        Assert.Equal(1, enumerations);
    }
}
