using SSAM_Server.Common;


namespace SSAM_Server.UnitTests.Common.SlugHelperTests;

public class SlugifyTests
{
    // UTC01: Normal - Vietnamese phrase with spaces and special characters -> Lowercase hyphen-separated slug
    [Fact]
    public void Slugify_WithVietnameseAndSpecialChars_ReturnsNormalizedSlug()
    {
        var input = "  Phòng Kỹ Thuật & Công Nghệ - 2026!  ";

        var result = SlugHelper.Slugify(input);

        Assert.Equal("phong-ky-thuat-cong-nghe-2026", result);
    }

    // UTC02: Abnormal - Null, whitespace, or only special characters -> Returns empty string
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---@@@###---")]
    public void Slugify_WhenInputHasNoAlphanumericChars_ReturnsEmpty(string? input)
    {
        var result = SlugHelper.Slugify(input);

        Assert.Equal(string.Empty, result);
    }

    // UTC03: Boundary - Exact MaxSlugLength (50 characters) -> Preserves full 50 characters
    [Fact]
    public void Slugify_WhenLengthIsExact50_Returns50Chars()
    {
        var input = new string('a', SlugHelper.MaxSlugLength);

        var result = SlugHelper.Slugify(input);

        Assert.Equal(SlugHelper.MaxSlugLength, result.Length);
        Assert.Equal(input, result);
    }

    // UTC04: Boundary - Exceeds 50 characters -> Truncates to 50 characters
    [Fact]
    public void Slugify_WhenLengthExceeds50_TruncatesTo50Chars()
    {
        var input = new string('a', 55);

        var result = SlugHelper.Slugify(input);

        Assert.Equal(SlugHelper.MaxSlugLength, result.Length);
        Assert.Equal(new string('a', 50), result);
    }

    // UTC05: Boundary - Truncation lands on a hyphen at index 49 -> Trims trailing hyphen (49 chars)
    [Fact]
    public void Slugify_WhenTruncationEndsWithHyphen_TrimsTrailingHyphen()
    {
        var input = new string('a', 49) + "-b"; // 51 chars; index 49 is '-'

        var result = SlugHelper.Slugify(input);

        Assert.Equal(49, result.Length);
        Assert.Equal(new string('a', 49), result);
    }
}