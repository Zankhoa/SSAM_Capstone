using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class CreateSectionAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;

    private (TestPlan Plan, TestModule Module) SetupAuthorizedModule()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id, "Login", "LG");
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);
        return (plan, module);
    }

    private async Task<TestSection> ReloadAsync(long id)
    {
        Db.ChangeTracker.Clear();
        return await Db.TestSections.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: First top-level section in an empty module -> 201, code "1.", position 1, plan touched
    [Fact]
    public async Task CreateSectionAsync_FirstTopLevelSection_ReturnsCreatedWithCodeOne()
    {
        var (plan, module) = SetupAuthorizedModule();

        var result = await TestingService.CreateSectionAsync(module.Id,
            new CreateTestSectionRequest { Title = "  Overview  ", AnchorId = "  ov  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.SectionId > 0);
        Assert.Equal(module.Id, result.Data.ModuleId);
        Assert.Null(result.Data.ParentId);
        Assert.Equal("1.", result.Data.Code);
        Assert.Equal("Overview", result.Data.Title);
        Assert.Equal("ov", result.Data.AnchorId);
        Assert.False(result.Data.ReqChanged);
        Assert.Equal(1, result.Data.Position);

        var saved = await Db.TestSections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.Data.SectionId, saved.Id);
        Assert.Equal(module.Id, saved.ModuleId);
        Assert.Null(saved.ParentId);
        Assert.Equal("1.", saved.Code);
        Assert.Equal("Overview", saved.Title);
        Assert.Equal("ov", saved.AnchorId);
        Assert.Equal(1, saved.Position);
        Db.ChangeTracker.Clear();
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Second top-level section -> code "2.", position after the existing maximum
    [Fact]
    public async Task CreateSectionAsync_SecondTopLevelSection_GetsNextCode()
    {
        var (_, module) = SetupAuthorizedModule();
        H.AddSection(Db, module.Id, "1.", "First", 1);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = "Second" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2.", result.Data!.Code);
        Assert.Equal(2, result.Data.Position);
        Assert.Equal(2, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC03: Child section under a top-level parent -> code "1.1", ParentId set, position 1
    [Fact]
    public async Task CreateSectionAsync_ChildSection_GetsHierarchicalCode()
    {
        var (_, module) = SetupAuthorizedModule();
        var parent = H.AddSection(Db, module.Id, "1.", "Parent", 1);

        var result = await TestingService.CreateSectionAsync(module.Id,
            new CreateTestSectionRequest { ParentId = parent.Id, Title = "Child" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(parent.Id, result.Data!.ParentId);
        Assert.Equal("1.1", result.Data.Code);
        Assert.Equal(1, result.Data.Position);
        var saved = await ReloadAsync(result.Data.SectionId);
        Assert.Equal(parent.Id, saved.ParentId);
        Assert.Equal("1.1", saved.Code);
    }

    // UTC04: Second child under same parent -> code "1.2"; child under the 2nd top-level section -> code "2.1"
    [Fact]
    public async Task CreateSectionAsync_MultipleChildren_AreNumberedPerParent()
    {
        var (_, module) = SetupAuthorizedModule();
        var p1 = H.AddSection(Db, module.Id, "1.", "P1", 1);
        var p2 = H.AddSection(Db, module.Id, "2.", "P2", 2);
        H.AddSection(Db, module.Id, "1.1", "C1", 1, p1.Id);

        var second = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { ParentId = p1.Id, Title = "C2" }, TestContext.Current.CancellationToken);
        var underP2 = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { ParentId = p2.Id, Title = "D1" }, TestContext.Current.CancellationToken);

        Assert.True(second.IsSuccess);
        Assert.Equal("1.2", second.Data!.Code);
        Assert.Equal(2, second.Data.Position);
        Assert.True(underP2.IsSuccess);
        Assert.Equal("2.1", underP2.Data!.Code);
        Assert.Equal(1, underP2.Data.Position);
    }

    // UTC05: Position gaps are renumbered after creation (existing positions 1 and 5 -> new section ends up with position 3)
    [Fact]
    public async Task CreateSectionAsync_WhenExistingPositionsHaveGaps_RecodesSectionsSequentially()
    {
        var (_, module) = SetupAuthorizedModule();
        var s1 = H.AddSection(Db, module.Id, "1.", "A", 1);
        var s2 = H.AddSection(Db, module.Id, "9.", "B", 5);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = "C" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("3.", result.Data!.Code);
        Assert.Equal(3, result.Data.Position);
        var a = await ReloadAsync(s1.Id);
        var b = await ReloadAsync(s2.Id);
        Assert.Equal(("1.", 1), (a.Code, a.Position));
        Assert.Equal(("2.", 2), (b.Code, b.Position));
    }

    // UTC06: AnchorId null / empty / whitespace -> stored as null
    [Theory]
    [InlineData(null)]  // UTC06a
    [InlineData("")]    // UTC06b
    [InlineData("   ")] // UTC06c
    public async Task CreateSectionAsync_WhenAnchorIdBlank_StoresNull(string? anchor)
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = "T", AnchorId = anchor }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.AnchorId);
        Assert.Null((await Db.TestSections.SingleAsync(TestContext.Current.CancellationToken)).AnchorId);
    }

    // UTC07: Sections of other modules are ignored for numbering
    [Fact]
    public async Task CreateSectionAsync_IgnoresSectionsOfOtherModules()
    {
        var (plan, module) = SetupAuthorizedModule();
        var other = H.AddModule(Db, plan.Id, "Other", "OT", 2);
        H.AddSection(Db, other.Id, "1.", "X", 1);
        H.AddSection(Db, other.Id, "2.", "Y", 2);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = "Mine" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.", result.Data!.Code);
        Assert.Equal(1, result.Data.Position);
    }

    // UTC08: Duplicate title is allowed (no uniqueness rule)
    [Fact]
    public async Task CreateSectionAsync_WhenTitleDuplicatesExisting_IsAccepted()
    {
        var (_, module) = SetupAuthorizedModule();
        H.AddSection(Db, module.Id, "1.", "Same", 1);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = "Same" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await Db.TestSections.CountAsync(s => s.Title == "Same", TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC09: Module does not exist -> 404 MODULE_NOT_FOUND, guard never consulted
    [Fact]
    public async Task CreateSectionAsync_WhenModuleNotFound_ReturnsNotFound()
    {
        var result = await TestingService.CreateSectionAsync(999, new CreateTestSectionRequest { Title = "T" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("MODULE_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC10: Caller is not authenticated -> 401, nothing persisted
    [Fact]
    public async Task CreateSectionAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = "T" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Empty(await Db.TestSections.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC11: Caller lacks testcase.manage -> 403 FORBIDDEN, nothing persisted
    [Fact]
    public async Task CreateSectionAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id);
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = "T" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseCreate, result.RequiredPermission);
        Assert.Empty(await Db.TestSections.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC12: Title empty / whitespace / null -> 400 VALIDATION_ERROR on "title", nothing persisted
    [Theory]
    [InlineData("")]     // UTC12a
    [InlineData("   ")]  // UTC12b
    [InlineData(null)]   // UTC12c
    public async Task CreateSectionAsync_WhenTitleIsBlank_ReturnsValidationError(string? title)
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = title! }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(new[] { "Section title is required." }, result.Errors!["title"]);
        Assert.Empty(await Db.TestSections.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC13: ParentId refers to a section that does not exist -> 400 INVALID_PARENT
    [Fact]
    public async Task CreateSectionAsync_WhenParentDoesNotExist_ReturnsInvalidParent()
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { ParentId = 999, Title = "T" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Empty(await Db.TestSections.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC14: ParentId refers to a section of another module -> 400 INVALID_PARENT
    [Fact]
    public async Task CreateSectionAsync_WhenParentBelongsToAnotherModule_ReturnsInvalidParent()
    {
        var (plan, module) = SetupAuthorizedModule();
        var other = H.AddModule(Db, plan.Id, "Other", "OT", 2);
        var foreign = H.AddSection(Db, other.Id, "1.", "Foreign", 1);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { ParentId = foreign.Id, Title = "T" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Equal(1, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC15: ParentId refers to a section that is itself a child (3rd level) -> 400 INVALID_PARENT ("at most 2 levels")
    [Fact]
    public async Task CreateSectionAsync_WhenParentIsAlreadyAChild_ReturnsInvalidParent()
    {
        var (_, module) = SetupAuthorizedModule();
        var top = H.AddSection(Db, module.Id, "1.", "Top", 1);
        var child = H.AddSection(Db, module.Id, "1.1", "Child", 1, top.Id);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { ParentId = child.Id, Title = "Grandchild" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
        Assert.Equal("Sections support at most 2 levels.", result.ErrorMessage);
        Assert.Equal(2, await Db.TestSections.CountAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC16: Title with exactly 1 character -> created
    [Fact]
    public async Task CreateSectionAsync_WhenTitleHasOneChar_ReturnsCreated()
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = "A" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("A", result.Data!.Title);
    }

    // UTC17: Very long title (200 / 201 / 1000 chars) -> accepted (no max-length validation)
    [Theory]
    [InlineData(200)]  // UTC17a
    [InlineData(201)]  // UTC17b
    [InlineData(1000)] // UTC17c
    public async Task CreateSectionAsync_WhenTitleIsLong_IsAcceptedBecauseNoLengthValidation(int length)
    {
        var (_, module) = SetupAuthorizedModule();
        var title = new string('t', length);

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { Title = title }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(title, result.Data!.Title);
        Assert.Equal(title, (await Db.TestSections.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    // UTC18: ParentId 0 (lowest invalid id, still "has value") -> 400 INVALID_PARENT
    [Fact]
    public async Task CreateSectionAsync_WhenParentIdIsZero_ReturnsInvalidParent()
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.CreateSectionAsync(module.Id, new CreateTestSectionRequest { ParentId = 0, Title = "T" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PARENT", result.ErrorCode);
    }

    // UTC19: Module id 0 -> 404 MODULE_NOT_FOUND
    [Fact]
    public async Task CreateSectionAsync_WhenModuleIdIsZero_ReturnsNotFound()
    {
        var result = await TestingService.CreateSectionAsync(0, new CreateTestSectionRequest { Title = "T" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("MODULE_NOT_FOUND", result.ErrorCode);
    }
}
