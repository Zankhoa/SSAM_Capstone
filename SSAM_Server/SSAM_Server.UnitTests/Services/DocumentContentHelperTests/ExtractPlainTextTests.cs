using System.Text.Json.Nodes;
using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

public class ExtractPlainTextTests : DocumentContentHelperTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Text node -> Its text
    [Fact]
    public void ExtractPlainText_WithTextNode_ReturnsText()
    {
        Assert.Equal("hello", DocumentContentHelper.ExtractPlainText(Obj(TextNode("hello"))));
    }

    // UTC02: Paragraph with one text child -> Text
    [Fact]
    public void ExtractPlainText_WithParagraph_ReturnsText()
    {
        Assert.Equal("abc", DocumentContentHelper.ExtractPlainText(Obj(P("abc"))));
    }

    // UTC03: Paragraph with two text children -> Joined with a single space
    [Fact]
    public void ExtractPlainText_WithTwoChildren_JoinsWithSpace()
    {
        var node = Obj("{\"type\":\"paragraph\",\"content\":[" + TextNode("a") + "," + TextNode("b") + "]}");

        Assert.Equal("a b", DocumentContentHelper.ExtractPlainText(node));
    }

    // UTC04: Doc with nested blocks -> Depth-first joined with spaces
    [Fact]
    public void ExtractPlainText_WithNestedBlocks_JoinsDepthFirst()
    {
        var node = Obj(Doc(H(1, "Title"), P("one"), "{\"type\":\"bulletList\",\"content\":[{\"type\":\"listItem\",\"content\":[" + P("two") + "]}]}"));

        Assert.Equal("Title one two", DocumentContentHelper.ExtractPlainText(node));
    }

    // UTC05: Text node with content array -> Own text wins, children ignored
    [Fact]
    public void ExtractPlainText_TextNodeWithContent_IgnoresChildren()
    {
        var node = Obj("{\"type\":\"text\",\"text\":\"own\",\"content\":[" + TextNode("child") + "]}");

        Assert.Equal("own", DocumentContentHelper.ExtractPlainText(node));
    }

    // UTC06: Text containing internal whitespace -> Preserved as is
    [Fact]
    public void ExtractPlainText_PreservesInternalWhitespaceOfText()
    {
        Assert.Equal("  a   b  ", DocumentContentHelper.ExtractPlainText(Obj(TextNode("  a   b  "))));
    }

    // UTC07: Node without type but with content -> Children joined
    [Fact]
    public void ExtractPlainText_NodeWithoutType_JoinsChildren()
    {
        var node = Obj("{\"content\":[" + TextNode("x") + "," + TextNode("y") + "]}");

        Assert.Equal("x y", DocumentContentHelper.ExtractPlainText(node));
    }

    // UTC08: Unicode text -> Preserved
    [Fact]
    public void ExtractPlainText_WithUnicode_Preserved()
    {
        Assert.Equal("Tiếng Việt 😀", DocumentContentHelper.ExtractPlainText(Obj(TextNode("Tiếng Việt 😀"))));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC09: Null node -> Empty
    [Fact]
    public void ExtractPlainText_WithNull_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(null));
    }

    // UTC10: JsonValue node (string) -> Empty
    [Fact]
    public void ExtractPlainText_WithJsonValue_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(JsonValue.Create("text")));
    }

    // UTC11: JsonArray node -> Empty (not an object)
    [Fact]
    public void ExtractPlainText_WithJsonArray_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(new JsonArray(Obj(TextNode("a")))));
    }

    // UTC12: Text node without text property -> Empty
    [Fact]
    public void ExtractPlainText_TextNodeWithoutText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(Obj("{\"type\":\"text\"}")));
    }

    // UTC13: Text node with null text -> Empty
    [Fact]
    public void ExtractPlainText_TextNodeWithNullText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(Obj("{\"type\":\"text\",\"text\":null}")));
    }

    // UTC14: Node without content -> Empty
    [Fact]
    public void ExtractPlainText_WithoutContent_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(Obj("{\"type\":\"paragraph\"}")));
    }

    // UTC15: Node with empty content array -> Empty
    [Fact]
    public void ExtractPlainText_WithEmptyContent_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(Obj("{\"type\":\"paragraph\",\"content\":[]}")));
    }

    // UTC16: content is not an array -> Empty
    [Fact]
    public void ExtractPlainText_WithContentNotArray_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(Obj("{\"type\":\"paragraph\",\"content\":\"x\"}")));
    }

    // UTC17: Empty children are skipped when joining -> No leading/double spaces
    [Fact]
    public void ExtractPlainText_SkipsEmptyChildren()
    {
        var node = Obj("{\"type\":\"paragraph\",\"content\":[{\"type\":\"hardBreak\"}," + TextNode("a") + ",{\"type\":\"image\"}," + TextNode("b") + "]}");

        Assert.Equal("a b", DocumentContentHelper.ExtractPlainText(node));
    }

    // UTC18: Non-object children -> Skipped
    [Fact]
    public void ExtractPlainText_SkipsNonObjectChildren()
    {
        var node = Obj("{\"type\":\"paragraph\",\"content\":[\"s\",5,null," + TextNode("a") + "]}");

        Assert.Equal("a", DocumentContentHelper.ExtractPlainText(node));
    }

    // UTC19: Non-string type -> Throws InvalidOperationException (current behaviour)
    [Fact]
    public void ExtractPlainText_WithNumericType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DocumentContentHelper.ExtractPlainText(Obj("{\"type\":1}")));
    }

    // UTC20: Text node with numeric text -> Throws InvalidOperationException (current behaviour)
    [Fact]
    public void ExtractPlainText_TextNodeWithNumericText_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DocumentContentHelper.ExtractPlainText(Obj("{\"type\":\"text\",\"text\":5}")));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC21: Empty text -> Empty
    [Fact]
    public void ExtractPlainText_WithEmptyText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.ExtractPlainText(Obj(TextNode(""))));
    }

    // UTC22: Single child -> No separator added
    [Fact]
    public void ExtractPlainText_WithSingleChild_NoSeparator()
    {
        Assert.Equal("only", DocumentContentHelper.ExtractPlainText(Obj(P("only"))));
    }

    // UTC23: Whitespace-only text child is kept (non-empty) -> Joined with spaces
    [Fact]
    public void ExtractPlainText_WhitespaceChildIsKept()
    {
        var node = Obj("{\"type\":\"paragraph\",\"content\":[" + TextNode("a") + "," + TextNode(" ") + "," + TextNode("b") + "]}");

        Assert.Equal("a   b", DocumentContentHelper.ExtractPlainText(node));
    }

    // UTC24: Deeply nested (30 levels) -> Text reached
    [Fact]
    public void ExtractPlainText_DeeplyNested_ReturnsInnerText()
    {
        var json = TextNode("deep");
        for (var i = 0; i < 30; i++) json = "{\"type\":\"blockquote\",\"content\":[" + json + "]}";

        Assert.Equal("deep", DocumentContentHelper.ExtractPlainText(Obj(json)));
    }
}
