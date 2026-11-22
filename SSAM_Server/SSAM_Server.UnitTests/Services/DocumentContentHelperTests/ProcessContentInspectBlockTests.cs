namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>ProcessContent: per-block inspection of a requirement section (InspectBlock + HasFigmaLinkInMarks).</summary>
public class ProcessContentInspectBlockTests : DocumentContentHelperTestBase
{
    private static string Blk(string type, string? attrs = null, string? content = null)
        => "{\"type\":\"" + type + "\""
           + (attrs == null ? "" : ",\"attrs\":" + attrs)
           + (content == null ? "" : ",\"content\":" + content)
           + "}";

    private static string LinkText(string text, string href)
        => "{\"type\":\"text\",\"text\":" + Quote(text) + ",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":" + Quote(href) + "}}]}";

    private static string ParaOf(params string[] inline)
        => "{\"type\":\"paragraph\",\"content\":[" + string.Join(",", inline) + "]}";

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: taskList with two items -> Acceptance criteria collected in order, not added to body
    [Fact]
    public void InspectBlock_TaskList_CollectsAcceptanceCriteria()
    {
        var req = ExtractOne(TaskList("Check input", "Save   record"));

        Assert.Equal(new[] { "Check input", "Save record" }, req.AcceptanceCriteria.ToArray());
        Assert.Equal(string.Empty, req.BodyPreview);
        Assert.False(req.HasTable);
    }

    // UTC02: table block -> HasTable true, text added to body
    [Fact]
    public void InspectBlock_Table_SetsHasTableAndAddsTextToBody()
    {
        var cell = "{\"type\":\"tableCell\",\"content\":[" + P("A1") + "]}";
        var cell2 = "{\"type\":\"tableCell\",\"content\":[" + P("B1") + "]}";
        var table = Blk("table", null, "[{\"type\":\"tableRow\",\"content\":[" + cell + "," + cell2 + "]}]");

        var req = ExtractOne(table);

        Assert.True(req.HasTable);
        Assert.Equal("A1 B1", req.BodyPreview);
        Assert.Empty(req.Constraints);
    }

    // UTC03: constraints block with one row -> HasTable true, constraint row mapped, not in body
    [Fact]
    public void InspectBlock_Constraints_MapsRowAndSetsHasTable()
    {
        var rows = "[{\"field\":\"Email\",\"kind\":\"email\",\"required\":true,\"min\":1,\"max\":50,\"lettersAndDigits\":true,\"options\":[\"a\",\"b\"],\"message\":\"Invalid\"}]";

        var req = ExtractOne(Constraints(rows));

        Assert.True(req.HasTable);
        var row = Assert.Single(req.Constraints);
        Assert.Equal("Email", row.Field);
        Assert.Equal("email", row.Kind);
        Assert.True(row.Required);
        Assert.Equal(1d, row.Min);
        Assert.Equal(50d, row.Max);
        Assert.True(row.LettersAndDigits);
        Assert.Equal(new[] { "a", "b" }, row.Options.ToArray());
        Assert.Equal("Invalid", row.Message);
        Assert.Equal(string.Empty, req.BodyPreview);
    }

    // UTC04: mermaidBlock with attrs.code -> HasDiagram true, not in body
    [Fact]
    public void InspectBlock_MermaidBlockWithCode_SetsHasDiagram()
    {
        var req = ExtractOne(Blk("mermaidBlock", "{\"code\":\"graph TD; A-->B\"}"));

        Assert.True(req.HasDiagram);
        Assert.Equal(string.Empty, req.BodyPreview);
    }

    // UTC05: diagram block with attrs.mermaid -> HasDiagram true
    [Fact]
    public void InspectBlock_DiagramWithMermaidAttr_SetsHasDiagram()
    {
        var req = ExtractOne(Blk("diagram", "{\"mermaid\":\"graph TD; A-->B\"}"));

        Assert.True(req.HasDiagram);
    }

    // UTC06: mermaidBlock without attrs but with text content -> Falls back to plain text, HasDiagram true
    [Fact]
    public void InspectBlock_MermaidBlockWithTextFallback_SetsHasDiagram()
    {
        var req = ExtractOne(Blk("mermaidBlock", null, "[" + TextNode("graph TD") + "]"));

        Assert.True(req.HasDiagram);
        Assert.Equal(string.Empty, req.BodyPreview);
    }

    // UTC07: codeBlock language mermaid -> HasDiagram true, not in body
    [Fact]
    public void InspectBlock_CodeBlockMermaid_SetsHasDiagram()
    {
        var req = ExtractOne(Blk("codeBlock", "{\"language\":\"mermaid\"}", "[" + TextNode("graph TD") + "]"));

        Assert.True(req.HasDiagram);
        Assert.Equal(string.Empty, req.BodyPreview);
    }

    // UTC08: codeBlock language plantuml -> HasDiagram true
    [Fact]
    public void InspectBlock_CodeBlockPlantuml_SetsHasDiagram()
    {
        var req = ExtractOne(Blk("codeBlock", "{\"language\":\"plantuml\"}", "[" + TextNode("@startuml") + "]"));

        Assert.True(req.HasDiagram);
    }

    // UTC09: codeBlock language puml -> HasDiagram true
    [Fact]
    public void InspectBlock_CodeBlockPuml_SetsHasDiagram()
    {
        var req = ExtractOne(Blk("codeBlock", "{\"language\":\"puml\"}", "[" + TextNode("@startuml") + "]"));

        Assert.True(req.HasDiagram);
    }

    // UTC10: codeBlock language mixed case with padding " MerMaid " -> Trimmed, case-insensitive match
    [Fact]
    public void InspectBlock_CodeBlockMermaidMixedCase_SetsHasDiagram()
    {
        var req = ExtractOne(Blk("codeBlock", "{\"language\":\" MerMaid \"}", "[" + TextNode("graph TD") + "]"));

        Assert.True(req.HasDiagram);
    }

    // UTC11: codeBlock other language (csharp) -> Not a diagram; code text added to body
    [Fact]
    public void InspectBlock_CodeBlockOtherLanguage_AddsTextToBody()
    {
        var req = ExtractOne(Blk("codeBlock", "{\"language\":\"csharp\"}", "[" + TextNode("var x = 1;") + "]"));

        Assert.False(req.HasDiagram);
        Assert.Equal("var x = 1;", req.BodyPreview);
    }

    // UTC12: codeBlock without attrs -> Language treated as empty; added to body
    [Fact]
    public void InspectBlock_CodeBlockWithoutAttrs_AddsTextToBody()
    {
        var req = ExtractOne(Blk("codeBlock", null, "[" + TextNode("echo hi") + "]"));

        Assert.False(req.HasDiagram);
        Assert.Equal("echo hi", req.BodyPreview);
    }

    // UTC13: figmaEmbed with attrs -> HasFigma true, not in body
    [Fact]
    public void InspectBlock_FigmaEmbedWithAttrs_SetsHasFigma()
    {
        var req = ExtractOne(Blk("figmaEmbed", "{\"url\":\"https://figma.com/file/1\"}"));

        Assert.True(req.HasFigma);
        Assert.Equal(string.Empty, req.BodyPreview);
    }

    // UTC14: figma block with attrs -> HasFigma true
    [Fact]
    public void InspectBlock_FigmaWithAttrs_SetsHasFigma()
    {
        var req = ExtractOne(Blk("figma", "{\"url\":\"u\"}"));

        Assert.True(req.HasFigma);
    }

    // UTC15: figmaEmbed without attrs -> HasFigma true still
    [Fact]
    public void InspectBlock_FigmaEmbedWithoutAttrs_SetsHasFigma()
    {
        var req = ExtractOne(Blk("figmaEmbed"));

        Assert.True(req.HasFigma);
    }

    // UTC16: figma block without attrs -> HasFigma true still
    [Fact]
    public void InspectBlock_FigmaWithoutAttrs_SetsHasFigma()
    {
        var req = ExtractOne(Blk("figma"));

        Assert.True(req.HasFigma);
    }

    // UTC17: Default paragraph with figma URL in text -> HasFigma true, text in body
    [Fact]
    public void InspectBlock_ParagraphWithFigmaTextUrl_SetsHasFigma()
    {
        var req = ExtractOne(P("See https://www.figma.com/file/abc"));

        Assert.True(req.HasFigma);
        Assert.Equal("See https://www.figma.com/file/abc", req.BodyPreview);
    }

    // UTC18: Figma URL detection is case-insensitive -> HasFigma true
    [Fact]
    public void InspectBlock_ParagraphWithUppercaseFigmaUrl_SetsHasFigma()
    {
        var req = ExtractOne(P("HTTPS://FIGMA.COM/FILE/abc"));

        Assert.True(req.HasFigma);
    }

    // UTC19: Paragraph with link mark to figma -> HasFigma true even though text is not a URL
    [Fact]
    public void InspectBlock_ParagraphWithFigmaLinkMark_SetsHasFigma()
    {
        var req = ExtractOne(ParaOf(LinkText("design", "https://www.figma.com/file/x")));

        Assert.True(req.HasFigma);
        Assert.Equal("design", req.BodyPreview);
    }

    // UTC20: Figma link mark uppercase host -> HasFigma true
    [Fact]
    public void InspectBlock_FigmaLinkMarkUppercase_SetsHasFigma()
    {
        var req = ExtractOne(ParaOf(LinkText("design", "HTTPS://WWW.FIGMA.COM/file/x")));

        Assert.True(req.HasFigma);
    }

    // UTC21: Figma link mark nested deep (list > item > paragraph > text) -> HasFigma true
    [Fact]
    public void InspectBlock_NestedFigmaLinkMark_SetsHasFigma()
    {
        var list = Blk("bulletList", null,
            "[" + Blk("listItem", null, "[" + ParaOf(LinkText("d", "https://figma.com/x")) + "]") + "]");

        var req = ExtractOne(list);

        Assert.True(req.HasFigma);
        Assert.Equal("d", req.BodyPreview);
    }

    // UTC22: Link mark on the block itself (marks at block level) -> HasFigma true
    [Fact]
    public void InspectBlock_BlockLevelFigmaLinkMark_SetsHasFigma()
    {
        var block = "{\"type\":\"paragraph\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"https://figma.com/a\"}}],\"content\":[" + TextNode("x") + "]}";

        var req = ExtractOne(block);

        Assert.True(req.HasFigma);
    }

    // UTC23: Plain paragraph -> Added to body, no flags
    [Fact]
    public void InspectBlock_PlainParagraph_AddsBodyOnly()
    {
        var req = ExtractOne(P("Just text"));

        Assert.Equal("Just text", req.BodyPreview);
        Assert.False(req.HasTable);
        Assert.False(req.HasDiagram);
        Assert.False(req.HasFigma);
    }

    // UTC24: Unknown block type with text -> Treated as default (body)
    [Fact]
    public void InspectBlock_UnknownBlockType_TreatedAsDefault()
    {
        var req = ExtractOne(Blk("callout", null, "[" + TextNode("Note") + "]"));

        Assert.Equal("Note", req.BodyPreview);
    }

    // UTC25: Combination of every block type -> All flags true, AC and constraints collected
    [Fact]
    public void InspectBlock_AllBlockTypes_AggregateCorrectly()
    {
        var req = ExtractOne(
            P("intro"),
            TaskList("ac1"),
            Constraints("[{\"field\":\"F\"}]"),
            Blk("mermaidBlock", "{\"code\":\"graph\"}"),
            Blk("figma", "{\"u\":1}"));

        Assert.True(req.HasTable);
        Assert.True(req.HasDiagram);
        Assert.True(req.HasFigma);
        Assert.Equal("intro", req.BodyPreview);
        Assert.Equal(new[] { "ac1" }, req.AcceptanceCriteria.ToArray());
        Assert.Single(req.Constraints);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC26: figma text without trailing slash ("figma.com") -> Not detected
    [Fact]
    public void InspectBlock_FigmaTextWithoutSlash_NotDetected()
    {
        var req = ExtractOne(P("visit figma.com today"));

        Assert.False(req.HasFigma);
    }

    // UTC27: Non-figma link mark -> HasFigma false
    [Fact]
    public void InspectBlock_NonFigmaLinkMark_NotDetected()
    {
        var req = ExtractOne(ParaOf(LinkText("site", "https://example.com/page")));

        Assert.False(req.HasFigma);
    }

    // UTC28: Non-link mark carrying a figma href -> Ignored
    [Fact]
    public void InspectBlock_NonLinkMarkWithFigmaHref_NotDetected()
    {
        var text = "{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"bold\",\"attrs\":{\"href\":\"https://figma.com/a\"}}]}";

        var req = ExtractOne(ParaOf(text));

        Assert.False(req.HasFigma);
    }

    // UTC29: Link mark without attrs -> Ignored
    [Fact]
    public void InspectBlock_LinkMarkWithoutAttrs_NotDetected()
    {
        var text = "{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"link\"}]}";

        var req = ExtractOne(ParaOf(text));

        Assert.False(req.HasFigma);
    }

    // UTC30: Link mark with attrs but null/missing href -> Ignored
    [Fact]
    public void InspectBlock_LinkMarkWithNullHref_NotDetected()
    {
        var text = "{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":null}}]}";

        var req = ExtractOne(ParaOf(text));

        Assert.False(req.HasFigma);
    }

    // UTC31: Marks array containing non-object entries -> Ignored without error
    [Fact]
    public void InspectBlock_MarksWithNonObjectEntries_Ignored()
    {
        var text = "{\"type\":\"text\",\"text\":\"x\",\"marks\":[\"bold\",5]}";

        var req = ExtractOne(ParaOf(text));

        Assert.False(req.HasFigma);
        Assert.Equal("x", req.BodyPreview);
    }

    // UTC32: Several link marks, only second is figma -> HasFigma true
    [Fact]
    public void InspectBlock_SecondLinkMarkIsFigma_Detected()
    {
        var text = "{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"https://a.com\"}},{\"type\":\"link\",\"attrs\":{\"href\":\"https://figma.com/z\"}}]}";

        var req = ExtractOne(ParaOf(text));

        Assert.True(req.HasFigma);
    }

    // UTC33: Empty taskList item (no text) -> Skipped
    [Fact]
    public void InspectBlock_TaskListWithEmptyItems_SkipsThem()
    {
        var list = "{\"type\":\"taskList\",\"content\":[{\"type\":\"taskItem\",\"content\":[{\"type\":\"paragraph\"}]},{\"type\":\"taskItem\",\"content\":[" + P("   ") + "]}," + "{\"type\":\"taskItem\",\"content\":[" + P("real") + "]}]}";

        var req = ExtractOne(list);

        Assert.Equal(new[] { "real" }, req.AcceptanceCriteria.ToArray());
    }

    // UTC34: taskList without content array -> No acceptance criteria
    [Fact]
    public void InspectBlock_TaskListWithoutContent_NoCriteria()
    {
        var req = ExtractOne(Blk("taskList"));

        Assert.Empty(req.AcceptanceCriteria);
    }

    // UTC35: taskList with non-object items -> Treated as empty text, skipped
    [Fact]
    public void InspectBlock_TaskListWithNonObjectItems_Skipped()
    {
        var req = ExtractOne(Blk("taskList", null, "[\"x\",5]"));

        Assert.Empty(req.AcceptanceCriteria);
    }

    // UTC36: Empty table -> HasTable true but body empty
    [Fact]
    public void InspectBlock_EmptyTable_SetsHasTableWithoutBody()
    {
        var req = ExtractOne(Blk("table"));

        Assert.True(req.HasTable);
        Assert.Equal(string.Empty, req.BodyPreview);
        Assert.Equal(0, req.BodyLength);
    }

    // UTC37: constraints block without rows -> HasTable true, no rows
    [Fact]
    public void InspectBlock_ConstraintsWithoutRows_SetsHasTableOnly()
    {
        var req = ExtractOne(Blk("constraints"));

        Assert.True(req.HasTable);
        Assert.Empty(req.Constraints);
    }

    // UTC38: constraints block where every row has blank field -> HasTable true, no rows
    [Fact]
    public void InspectBlock_ConstraintsWithOnlyBlankFields_NoRows()
    {
        var req = ExtractOne(Constraints("[{\"field\":\"  \"},{\"kind\":\"text\"}]"));

        Assert.True(req.HasTable);
        Assert.Empty(req.Constraints);
    }

    // UTC39: Diagram block with blank code (empty string) -> HasDiagram true, no fallback to text
    [Fact]
    public void InspectBlock_DiagramWithEmptyCode_HasDiagramTrue()
    {
        var req = ExtractOne(Blk("mermaidBlock", "{\"code\":\"\"}", "[" + TextNode("ignored") + "]"));

        Assert.True(req.HasDiagram);
    }

    // UTC40: Diagram block with no attrs and no content -> HasDiagram true
    [Fact]
    public void InspectBlock_EmptyDiagramBlock_HasDiagramTrue()
    {
        var req = ExtractOne(Blk("diagram"));

        Assert.True(req.HasDiagram);
        Assert.Equal(string.Empty, req.BodyPreview);
    }

    // UTC41: Diagram attrs.code null with attrs.mermaid present -> Mermaid attr used
    [Fact]
    public void InspectBlock_DiagramWithNullCodeAndMermaid_UsesMermaid()
    {
        var withMermaid = ExtractOne(Blk("diagram", "{\"code\":null,\"mermaid\":\"graph A\"}"));
        var withMermaidOnly = ExtractOne(Blk("diagram", "{\"mermaid\":\"graph A\"}"));

        Assert.Equal(withMermaidOnly.ContentHash, withMermaid.ContentHash);
    }

    // UTC42: codeBlock mermaid with empty text -> HasDiagram true
    [Fact]
    public void InspectBlock_EmptyMermaidCodeBlock_HasDiagramTrue()
    {
        var req = ExtractOne(Blk("codeBlock", "{\"language\":\"mermaid\"}"));

        Assert.True(req.HasDiagram);
    }

    // UTC43: codeBlock other language with whitespace-only text -> Not added to body
    [Fact]
    public void InspectBlock_CodeBlockWhitespaceOnly_NotAddedToBody()
    {
        var req = ExtractOne(Blk("codeBlock", "{\"language\":\"js\"}", "[" + TextNode("   ") + "]"));

        Assert.Equal(string.Empty, req.BodyPreview);
        Assert.False(req.HasDiagram);
    }

    // UTC44: codeBlock language "mermaidx" (prefix only) -> Not a diagram
    [Fact]
    public void InspectBlock_CodeBlockLanguageSimilarToMermaid_NotDiagram()
    {
        var req = ExtractOne(Blk("codeBlock", "{\"language\":\"mermaidx\"}", "[" + TextNode("code") + "]"));

        Assert.False(req.HasDiagram);
        Assert.Equal("code", req.BodyPreview);
    }

    // UTC45: Block without type -> Treated as default, body included
    [Fact]
    public void InspectBlock_BlockWithoutType_TreatedAsDefault()
    {
        var block = "{\"content\":[" + TextNode("typeless") + "]}";

        var req = ExtractOne(block);

        Assert.Equal("typeless", req.BodyPreview);
    }

    // UTC46: Default block with empty text -> Nothing added to body
    [Fact]
    public void InspectBlock_EmptyParagraph_NothingAdded()
    {
        var req = ExtractOne("{\"type\":\"paragraph\"}");

        Assert.Equal(string.Empty, req.BodyPreview);
        Assert.False(req.HasFigma);
    }

    // UTC47: Whitespace-only paragraph -> Not added to body
    [Fact]
    public void InspectBlock_WhitespaceParagraph_NothingAdded()
    {
        var req = ExtractOne(P("   "));

        Assert.Equal(string.Empty, req.BodyPreview);
    }

    // UTC48: Paragraph with empty-text figma link and empty text but figma href -> HasFigma true via marks only
    [Fact]
    public void InspectBlock_FigmaLinkOnEmptyText_StillDetected()
    {
        var req = ExtractOne(ParaOf(LinkText("", "https://figma.com/q")));

        Assert.True(req.HasFigma);
    }

    // =========================================================================
    // 3. BOUNDARY CASES (Figma marker "figma.com/" exact; empty vs single item collections)
    // =========================================================================

    // UTC49: Text "figma.com/" exactly -> Detected
    [Fact]
    public void InspectBlock_TextIsExactlyFigmaMarker_Detected()
    {
        var req = ExtractOne(P("figma.com/"));

        Assert.True(req.HasFigma);
    }

    // UTC50: Text "figma.com" (one char short of marker) -> Not detected
    [Fact]
    public void InspectBlock_TextOneCharShortOfFigmaMarker_NotDetected()
    {
        var req = ExtractOne(P("figma.com"));

        Assert.False(req.HasFigma);
    }

    // UTC51: Single taskList item -> One criterion
    [Fact]
    public void InspectBlock_TaskListWithSingleItem_OneCriterion()
    {
        var req = ExtractOne(TaskList("only"));

        Assert.Equal(new[] { "only" }, req.AcceptanceCriteria.ToArray());
    }

    // UTC52: Multiple taskLists -> Criteria concatenated in order
    [Fact]
    public void InspectBlock_MultipleTaskLists_ConcatenatesCriteria()
    {
        var req = ExtractOne(TaskList("a", "b"), TaskList("c"));

        Assert.Equal(new[] { "a", "b", "c" }, req.AcceptanceCriteria.ToArray());
    }

    // UTC53: Multiple constraints blocks -> Rows concatenated
    [Fact]
    public void InspectBlock_MultipleConstraintBlocks_ConcatenatesRows()
    {
        var req = ExtractOne(Constraints("[{\"field\":\"A\"}]"), Constraints("[{\"field\":\"B\"}]"));

        Assert.Equal(new[] { "A", "B" }, req.Constraints.Select(c => c.Field).ToArray());
    }
}
