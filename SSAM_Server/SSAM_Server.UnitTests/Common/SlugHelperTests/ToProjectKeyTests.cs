using SSAM_Server.Common;

namespace SSAM_Server.UnitTests.Common.SlugHelperTests;

public class ToProjectKeyTests
{
    // UTC01: Normal - Name contains an existing valid uppercase acronym (2..10 chars) -> Returns that acronym
    [Fact]
    public void ToProjectKey_WhenNameContainsValidAcronym_ReturnsAcronym()
    {
        var input = "Hệ thống SSAM Backend";

        var result = SlugHelper.ToProjectKey(input);

        Assert.Equal("SSAM", result);
    }

    // UTC02: Normal - Multi-word name without existing acronym -> Returns uppercase initials
    [Fact]
    public void ToProjectKey_WithMultiWordVietnameseName_ReturnsInitials()
    {
        var input = "Quản lý Nhân sự Tiền lương";

        var result = SlugHelper.ToProjectKey(input);

        Assert.Equal("QLNSTL", result);
    }

    // UTC03: Normal - Single word name (initials < 2 chars) -> Falls back to first word uppercase
    [Fact]
    public void ToProjectKey_WithSingleWordName_FallsBackToFirstWord()
    {
        var input = "Phoenix";

        var result = SlugHelper.ToProjectKey(input);

        Assert.Equal("PHOENIX", result);
    }

    // UTC04: Normal - Leading digits in initials -> Strips leading digits
    [Fact]
    public void ToProjectKey_WhenInitialsStartWithDigits_StripsLeadingDigits()
    {
        var input = "2026 Quản Lý Dự Án";

        var result = SlugHelper.ToProjectKey(input);

        Assert.Equal("QLDA", result);
    }

    // UTC05: Abnormal - Null, whitespace, special chars, or digits-only input -> Returns empty string
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@@@###")]
    [InlineData("12345")]
    public void ToProjectKey_WhenInputCannotProduceValidLetters_ReturnsEmpty(string? input)
    {
        var result = SlugHelper.ToProjectKey(input);

        Assert.Equal(string.Empty, result);
    }

    // UTC06: Boundary - Acronym length 2 (min) and 10 (max) -> Returns exact acronym
    [Theory]
    [InlineData("AI Platform", "AI")]                   // Length 2 (lower boundary)
    [InlineData("ABCDEFGHIJ Platform", "ABCDEFGHIJ")]   // Length 10 (upper boundary)
    public void ToProjectKey_WhenAcronymLengthIsAtBoundary_ReturnsAcronym(string input, string expected)
    {
        var result = SlugHelper.ToProjectKey(input);

        Assert.Equal(expected, result);
        Assert.Equal(expected.Length, result.Length);
    }

    // UTC07: Boundary - Acronym length 11 (exceeds ProjectKeyRegex max 10) -> Ignores acronym and falls back to initials
    [Fact]
    public void ToProjectKey_WhenAcronymExceeds10Chars_FallsBackToInitials()
    {
        var input = "ABCDEFGHIJK Platform"; // First word is 11 chars -> not matched as acronym -> initials "AP"

        var result = SlugHelper.ToProjectKey(input);

        Assert.Equal("AP", result);
    }

    // UTC08: Boundary - Generated initials exceed 10 characters -> Truncates to 10 characters
    [Fact]
    public void ToProjectKey_WhenInitialsExceed10Chars_TruncatesTo10Chars()
    {
        var input = "One Two Three Four Five Six Seven Eight Nine Ten Eleven Twelve"; // 12 words

        var result = SlugHelper.ToProjectKey(input);

        Assert.Equal(10, result.Length);
        Assert.Equal("OTTFFSSENT", result);
    }
}