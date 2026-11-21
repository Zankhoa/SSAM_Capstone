using SSAM_Server.Common;

namespace SSAM_Server.UnitTests.Common.SlugHelperTests;

public class SlugHelperRegexTests
{
    // =========================================================================
    // 1. WorkspaceCodeRegex: ^[a-z0-9]+(-[a-z0-9]+)*$
    // =========================================================================

    // UTC01 -> UTC03: Valid workspace codes (single char, hyphenated, alphanumeric)
    [Theory]
    [InlineData("a")]           // UTC01: Boundary - 1 character
    [InlineData("ssam-tech")]   // UTC02: Normal - hyphen separated
    [InlineData("team-01")]     // UTC03: Normal - with digits
    public void WorkspaceCodeRegex_WithValidSlug_ReturnsTrue(string code)
    {
        Assert.True(SlugHelper.WorkspaceCodeRegex().IsMatch(code));
    }

    // UTC04 -> UTC07: Invalid workspace codes (empty, leading/trailing/consecutive hyphens, uppercase)
    [Theory]
    [InlineData("")]            // UTC04: Empty
    [InlineData("-ssam")]       // UTC05: Starts with hyphen
    [InlineData("ssam-")]       // UTC06: Ends with hyphen
    [InlineData("ssam--tech")]  // UTC07: Consecutive hyphens or uppercase ("SSAM")
    [InlineData("SSAM")]
    public void WorkspaceCodeRegex_WithInvalidSlug_ReturnsFalse(string code)
    {
        Assert.False(SlugHelper.WorkspaceCodeRegex().IsMatch(code));
    }

    // =========================================================================
    // 2. RoleCodeRegex: ^[A-Z][A-Z0-9_]{1,49}$ (Length 2..50)
    // =========================================================================

    // UTC01 -> UTC03: Valid role codes (2 chars, 50 chars, letters/digits/underscore)
    [Theory]
    [InlineData("QA")]                                                      // UTC01: Boundary - 2 chars (min)
    [InlineData("BACKEND_DEV_01")]                                          // UTC02: Normal - letters, digits, underscore
    [InlineData("A1234567890123456789012345678901234567890123456789")]       // UTC03: Boundary - 50 chars (max)
    public void RoleCodeRegex_WithValidCode_ReturnsTrue(string code)
    {
        Assert.True(SlugHelper.RoleCodeRegex().IsMatch(code));
    }

    // UTC04 -> UTC07: Invalid role codes (1 char, 51 chars, starts with digit/underscore, lowercase)
    [Theory]
    [InlineData("A")]                                                       // UTC04: Boundary - 1 char (below min 2)
    [InlineData("A12345678901234567890123456789012345678901234567890")]     // UTC05: Boundary - 51 chars (exceeds max 50)
    [InlineData("1ADMIN")]                                                  // UTC06: Starts with digit
    [InlineData("_ADMIN")]                                                  // UTC07: Starts with underscore or lowercase ("admin")
    [InlineData("admin")]
    public void RoleCodeRegex_WithInvalidCode_ReturnsFalse(string code)
    {
        Assert.False(SlugHelper.RoleCodeRegex().IsMatch(code));
    }

    // =========================================================================
    // 3. ProjectKeyRegex: ^[A-Z][A-Z0-9]{1,9}$ (Length 2..10)
    // =========================================================================

    // UTC01 -> UTC03: Valid project keys (2 chars, 10 chars, uppercase alphanumeric)
    [Theory]
    [InlineData("AI")]          // UTC01: Boundary - 2 chars (min)
    [InlineData("SSAM2026")]    // UTC02: Normal - uppercase letters & digits
    [InlineData("PROJ123456")]  // UTC03: Boundary - 10 chars (max)
    public void ProjectKeyRegex_WithValidKey_ReturnsTrue(string key)
    {
        Assert.True(SlugHelper.ProjectKeyRegex().IsMatch(key));
    }

    // UTC04 -> UTC07: Invalid project keys (1 char, 11 chars, starts with digit, contains underscore/lowercase)
    [Theory]
    [InlineData("P")]               // UTC04: Boundary - 1 char (below min 2)
    [InlineData("PROJ1234567")]     // UTC05: Boundary - 11 chars (exceeds max 10)
    [InlineData("1PROJ")]           // UTC06: Starts with digit
    [InlineData("SSAM_1")]          // UTC07: Contains underscore or lowercase ("ssam")
    [InlineData("ssam")]
    public void ProjectKeyRegex_WithInvalidKey_ReturnsFalse(string key)
    {
        Assert.False(SlugHelper.ProjectKeyRegex().IsMatch(key));
    }
}
