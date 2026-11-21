using SSAM_Server.Common;

namespace SSAM_Server.UnitTests.Common.SlugHelperTests;

public class ToRoleCodeTests
{
    // UTC01: Normal - Vietnamese role name -> Uppercase underscore-separated role code
    [Fact]
    public void ToRoleCode_WithVietnameseRoleName_ReturnsUppercaseCode()
    {
        var input = "  Trưởng nhóm Kiểm thử (QA)  ";

        var result = SlugHelper.ToRoleCode(input);

        Assert.Equal("TRUONG_NHOM_KIEM_THU_QA", result);
    }

    // UTC02: Normal - Input starting with digit -> Prepends 'R_' prefix
    [Fact]
    public void ToRoleCode_WhenStartsWithDigit_PrependsRPrefix()
    {
        var input = "2FA Security Admin";

        var result = SlugHelper.ToRoleCode(input);

        Assert.Equal("R_2FA_SECURITY_ADMIN", result);
    }

    // UTC03: Abnormal - Null, whitespace, or only special characters -> Returns empty string
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("___@@@___")]
    public void ToRoleCode_WhenInputHasNoAlphanumericChars_ReturnsEmpty(string? input)
    {
        var result = SlugHelper.ToRoleCode(input);

        Assert.Equal(string.Empty, result);
    }

    // UTC04: Boundary - Exact 50 characters starting with letter -> Preserves full 50 characters
    [Fact]
    public void ToRoleCode_WhenLengthIsExact50_Returns50Chars()
    {
        var input = new string('A', 50);

        var result = SlugHelper.ToRoleCode(input);

        Assert.Equal(50, result.Length);
        Assert.Equal(input, result);
    }

    // UTC05: Boundary - Exceeds 50 characters with underscore at index 49 -> Truncates and trims trailing '_' (49 chars)
    [Fact]
    public void ToRoleCode_WhenTruncationEndsWithUnderscore_TrimsTrailingUnderscore()
    {
        var input = new string('A', 49) + "_B"; // 51 chars; index 49 is '_'

        var result = SlugHelper.ToRoleCode(input);

        Assert.Equal(49, result.Length);
        Assert.Equal(new string('A', 49), result);
    }

    // UTC06: Boundary - 50 chars starting with digit -> Prepends 'R_' then truncates back to 50 chars
    [Fact]
    public void ToRoleCode_WhenDigitPrefixCausesLengthExceed50_TruncatesTo50Chars()
    {
        var input = "1" + new string('A', 49); // 50 chars -> "R_1AAA...A" (52 chars) -> truncated to 50

        var result = SlugHelper.ToRoleCode(input);

        Assert.Equal(50, result.Length);
        Assert.StartsWith("R_1", result);
    }

    // UTC07: Boundary - Digit prefix pushes underscore to index 49 -> Trims trailing '_' after second truncation (49 chars)
    [Fact]
    public void ToRoleCode_WhenDigitPrefixTruncationEndsWithUnderscore_TrimsTrailingUnderscore()
    {
        var input = "1" + new string('A', 46) + "_B"; // 49 chars -> "R_1" + 46 'A's + "_B" (51 chars) -> index 49 is '_'

        var result = SlugHelper.ToRoleCode(input);

        Assert.Equal(49, result.Length);
        Assert.Equal("R_1" + new string('A', 46), result);
    }
}