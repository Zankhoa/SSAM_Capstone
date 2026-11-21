using SSAM_Server.Common;

namespace SSAM_Server.UnitTests.Common.SlugHelperTests;

public class StripVietnameseTests
{
    // UTC01: Normal - Vietnamese string with all tone marks and 'đ'/'Đ' -> Strips diacritics and preserves case
    [Fact]
    public void StripVietnamese_WithVietnameseDiacritics_ReturnsAsciiString()
    {
        var input = "Đặng Phạm Công Minh - Á à ả ã ạ, Ă ắ ằ ẳ ẵ ặ, Â ấ ầ ẩ ẫ ậ, Ê ế ề ể ễ ệ, Ô ố ồ ổ ỗ ộ, Ơ ớ ờ ở ỡ ợ, Ư ứ ừ ử ữ ự, Ý ỳ ỷ ỹ ỵ, đ Đ";

        var result = SlugHelper.StripVietnamese(input);

        Assert.Equal("Dang Pham Cong Minh - A a a a a, A a a a a a, A a a a a a, E e e e e e, O o o o o o, O o o o o o, U u u u u u, Y y y y y, d D", result);
    }

    // UTC02: Normal - Plain ASCII string -> Returns unchanged
    [Fact]
    public void StripVietnamese_WithPlainAscii_ReturnsUnchanged()
    {
        var input = "SSAM_Server Backend API 2026!";

        var result = SlugHelper.StripVietnamese(input);

        Assert.Equal("SSAM_Server Backend API 2026!", result);
    }

    // UTC03: Abnormal - Null, empty or whitespace -> Returns empty string
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StripVietnamese_WhenNullOrWhitespace_ReturnsEmpty(string? input)
    {
        var result = SlugHelper.StripVietnamese(input);

        Assert.Equal(string.Empty, result);
    }
}