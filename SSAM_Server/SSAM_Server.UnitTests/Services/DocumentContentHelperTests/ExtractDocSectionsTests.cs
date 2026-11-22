using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

/// <summary>ExtractDocSections (+ ExtractHeadingInlineText).</summary>
public class ExtractDocSectionsTests : DocumentContentHelperTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Two headings with ids -> Two sections with anchor, title, level and empty ReqUids
    [Fact]
    public void ExtractDocSections_WithTwoHeadings_ReturnsTwoSections()
    {
        var json = Doc(H(1, "Intro", "\"id\":\"a\""), P("x"), H(2, "Scope", "\"id\":\"b\""));

        var sections = DocumentContentHelper.ExtractDocSections(json);

        Assert.Equal(2, sections.Count);
        Assert.Equal("a", sections[0].AnchorId);
        Assert.Equal("Intro", sections[0].Title);
        Assert.Equal(1, sections[0].Level);
        Assert.Empty(sections[0].ReqUids);
        Assert.Equal("b", sections[1].AnchorId);
        Assert.Equal("Scope", sections[1].Title);
        Assert.Equal(2, sections[1].Level);
        Assert.Empty(sections[1].ReqUids);
    }

    // UTC02: Requirement headings after a section -> reqUid attached to the current section
    [Fact]
    public void ExtractDocSections_WithRequirementHeadings_AttachesReqUidsToCurrentSection()
    {
        var json = Doc(
            H(1, "Intro", "\"id\":\"a\""),
            ReqH(2, "REQ-01 — X", "\"reqUid\":\"REQ-01\""),
            ReqH(2, "REQ-02 — Y", "\"reqUid\":\"REQ-02\""),
            H(1, "Next", "\"id\":\"b\""),
            ReqH(2, "REQ-03 — Z", "\"reqUid\":\"REQ-03\""));

        var sections = DocumentContentHelper.ExtractDocSections(json);

        Assert.Equal(2, sections.Count);
        Assert.Equal(new[] { "REQ-01", "REQ-02" }, sections[0].ReqUids.ToArray());
        Assert.Equal(new[] { "REQ-03" }, sections[1].ReqUids.ToArray());
    }

    // UTC03: Heading with only anchorId -> anchorId used
    [Fact]
    public void ExtractDocSections_WithOnlyAnchorId_UsesAnchorId()
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(H(1, "T", "\"anchorId\":\"anc\"")));

        Assert.Equal("anc", Assert.Single(sections).AnchorId);
    }

    // UTC04: Heading with both id and anchorId -> id wins
    [Fact]
    public void ExtractDocSections_WithIdAndAnchorId_IdWins()
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(H(1, "T", "\"id\":\"i\",\"anchorId\":\"anc\"")));

        Assert.Equal("i", Assert.Single(sections).AnchorId);
    }

    // UTC05: Heading levels 1..6 -> Level read from attrs
    [Theory]
    [InlineData(1)] // UTC05
    [InlineData(2)] // UTC06
    [InlineData(3)] // UTC07
    [InlineData(4)] // UTC08
    [InlineData(6)] // UTC09
    public void ExtractDocSections_WithLevel_ReturnsThatLevel(int level)
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(H(level, "T", "\"id\":\"a\"")));

        Assert.Equal(level, Assert.Single(sections).Level);
    }

    // UTC10: Title and id padded with whitespace -> Trimmed
    [Fact]
    public void ExtractDocSections_WithPaddedTitleAndId_Trims()
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(H(1, "  Intro  ", "\"id\":\"  a  \"")));

        var s = Assert.Single(sections);
        Assert.Equal("Intro", s.Title);
        Assert.Equal("a", s.AnchorId);
    }

    // UTC11: Heading with several text children -> Concatenated without separator
    [Fact]
    public void ExtractDocSections_WithMultipleTextChildren_ConcatenatesText()
    {
        var heading = "{\"type\":\"heading\",\"attrs\":{\"level\":1,\"id\":\"a\"},\"content\":[" + TextNode("Hello ") + "," + TextNode("World") + "]}";

        var sections = DocumentContentHelper.ExtractDocSections(Doc(heading));

        Assert.Equal("Hello World", Assert.Single(sections).Title);
    }

    // UTC12: Heading child without text but with nested content -> Plain text of child appended
    [Fact]
    public void ExtractDocSections_WithNestedInlineChild_UsesNestedText()
    {
        var heading = "{\"type\":\"heading\",\"attrs\":{\"level\":1,\"id\":\"a\"},\"content\":[" + TextNode("A ")
                      + ",{\"type\":\"mention\",\"content\":[" + TextNode("B") + "," + TextNode("C") + "]}]}";

        var sections = DocumentContentHelper.ExtractDocSections(Doc(heading));

        Assert.Equal("A B C", Assert.Single(sections).Title);
    }

    // UTC13: Heading child hardBreak (no text) -> Contributes nothing
    [Fact]
    public void ExtractDocSections_WithHardBreakChild_IgnoredInTitle()
    {
        var heading = "{\"type\":\"heading\",\"attrs\":{\"level\":1,\"id\":\"a\"},\"content\":[" + TextNode("A") + ",{\"type\":\"hardBreak\"}," + TextNode("B") + "]}";

        var sections = DocumentContentHelper.ExtractDocSections(Doc(heading));

        Assert.Equal("AB", Assert.Single(sections).Title);
    }

    // UTC14: Non-heading blocks interleaved -> Ignored
    [Fact]
    public void ExtractDocSections_WithNonHeadingBlocks_Ignored()
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(P("a"), H(1, "T", "\"id\":\"x\""), P("b")));

        Assert.Single(sections);
    }

    // UTC15: attrs.req = true with reqUid and no section before -> Ignored
    [Fact]
    public void ExtractDocSections_RequirementBeforeAnySection_Ignored()
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(ReqH(2, "REQ-01 — X", "\"reqUid\":\"REQ-01\"")));

        Assert.Empty(sections);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC16: Null content -> Empty list
    // UTC17: Empty string -> Empty list
    // UTC18: Whitespace-only -> Empty list
    [Theory]
    [InlineData(null)]    // UTC16
    [InlineData("")]      // UTC17
    [InlineData("   \n")] // UTC18
    public void ExtractDocSections_WithNullOrBlank_ReturnsEmpty(string? json)
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections(json));
    }

    // UTC19: Invalid JSON -> Empty list (catch path)
    [Theory]
    [InlineData("{not json")]  // UTC19
    [InlineData("[1,")]        // UTC20
    [InlineData("hello")]      // UTC21
    public void ExtractDocSections_WithInvalidJson_ReturnsEmpty(string json)
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections(json));
    }

    // UTC22: Root is an array -> Empty list
    [Fact]
    public void ExtractDocSections_WhenRootIsArray_ReturnsEmpty()
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections("[]"));
    }

    // UTC23: Root is a string/number/null literal -> Empty list
    [Theory]
    [InlineData("\"x\"")] // UTC23
    [InlineData("12")]    // UTC24
    [InlineData("null")]  // UTC25
    public void ExtractDocSections_WhenRootIsLiteral_ReturnsEmpty(string json)
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections(json));
    }

    // UTC26: Object without content -> Empty list
    [Fact]
    public void ExtractDocSections_WhenContentMissing_ReturnsEmpty()
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections("{\"type\":\"doc\"}"));
    }

    // UTC27: content is an object, not an array -> Empty list
    [Fact]
    public void ExtractDocSections_WhenContentIsNotArray_ReturnsEmpty()
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections("{\"content\":{\"a\":1}}"));
    }

    // UTC28: Non-object entries in content -> Skipped
    [Fact]
    public void ExtractDocSections_WithNonObjectEntries_Skipped()
    {
        var json = "{\"content\":[1,\"x\",null," + H(1, "T", "\"id\":\"a\"") + "]}";

        Assert.Single(DocumentContentHelper.ExtractDocSections(json));
    }

    // UTC29: Block without a type -> Skipped
    [Fact]
    public void ExtractDocSections_BlockWithoutType_Skipped()
    {
        var json = "{\"content\":[{\"attrs\":{\"id\":\"a\"},\"content\":[" + TextNode("T") + "]}]}";

        Assert.Empty(DocumentContentHelper.ExtractDocSections(json));
    }

    // UTC30: Heading without content -> Skipped (empty title)
    [Fact]
    public void ExtractDocSections_HeadingWithoutContent_Skipped()
    {
        var json = "{\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":1,\"id\":\"a\"}}]}";

        Assert.Empty(DocumentContentHelper.ExtractDocSections(json));
    }

    // UTC31: Heading with empty content array -> Skipped
    [Fact]
    public void ExtractDocSections_HeadingWithEmptyContentArray_Skipped()
    {
        var json = "{\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":1,\"id\":\"a\"},\"content\":[]}]}";

        Assert.Empty(DocumentContentHelper.ExtractDocSections(json));
    }

    // UTC32: Heading with whitespace-only title -> Skipped
    [Fact]
    public void ExtractDocSections_HeadingWithWhitespaceTitle_Skipped()
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections(Doc(H(1, "   ", "\"id\":\"a\""))));
    }

    // UTC33: Heading without id/anchorId -> Skipped
    [Fact]
    public void ExtractDocSections_HeadingWithoutId_Skipped()
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections(Doc(H(1, "T"))));
    }

    // UTC34: Heading without attrs at all -> Skipped
    [Fact]
    public void ExtractDocSections_HeadingWithoutAttrs_Skipped()
    {
        var json = "{\"content\":[{\"type\":\"heading\",\"content\":[" + TextNode("T") + "]}]}";

        Assert.Empty(DocumentContentHelper.ExtractDocSections(json));
    }

    // UTC35: Heading with whitespace-only id -> Skipped (trimmed id is empty, no fallback to anchorId)
    [Fact]
    public void ExtractDocSections_HeadingWithWhitespaceId_Skipped()
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections(Doc(H(1, "T", "\"id\":\"  \",\"anchorId\":\"anc\""))));
    }

    // UTC36: Level missing -> Defaults to 1
    [Fact]
    public void ExtractDocSections_WithoutLevel_DefaultsToOne()
    {
        var json = "{\"content\":[{\"type\":\"heading\",\"attrs\":{\"id\":\"a\"},\"content\":[" + TextNode("T") + "]}]}";

        Assert.Equal(1, Assert.Single(DocumentContentHelper.ExtractDocSections(json)).Level);
    }

    // UTC37: Level as string -> Defaults to 1 (not int)
    [Fact]
    public void ExtractDocSections_WithStringLevel_DefaultsToOne()
    {
        var json = "{\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":\"3\",\"id\":\"a\"},\"content\":[" + TextNode("T") + "]}]}";

        Assert.Equal(1, Assert.Single(DocumentContentHelper.ExtractDocSections(json)).Level);
    }

    // UTC38: Level as decimal 2.5 -> Defaults to 1 (not int)
    [Fact]
    public void ExtractDocSections_WithDecimalLevel_DefaultsToOne()
    {
        var json = "{\"content\":[{\"type\":\"heading\",\"attrs\":{\"level\":2.5,\"id\":\"a\"},\"content\":[" + TextNode("T") + "]}]}";

        Assert.Equal(1, Assert.Single(DocumentContentHelper.ExtractDocSections(json)).Level);
    }

    // UTC39: Level 0 / negative -> Stored as is (no validation)
    [Fact]
    public void ExtractDocSections_WithZeroOrNegativeLevel_StoredAsIs()
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(H(0, "A", "\"id\":\"a\""), H(-2, "B", "\"id\":\"b\"")));

        Assert.Equal(new[] { 0, -2 }, sections.Select(s => s.Level).ToArray());
    }

    // UTC40: req attr is string "true" -> Not a requirement; heading treated as section when id exists
    [Fact]
    public void ExtractDocSections_WithStringReqAttr_TreatedAsSection()
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(H(1, "T", "\"req\":\"true\",\"id\":\"a\"")));

        Assert.Single(sections);
    }

    // UTC41: req=false heading with id -> Treated as section
    [Fact]
    public void ExtractDocSections_WithReqFalse_TreatedAsSection()
    {
        var sections = DocumentContentHelper.ExtractDocSections(Doc(H(1, "T", "\"req\":false,\"id\":\"a\"")));

        Assert.Single(sections);
    }

    // UTC42: Requirement heading with blank reqUid -> Not attached
    [Fact]
    public void ExtractDocSections_RequirementWithBlankReqUid_NotAttached()
    {
        var json = Doc(H(1, "S", "\"id\":\"a\""), ReqH(2, "R", "\"reqUid\":\"   \""));

        var s = Assert.Single(DocumentContentHelper.ExtractDocSections(json));

        Assert.Empty(s.ReqUids);
    }

    // UTC43: Requirement heading without reqUid -> Not attached
    [Fact]
    public void ExtractDocSections_RequirementWithoutReqUid_NotAttached()
    {
        var json = Doc(H(1, "S", "\"id\":\"a\""), ReqH(2, "R"));

        Assert.Empty(Assert.Single(DocumentContentHelper.ExtractDocSections(json)).ReqUids);
    }

    // UTC44: Requirement heading with empty title -> Skipped entirely (not attached)
    [Fact]
    public void ExtractDocSections_RequirementWithEmptyTitle_Skipped()
    {
        var json = Doc(H(1, "S", "\"id\":\"a\""), ReqH(2, "  ", "\"reqUid\":\"REQ-01\""));

        Assert.Empty(Assert.Single(DocumentContentHelper.ExtractDocSections(json)).ReqUids);
    }

    // UTC45: Requirement reqUid is padded -> Trimmed before attaching
    [Fact]
    public void ExtractDocSections_RequirementReqUidPadded_Trimmed()
    {
        var json = Doc(H(1, "S", "\"id\":\"a\""), ReqH(2, "R", "\"reqUid\":\"  REQ-09  \""));

        Assert.Equal(new[] { "REQ-09" }, Assert.Single(DocumentContentHelper.ExtractDocSections(json)).ReqUids.ToArray());
    }

    // UTC46: Heading with skipped (id-less) section followed by requirement -> Requirement attached to previous valid section
    [Fact]
    public void ExtractDocSections_IdLessHeadingBetween_RequirementAttachesToPreviousSection()
    {
        var json = Doc(H(1, "A", "\"id\":\"a\""), H(1, "No id"), ReqH(2, "R", "\"reqUid\":\"REQ-01\""));

        var s = Assert.Single(DocumentContentHelper.ExtractDocSections(json));

        Assert.Equal("a", s.AnchorId);
        Assert.Equal(new[] { "REQ-01" }, s.ReqUids.ToArray());
    }

    // UTC47: Non-string type value -> Throws InvalidOperationException (current behaviour, not caught)
    [Fact]
    public void ExtractDocSections_WithNumericType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DocumentContentHelper.ExtractDocSections("{\"content\":[{\"type\":3}]}"));
    }

    // UTC48: Duplicate requirement uids -> Both appended (no dedupe here)
    [Fact]
    public void ExtractDocSections_DuplicateReqUids_BothAppended()
    {
        var json = Doc(H(1, "S", "\"id\":\"a\""), ReqH(2, "R", "\"reqUid\":\"REQ-01\""), ReqH(2, "R2", "\"reqUid\":\"REQ-01\""));

        Assert.Equal(new[] { "REQ-01", "REQ-01" }, Assert.Single(DocumentContentHelper.ExtractDocSections(json)).ReqUids.ToArray());
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC49: Document with exactly one heading -> One section
    [Fact]
    public void ExtractDocSections_WithSingleHeading_ReturnsOneSection()
    {
        Assert.Single(DocumentContentHelper.ExtractDocSections(Doc(H(1, "Only", "\"id\":\"a\""))));
    }

    // UTC50: Empty content array -> Empty list
    [Fact]
    public void ExtractDocSections_WithEmptyContentArray_ReturnsEmpty()
    {
        Assert.Empty(DocumentContentHelper.ExtractDocSections("{\"content\":[]}"));
    }

    // UTC51: Long anchor id is not truncated (no 36 cap in this method)
    [Fact]
    public void ExtractDocSections_LongAnchorId_NotTruncated()
    {
        var id = new string('z', 50);

        Assert.Equal(id, Assert.Single(DocumentContentHelper.ExtractDocSections(Doc(H(1, "T", "\"id\":\"" + id + "\"")))).AnchorId);
    }

    // UTC52: Section order follows document order for many headings
    [Fact]
    public void ExtractDocSections_ManyHeadings_PreservesDocumentOrder()
    {
        var blocks = Enumerable.Range(1, 5).Select(i => H(2, "S" + i, "\"id\":\"id" + i + "\"")).ToArray();

        var sections = DocumentContentHelper.ExtractDocSections(Doc(blocks));

        Assert.Equal(new[] { "S1", "S2", "S3", "S4", "S5" }, sections.Select(s => s.Title).ToArray());
    }
}
