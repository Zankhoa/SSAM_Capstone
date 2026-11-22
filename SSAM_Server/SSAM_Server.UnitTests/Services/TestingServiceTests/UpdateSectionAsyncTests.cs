using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class UpdateSectionAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;

    private (TestPlan Plan, TestModule Module, TestSection Section) SetupAuthorizedSection()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id, "Login", "LG");
        var section = H.AddSection(Db, module.Id, "1.", "Original", 1, anchorId: "orig");
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);
        return (plan, module, section);
    }

    private async Task<TestSection> ReloadAsync(long id)
    {
        Db.ChangeTracker.Clear();
        return await Db.TestSections.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Update title and anchor -> 200 OK, DTO and DB updated, plan touched
    [Fact]
    public async Task UpdateSectionAsync_WithTitleAndAnchor_ReturnsSuccessAndPersistsChanges()
    {
        var (_, module, section) = SetupAuthorizedSection();

        var result = await TestingService.UpdateSectionAsync(section.Id,
            new UpdateTestSectionRequest { Title = "  Renamed  ", AnchorId = "  new-anchor  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(section.Id, result.Data!.SectionId);
        Assert.Equal(module.Id, result.Data.ModuleId);
        Assert.Null(result.Data.ParentId);
        Assert.Equal("1.", result.Data.Code);
        Assert.Equal("Renamed", result.Data.Title);
        Assert.Equal("new-anchor", result.Data.AnchorId);
        Assert.False(result.Data.ReqChanged);
        Assert.Equal(1, result.Data.Position);

        var saved = await ReloadAsync(section.Id);
        Assert.Equal("Renamed", saved.Title);
        Assert.Equal("new-anchor", saved.AnchorId);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Empty request -> values unchanged, plan UpdatedAt touched
    [Fact]
    public async Task UpdateSectionAsync_WithEmptyRequest_KeepsAllValues()
    {
        var (_, _, section) = SetupAuthorizedSection();

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Original", result.Data!.Title);
        Assert.Equal("orig", result.Data.AnchorId);
        Assert.Equal(1, result.Data.Position);
        var saved = await ReloadAsync(section.Id);
        Assert.Equal("Original", saved.Title);
        Assert.Equal("orig", saved.AnchorId);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
    }

    // UTC03: Title null / empty / whitespace -> title kept
    [Theory]
    [InlineData(null)]  // UTC03a
    [InlineData("")]    // UTC03b
    [InlineData("   ")] // UTC03c
    public async Task UpdateSectionAsync_WhenTitleBlank_KeepsExistingTitle(string? title)
    {
        var (_, _, section) = SetupAuthorizedSection();

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest { Title = title, AnchorId = "x" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Original", result.Data!.Title);
        Assert.Equal("x", result.Data.AnchorId);
    }

    // UTC04: AnchorId empty / whitespace -> cleared to null; AnchorId null -> unchanged
    [Theory]
    [InlineData("")]    // UTC04a
    [InlineData("   ")] // UTC04b
    public async Task UpdateSectionAsync_WhenAnchorIdBlank_ClearsAnchor(string anchor)
    {
        var (_, _, section) = SetupAuthorizedSection();

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest { AnchorId = anchor }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.AnchorId);
        Assert.Null((await ReloadAsync(section.Id)).AnchorId);
    }

    // UTC05: Moving a section to a later position -> sections are recoded sequentially by position
    [Fact]
    public async Task UpdateSectionAsync_WhenPositionChanges_RecodesSiblingsInNewOrder()
    {
        var (_, module, s1) = SetupAuthorizedSection();
        var s2 = H.AddSection(Db, module.Id, "2.", "Second", 2);
        var s3 = H.AddSection(Db, module.Id, "3.", "Third", 3);

        var result = await TestingService.UpdateSectionAsync(s1.Id, new UpdateTestSectionRequest { Position = 10 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("3.", result.Data!.Code);
        Assert.Equal(3, result.Data.Position);
        var b = await ReloadAsync(s2.Id);
        var c = await ReloadAsync(s3.Id);
        var a = await ReloadAsync(s1.Id);
        Assert.Equal(("1.", 1), (b.Code, b.Position));
        Assert.Equal(("2.", 2), (c.Code, c.Position));
        Assert.Equal(("3.", 3), (a.Code, a.Position));
    }

    // UTC06: Moving a section to an earlier position -> it becomes first
    [Fact]
    public async Task UpdateSectionAsync_WhenMovedToFront_BecomesFirstAfterRecode()
    {
        var (_, module, s1) = SetupAuthorizedSection();
        var s2 = H.AddSection(Db, module.Id, "2.", "Second", 2);
        var s3 = H.AddSection(Db, module.Id, "3.", "Third", 3);

        var result = await TestingService.UpdateSectionAsync(s3.Id, new UpdateTestSectionRequest { Position = 0 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.", result.Data!.Code);
        Assert.Equal(1, result.Data.Position);
        Assert.Equal("2.", (await ReloadAsync(s1.Id)).Code);
        Assert.Equal("3.", (await ReloadAsync(s2.Id)).Code);
    }

    // UTC07: Child sections are recoded relative to their parent ("n.m") after an update
    [Fact]
    public async Task UpdateSectionAsync_RecodesChildSectionsUnderTheirParent()
    {
        var (_, module, parent) = SetupAuthorizedSection();
        var c1 = H.AddSection(Db, module.Id, "x", "C1", 7, parent.Id);
        var c2 = H.AddSection(Db, module.Id, "y", "C2", 9, parent.Id);

        var result = await TestingService.UpdateSectionAsync(c2.Id, new UpdateTestSectionRequest { Title = "C2 renamed" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(parent.Id, result.Data!.ParentId);
        Assert.Equal("1.2", result.Data.Code);
        Assert.Equal(2, result.Data.Position);
        var first = await ReloadAsync(c1.Id);
        Assert.Equal(("1.1", 1), (first.Code, first.Position));
    }

    // UTC08: Sections of other modules are not touched
    [Fact]
    public async Task UpdateSectionAsync_DoesNotRecodeSectionsOfOtherModules()
    {
        var (plan, _, section) = SetupAuthorizedSection();
        var other = H.AddModule(Db, plan.Id, "Other", "OT", 2);
        var foreign = H.AddSection(Db, other.Id, "weird", "Foreign", 42);

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest { Title = "Changed" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var savedForeign = await ReloadAsync(foreign.Id);
        Assert.Equal("weird", savedForeign.Code);
        Assert.Equal(42, savedForeign.Position);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC09: Section does not exist -> 404 SECTION_NOT_FOUND, guard never consulted
    [Fact]
    public async Task UpdateSectionAsync_WhenSectionNotFound_ReturnsNotFound()
    {
        var result = await TestingService.UpdateSectionAsync(999, new UpdateTestSectionRequest { Title = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC10: Caller is not authenticated -> 401, section unchanged
    [Fact]
    public async Task UpdateSectionAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        var section = H.AddSection(Db, module.Id, "1.", "Original", 1);
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest { Title = "Hacked" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal("Original", (await ReloadAsync(section.Id)).Title);
    }

    // UTC11: Caller lacks testcase.manage -> 403 FORBIDDEN, section and plan unchanged
    [Fact]
    public async Task UpdateSectionAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        var section = H.AddSection(Db, module.Id, "1.", "Original", 1, anchorId: "a");
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest { Title = "Hacked", AnchorId = "b", Position = 5 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseUpdate, result.RequiredPermission);
        var saved = await ReloadAsync(section.Id);
        Assert.Equal("Original", saved.Title);
        Assert.Equal("a", saved.AnchorId);
        Assert.Equal(1, saved.Position);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC12: Position boundary values (0, negative, int.MaxValue) -> accepted; sections are then renumbered to 1..n
    [Theory]
    [InlineData(0)]            // UTC12a
    [InlineData(-1)]           // UTC12b
    [InlineData(int.MaxValue)] // UTC12c
    public async Task UpdateSectionAsync_WhenPositionAtBoundary_IsAcceptedAndRenumbered(int position)
    {
        var (_, _, section) = SetupAuthorizedSection();

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest { Position = position }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Position);
        Assert.Equal("1.", result.Data.Code);
        Assert.Equal(1, (await ReloadAsync(section.Id)).Position);
    }

    // UTC13: Title with exactly 1 character -> accepted
    [Fact]
    public async Task UpdateSectionAsync_WhenTitleHasOneChar_Updates()
    {
        var (_, _, section) = SetupAuthorizedSection();

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest { Title = "Z" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Z", result.Data!.Title);
    }

    // UTC14: Very long title (201 / 1000 chars) -> accepted (no max-length validation)
    [Theory]
    [InlineData(201)]  // UTC14a
    [InlineData(1000)] // UTC14b
    public async Task UpdateSectionAsync_WhenTitleIsLong_IsAcceptedBecauseNoLengthValidation(int length)
    {
        var (_, _, section) = SetupAuthorizedSection();
        var title = new string('t', length);

        var result = await TestingService.UpdateSectionAsync(section.Id, new UpdateTestSectionRequest { Title = title }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(title, (await ReloadAsync(section.Id)).Title);
    }

    // UTC15: Section id 0 -> 404 SECTION_NOT_FOUND
    [Fact]
    public async Task UpdateSectionAsync_WhenIdIsZero_ReturnsNotFound()
    {
        var result = await TestingService.UpdateSectionAsync(0, new UpdateTestSectionRequest { Title = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SECTION_NOT_FOUND", result.ErrorCode);
    }
}
