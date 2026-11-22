using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class UpdateTestPlanAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;

    private TestPlan SetupAuthorizedPlan(string name = "Original", string status = TestPlanStatuses.Open)
    {
        var plan = H.AddPlan(Db, ProjectId, name, status, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);
        return plan;
    }

    private async Task<TestPlan> ReloadAsync(long id)
    {
        Db.ChangeTracker.Clear();
        return await Db.TestPlans.SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Update name, start date and end date together -> 200 OK, DTO and DB reflect new values, UpdatedAt = now
    [Fact]
    public async Task UpdateTestPlanAsync_WithAllFields_ReturnsSuccessAndPersistsChanges()
    {
        var plan = SetupAuthorizedPlan();
        var request = new UpdateTestPlanRequest { Name = "  Renamed  ", StartDate = "2026-02-01", EndDate = "2026-03-31" };

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(plan.Id, result.Data!.PlanId);
        Assert.Equal(ProjectId, result.Data.ProjectId);
        Assert.Equal("Renamed", result.Data.Name);
        Assert.Equal("2026-02-01", result.Data.StartDate);
        Assert.Equal("2026-03-31", result.Data.EndDate);
        Assert.Equal(TestPlanStatuses.Open, result.Data.Status);

        var saved = await ReloadAsync(plan.Id);
        Assert.Equal("Renamed", saved.Name);
        Assert.Equal(new DateOnly(2026, 2, 1), saved.StartDate);
        Assert.Equal(new DateOnly(2026, 3, 31), saved.EndDate);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, saved.UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Only name provided (dates not specified) -> name changes, dates untouched
    [Fact]
    public async Task UpdateTestPlanAsync_WhenOnlyNameProvided_KeepsDates()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { Name = "New name" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", result.Data!.Name);
        Assert.Equal("2026-01-01", result.Data.StartDate);
        Assert.Equal("2026-06-30", result.Data.EndDate);
        var saved = await ReloadAsync(plan.Id);
        Assert.Equal(new DateOnly(2026, 1, 1), saved.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 30), saved.EndDate);
    }

    // UTC03: Name null or whitespace -> name is kept (not validated), dates still applied
    [Theory]
    [InlineData(null)]  // UTC03a
    [InlineData("")]    // UTC03b
    [InlineData("   ")] // UTC03c
    public async Task UpdateTestPlanAsync_WhenNameIsBlank_KeepsExistingName(string? name)
    {
        var plan = SetupAuthorizedPlan("Keep me");

        var result = await TestingService.UpdateTestPlanAsync(plan.Id,
            new UpdateTestPlanRequest { Name = name, StartDate = "2026-04-01" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Keep me", result.Data!.Name);
        Assert.Equal("2026-04-01", result.Data.StartDate);
        Assert.Equal("Keep me", (await ReloadAsync(plan.Id)).Name);
    }

    // UTC04: StartDate explicitly set to null -> date is cleared (DTO null)
    [Fact]
    public async Task UpdateTestPlanAsync_WhenStartDateExplicitlyNull_ClearsStartDate()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { StartDate = null }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.StartDate);
        Assert.Equal("2026-06-30", result.Data.EndDate);
        Assert.Equal(DateOnly.MinValue, (await ReloadAsync(plan.Id)).StartDate);
    }

    // UTC05: EndDate explicitly set to null -> date is cleared (DTO null), start date untouched
    [Fact]
    public async Task UpdateTestPlanAsync_WhenEndDateExplicitlyNull_ClearsEndDate()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { EndDate = null }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.EndDate);
        Assert.Equal("2026-01-01", result.Data.StartDate);
        Assert.Equal(DateOnly.MinValue, (await ReloadAsync(plan.Id)).EndDate);
    }

    // UTC06: Date explicitly set to an invalid string -> treated as unset (cleared)
    [Fact]
    public async Task UpdateTestPlanAsync_WhenDateIsInvalidString_ClearsDate()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.UpdateTestPlanAsync(plan.Id,
            new UpdateTestPlanRequest { StartDate = "garbage", EndDate = "  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.StartDate);
        Assert.Null(result.Data.EndDate);
    }

    // UTC07: Date given in non-ISO but parseable format -> parsed via fallback
    [Fact]
    public async Task UpdateTestPlanAsync_WhenDateInNonIsoFormat_ParsesWithFallback()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.UpdateTestPlanAsync(plan.Id,
            new UpdateTestPlanRequest { StartDate = "03/15/2026" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-03-15", result.Data!.StartDate);
    }

    // UTC08: Empty request (nothing specified) -> still succeeds, only UpdatedAt is touched
    [Fact]
    public async Task UpdateTestPlanAsync_WithEmptyRequest_OnlyTouchesUpdatedAt()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Original", result.Data!.Name);
        Assert.Equal("2026-01-01", result.Data.StartDate);
        Assert.Equal("2026-06-30", result.Data.EndDate);
        var saved = await ReloadAsync(plan.Id);
        Assert.Equal("Original", saved.Name);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, saved.UpdatedAt);
    }

    // UTC09: Plan already handed over -> update is still allowed (no status rule in current implementation)
    [Fact]
    public async Task UpdateTestPlanAsync_WhenPlanHandedOver_StillUpdates()
    {
        var plan = SetupAuthorizedPlan(status: TestPlanStatuses.HandedOver);

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { Name = "After handover" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("After handover", result.Data!.Name);
        Assert.Equal(TestPlanStatuses.HandedOver, (await ReloadAsync(plan.Id)).Status);
    }

    // UTC10: Other plans in the same project are not modified
    [Fact]
    public async Task UpdateTestPlanAsync_DoesNotModifyOtherPlans()
    {
        var plan = SetupAuthorizedPlan();
        var other = H.AddPlan(Db, ProjectId, "Other");

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { Name = "Changed" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var savedOther = await ReloadAsync(other.Id);
        Assert.Equal("Other", savedOther.Name);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), savedOther.UpdatedAt);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC11: Plan does not exist -> 404 TEST_PLAN_NOT_FOUND and the access guard is never consulted
    [Fact]
    public async Task UpdateTestPlanAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        var result = await TestingService.UpdateTestPlanAsync(999, new UpdateTestPlanRequest { Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC12: Caller is not authenticated -> 401 and plan is unchanged
    [Fact]
    public async Task UpdateTestPlanAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId, "Original");
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { Name = "Hacked" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal("Original", (await ReloadAsync(plan.Id)).Name);
    }

    // UTC13: Caller lacks testcase.manage -> 403 FORBIDDEN and plan is unchanged
    [Fact]
    public async Task UpdateTestPlanAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId, "Original");
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { Name = "Hacked", StartDate = "2030-01-01" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseUpdate, result.RequiredPermission);
        var saved = await ReloadAsync(plan.Id);
        Assert.Equal("Original", saved.Name);
        Assert.Equal(new DateOnly(2026, 1, 1), saved.StartDate);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), saved.UpdatedAt);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC14: Name with exactly 1 character -> accepted
    [Fact]
    public async Task UpdateTestPlanAsync_WhenNameHasOneChar_Updates()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { Name = "A" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("A", result.Data!.Name);
    }

    // UTC15: Name with 200 characters -> accepted
    [Fact]
    public async Task UpdateTestPlanAsync_WhenNameHas200Chars_Updates()
    {
        var plan = SetupAuthorizedPlan();
        var name = new string('n', 200);

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(name, (await ReloadAsync(plan.Id)).Name);
    }

    // UTC16: Name with 201 characters -> accepted as-is (update has NO length validation, unlike create which limits to 200)
    [Fact]
    public async Task UpdateTestPlanAsync_WhenNameHas201Chars_IsAcceptedBecauseNoLengthValidation()
    {
        var plan = SetupAuthorizedPlan();
        var name = new string('n', 201);

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.Data!.Name.Length);
        Assert.Equal(name, (await ReloadAsync(plan.Id)).Name);
    }

    // UTC17: Date at the lower edge ("0001-01-01" == DateOnly.MinValue) -> reported as null by the DTO
    [Fact]
    public async Task UpdateTestPlanAsync_WhenDateIsMinValue_ReturnsNullDateInDto()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { StartDate = "0001-01-01" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.StartDate);
    }

    // UTC18: Date in year 1900 (DTO hides years <= 1900) -> null in DTO but persisted; year 1901 -> shown
    [Fact]
    public async Task UpdateTestPlanAsync_WhenYearIs1900_DtoHidesDateButYear1901IsShown()
    {
        var plan = SetupAuthorizedPlan();

        var hidden = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { StartDate = "1900-12-31" }, TestContext.Current.CancellationToken);
        Assert.True(hidden.IsSuccess);
        Assert.Null(hidden.Data!.StartDate);
        Assert.Equal(new DateOnly(1900, 12, 31), (await ReloadAsync(plan.Id)).StartDate);

        var shown = await TestingService.UpdateTestPlanAsync(plan.Id, new UpdateTestPlanRequest { StartDate = "1901-01-01" }, TestContext.Current.CancellationToken);
        Assert.True(shown.IsSuccess);
        Assert.Equal("1901-01-01", shown.Data!.StartDate);
    }
}
