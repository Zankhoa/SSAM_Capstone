using SSAM_Server.Services;

namespace SSAM_Server.UnitTests.Services.DocumentContentHelperTests;

public class CollapseSpacesTests
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Single spaces between words -> Unchanged
    [Fact]
    public void CollapseSpaces_WithSingleSpaces_ReturnsSame()
    {
        Assert.Equal("a b c", DocumentContentHelper.CollapseSpaces("a b c"));
    }

    // UTC02: Multiple spaces between words -> Collapsed to one
    [Fact]
    public void CollapseSpaces_WithMultipleSpaces_CollapsesToOne()
    {
        Assert.Equal("a b c", DocumentContentHelper.CollapseSpaces("a    b     c"));
    }

    // UTC03: Leading and trailing whitespace -> Trimmed
    [Fact]
    public void CollapseSpaces_WithLeadingAndTrailing_Trims()
    {
        Assert.Equal("a b", DocumentContentHelper.CollapseSpaces("   a b   "));
    }

    // UTC04: Tabs and newlines -> Replaced by a single space
    [Fact]
    public void CollapseSpaces_WithTabsAndNewlines_ReplacedBySpace()
    {
        Assert.Equal("a b c d", DocumentContentHelper.CollapseSpaces("a\tb\nc\r\nd"));
    }

    // UTC05: Mixed whitespace run -> One space
    [Fact]
    public void CollapseSpaces_WithMixedWhitespaceRun_OneSpace()
    {
        Assert.Equal("a b", DocumentContentHelper.CollapseSpaces("a \t\r\n  \t b"));
    }

    // UTC06: Non-breaking space -> Treated as whitespace and collapsed
    [Fact]
    public void CollapseSpaces_WithNonBreakingSpace_Collapsed()
    {
        Assert.Equal("a b", DocumentContentHelper.CollapseSpaces("a  b"));
    }

    // UTC07: Unicode text -> Preserved
    [Fact]
    public void CollapseSpaces_WithUnicodeText_Preserved()
    {
        Assert.Equal("Tiếng Việt", DocumentContentHelper.CollapseSpaces("Tiếng   Việt"));
    }

    // UTC08: Single word -> Same
    [Fact]
    public void CollapseSpaces_WithSingleWord_ReturnsSame()
    {
        Assert.Equal("word", DocumentContentHelper.CollapseSpaces("word"));
    }

    // UTC09: Zero-width space (not whitespace) -> Preserved
    [Fact]
    public void CollapseSpaces_WithZeroWidthSpace_Preserved()
    {
        Assert.Equal("a​b", DocumentContentHelper.CollapseSpaces("a​b"));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC10: Null -> Empty string
    [Fact]
    public void CollapseSpaces_WithNull_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.CollapseSpaces(null!));
    }

    // UTC11: Empty string -> Empty string
    [Fact]
    public void CollapseSpaces_WithEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DocumentContentHelper.CollapseSpaces(string.Empty));
    }

    // UTC12: Whitespace only (spaces/tabs/newlines) -> Empty string
    [Theory]
    [InlineData(" ")]        // UTC12
    [InlineData("     ")]     // UTC13
    [InlineData("\t\r\n")]    // UTC14
    [InlineData(" ")]    // UTC15
    public void CollapseSpaces_WithWhitespaceOnly_ReturnsEmpty(string input)
    {
        Assert.Equal(string.Empty, DocumentContentHelper.CollapseSpaces(input));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC16: Single character -> Same
    [Fact]
    public void CollapseSpaces_WithSingleChar_ReturnsSame()
    {
        Assert.Equal("a", DocumentContentHelper.CollapseSpaces("a"));
    }

    // UTC17: Exactly two spaces between words -> One space
    [Fact]
    public void CollapseSpaces_WithTwoSpaces_OneSpace()
    {
        Assert.Equal("a b", DocumentContentHelper.CollapseSpaces("a  b"));
    }

    // UTC18: Very long whitespace run (10000) -> One space
    [Fact]
    public void CollapseSpaces_WithVeryLongRun_OneSpace()
    {
        Assert.Equal("a b", DocumentContentHelper.CollapseSpaces("a" + new string(' ', 10000) + "b"));
    }

    // UTC19: Idempotent -> Collapsing result again returns same value
    [Fact]
    public void CollapseSpaces_IsIdempotent()
    {
        var once = DocumentContentHelper.CollapseSpaces("  a   b \n c ");

        Assert.Equal(once, DocumentContentHelper.CollapseSpaces(once));
    }
}
