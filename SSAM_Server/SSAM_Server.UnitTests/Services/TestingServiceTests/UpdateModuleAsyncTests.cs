using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class UpdateModuleAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;

    private (TestPlan Plan, TestModule Module) SetupAuthorizedModule()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id, "Login", "LG", 1, caseSeq: 3);
        H.AddSection(Db, module.Id, "1.", "Main", 1, anchorId: "main");
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);
        return (plan, module);
    }

    private async Task<TestModule> ReloadAsync(long id)
    {
        Db.ChangeTracker.Clear();
        return await Db.TestModules.SingleAsync(m => m.Id == id, TestContext.Current.CancellationToken);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Update name, short code and position -> 200 OK, DTO and DB updated, plan touched, sections/CaseSeq preserved
    [Fact]
    public async Task UpdateModuleAsync_WithAllFields_ReturnsSuccessAndPersistsChanges()
    {
        var (plan, module) = SetupAuthorizedModule();

        var result = await TestingService.UpdateModuleAsync(module.Id,
            new UpdateTestModuleRequest { Name = "  Sign In  ", ShortCode = "  si  ", Position = 3 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(module.Id, result.Data!.ModuleId);
        Assert.Equal("Sign In", result.Data.Name);
        Assert.Equal("SI", result.Data.ShortCode);
        Assert.Equal(3, result.Data.Position);
        Assert.Equal(0, result.Data.DocumentId);
        Assert.Equal("SI", result.Data.DocumentCode);
        Assert.Equal("Sign In", result.Data.DocumentTitle);
        var sec = Assert.Single(result.Data.Sections);
        Assert.Equal("Main", sec.Title);

        var saved = await ReloadAsync(module.Id);
        Assert.Equal("Sign In", saved.Name);
        Assert.Equal("SI", saved.ShortCode);
        Assert.Equal(3, saved.Position);
        Assert.Equal(3, saved.CaseSeq);
        Assert.Equal(plan.Id, saved.PlanId);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Empty request -> values unchanged, plan UpdatedAt touched
    [Fact]
    public async Task UpdateModuleAsync_WithEmptyRequest_KeepsAllValues()
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Login", result.Data!.Name);
        Assert.Equal("LG", result.Data.ShortCode);
        Assert.Equal(1, result.Data.Position);
        var saved = await ReloadAsync(module.Id);
        Assert.Equal("Login", saved.Name);
        Assert.Equal("LG", saved.ShortCode);
        Assert.Equal(1, saved.Position);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
    }

    // UTC03: Name and short code null / empty / whitespace -> kept as is
    [Theory]
    [InlineData(null)]  // UTC03a
    [InlineData("")]    // UTC03b
    [InlineData("   ")] // UTC03c
    public async Task UpdateModuleAsync_WhenNameAndShortCodeBlank_KeepsExistingValues(string? blank)
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.UpdateModuleAsync(module.Id,
            new UpdateTestModuleRequest { Name = blank, ShortCode = blank, Position = 2 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Login", result.Data!.Name);
        Assert.Equal("LG", result.Data.ShortCode);
        Assert.Equal(2, result.Data.Position);
        var saved = await ReloadAsync(module.Id);
        Assert.Equal("Login", saved.Name);
        Assert.Equal("LG", saved.ShortCode);
    }

    // UTC04: Only short code provided -> upper-cased and trimmed, other fields untouched
    [Fact]
    public async Task UpdateModuleAsync_WhenOnlyShortCodeProvided_UpperCasesIt()
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { ShortCode = " abc " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC", result.Data!.ShortCode);
        Assert.Equal("Login", result.Data.Name);
    }

    // UTC05: New short code duplicates another module's code -> accepted (no uniqueness check on update)
    [Fact]
    public async Task UpdateModuleAsync_WhenShortCodeDuplicatesAnotherModule_IsAccepted()
    {
        var (plan, module) = SetupAuthorizedModule();
        H.AddModule(Db, plan.Id, "Other", "OTH", 2);

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { ShortCode = "oth" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("OTH", result.Data!.ShortCode);
        Assert.Equal(2, await Db.TestModules.CountAsync(m => m.ShortCode == "OTH", TestContext.Current.CancellationToken));
    }

    // UTC06: Other modules are not modified
    [Fact]
    public async Task UpdateModuleAsync_DoesNotModifyOtherModules()
    {
        var (plan, module) = SetupAuthorizedModule();
        var other = H.AddModule(Db, plan.Id, "Other", "OTH", 2);

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { Name = "Changed", Position = 9 }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var savedOther = await ReloadAsync(other.Id);
        Assert.Equal("Other", savedOther.Name);
        Assert.Equal("OTH", savedOther.ShortCode);
        Assert.Equal(2, savedOther.Position);
    }

    // UTC07: Module linked to a document -> DTO exposes document id, code and title
    [Fact]
    public async Task UpdateModuleAsync_WhenModuleLinkedToDocument_ReturnsDocumentInfo()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var doc = H.AddDocument(Db, ProjectId, "SRS Login", "SRS-1");
        var module = H.AddModule(Db, plan.Id, "Login", "LG", 1, documentId: doc.Id);
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { Name = "Login v2" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(doc.Id, result.Data!.DocumentId);
        Assert.Equal("SRS-1", result.Data.DocumentCode);
        Assert.Equal("SRS Login", result.Data.DocumentTitle);
        Assert.Equal("Login v2", result.Data.Name);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC08: Module does not exist -> 404 MODULE_NOT_FOUND, guard never consulted
    [Fact]
    public async Task UpdateModuleAsync_WhenModuleNotFound_ReturnsNotFound()
    {
        var result = await TestingService.UpdateModuleAsync(999, new UpdateTestModuleRequest { Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("MODULE_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC09: Caller is not authenticated -> 401, module unchanged
    [Fact]
    public async Task UpdateModuleAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id, "Login", "LG");
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { Name = "Hacked" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal("Login", (await ReloadAsync(module.Id)).Name);
    }

    // UTC10: Caller lacks testcase.manage -> 403 FORBIDDEN, module and plan unchanged
    [Fact]
    public async Task UpdateModuleAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var module = H.AddModule(Db, plan.Id, "Login", "LG");
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { Name = "Hacked", ShortCode = "HK", Position = 9 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseUpdate, result.RequiredPermission);
        var saved = await ReloadAsync(module.Id);
        Assert.Equal("Login", saved.Name);
        Assert.Equal("LG", saved.ShortCode);
        Assert.Equal(1, saved.Position);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC11: Position boundary values (0, negative, int.MaxValue) -> stored verbatim
    [Theory]
    [InlineData(0)]            // UTC11a
    [InlineData(-1)]           // UTC11b
    [InlineData(int.MaxValue)] // UTC11c
    public async Task UpdateModuleAsync_WhenPositionAtBoundary_IsStoredVerbatim(int position)
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { Position = position }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(position, result.Data!.Position);
        Assert.Equal(position, (await ReloadAsync(module.Id)).Position);
    }

    // UTC12: Name with exactly 1 character -> accepted
    [Fact]
    public async Task UpdateModuleAsync_WhenNameHasOneChar_Updates()
    {
        var (_, module) = SetupAuthorizedModule();

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { Name = "L" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("L", result.Data!.Name);
    }

    // UTC13: Very long name / short code (201 / 1000 chars) -> accepted (no max-length validation on update)
    [Theory]
    [InlineData(201)]  // UTC13a
    [InlineData(1000)] // UTC13b
    public async Task UpdateModuleAsync_WhenValuesAreLong_AreAcceptedBecauseNoLengthValidation(int length)
    {
        var (_, module) = SetupAuthorizedModule();
        var name = new string('n', length);
        var code = new string('c', length);

        var result = await TestingService.UpdateModuleAsync(module.Id, new UpdateTestModuleRequest { Name = name, ShortCode = code }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var saved = await ReloadAsync(module.Id);
        Assert.Equal(name, saved.Name);
        Assert.Equal(code.ToUpperInvariant(), saved.ShortCode);
    }
}
