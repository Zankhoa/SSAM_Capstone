using System.Text.Json;
using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>NormalizeMermaidBlocksForEditor: converts mermaidBlock nodes into mermaid codeBlock nodes.</summary>
public class NormalizeMermaidBlocksForEditorTests : DocumentContentHelperTestBase
{
    private static JsonElement Block(JsonElement root, int index) => root.GetProperty("content")[index];

    // UTC01: mermaidBlock with attrs.code -> replaced by codeBlock(mermaid) with that code
    [Fact]
    public void NormalizeMermaidBlocksForEditor_BlockWithCode_ConvertsToCodeBlock()
    {
        var input = Json(Doc("{\"type\":\"mermaidBlock\",\"attrs\":{\"code\":\"graph TD\"}}"));

        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(input);

        var b = Block(result, 0);
        Assert.Equal("codeBlock", b.GetProperty("type").GetString());
        Assert.Equal("mermaid", b.GetProperty("attrs").GetProperty("language").GetString());
        Assert.Equal("graph TD", b.GetProperty("content")[0].GetProperty("text").GetString());
    }

    // UTC02: mermaidBlock with only attrs.mermaid -> falls back to attrs.mermaid
    [Fact]
    public void NormalizeMermaidBlocksForEditor_BlockWithMermaidAttr_UsesMermaidAttr()
    {
        var input = Json(Doc("{\"type\":\"mermaidBlock\",\"attrs\":{\"mermaid\":\"flowchart LR\"}}"));

        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(input);

        Assert.Equal("flowchart LR", Block(result, 0).GetProperty("content")[0].GetProperty("text").GetString());
    }

    // UTC03: mermaidBlock without attrs but with text content -> falls back to plain text
    [Fact]
    public void NormalizeMermaidBlocksForEditor_BlockWithoutAttrs_UsesPlainText()
    {
        var input = Json(Doc("{\"type\":\"mermaidBlock\",\"content\":[" + TextNode("sequenceDiagram") + "]}"));

        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(input);

        var b = Block(result, 0);
        Assert.Equal("codeBlock", b.GetProperty("type").GetString());
        Assert.Equal("sequenceDiagram", b.GetProperty("content")[0].GetProperty("text").GetString());
    }

    // UTC04: attrs.code takes precedence over attrs.mermaid
    [Fact]
    public void NormalizeMermaidBlocksForEditor_BothCodeAndMermaid_PrefersCode()
    {
        var input = Json(Doc("{\"type\":\"mermaidBlock\",\"attrs\":{\"code\":\"A\",\"mermaid\":\"B\"}}"));

        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(input);

        Assert.Equal("A", Block(result, 0).GetProperty("content")[0].GetProperty("text").GetString());
    }

    // UTC05: non-mermaid blocks are left untouched and order is preserved
    [Fact]
    public void NormalizeMermaidBlocksForEditor_MixedBlocks_OnlyMermaidConverted()
    {
        var input = Json(Doc(P("before"), "{\"type\":\"mermaidBlock\",\"attrs\":{\"code\":\"g\"}}", P("after")));

        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(input);

        Assert.Equal(3, result.GetProperty("content").GetArrayLength());
        Assert.Equal("paragraph", Block(result, 0).GetProperty("type").GetString());
        Assert.Equal("codeBlock", Block(result, 1).GetProperty("type").GetString());
        Assert.Equal("paragraph", Block(result, 2).GetProperty("type").GetString());
    }

    // UTC06: multiple mermaid blocks -> all converted
    [Fact]
    public void NormalizeMermaidBlocksForEditor_MultipleMermaidBlocks_AllConverted()
    {
        var m = "{\"type\":\"mermaidBlock\",\"attrs\":{\"code\":\"x\"}}";
        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(Json(Doc(m, m)));

        Assert.Equal("codeBlock", Block(result, 0).GetProperty("type").GetString());
        Assert.Equal("codeBlock", Block(result, 1).GetProperty("type").GetString());
    }

    // UTC07: document without mermaid blocks -> content unchanged
    [Fact]
    public void NormalizeMermaidBlocksForEditor_NoMermaidBlocks_ReturnsEquivalentDocument()
    {
        var input = Json(Doc(P("hello")));

        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(input);

        Assert.Equal(input.GetRawText(), result.GetRawText());
    }

    // UTC08: root without a content array -> returns clone of input
    [Fact]
    public void NormalizeMermaidBlocksForEditor_NoContentArray_ReturnsClone()
    {
        var input = Json("{\"type\":\"doc\"}");

        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(input);

        Assert.Equal("{\"type\":\"doc\"}", result.GetRawText());
    }

    // UTC09: empty content array -> returns empty content
    [Fact]
    public void NormalizeMermaidBlocksForEditor_EmptyContent_ReturnsEmptyContent()
    {
        var result = DocumentContentHelper.NormalizeMermaidBlocksForEditor(Json(Doc()));

        Assert.Equal(0, result.GetProperty("content").GetArrayLength());
    }
}
