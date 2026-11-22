using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class CreateTestPlanAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;
    private const long UserId = 7;

    private void SetupAuthorized()
    {
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Full valid request (name, dates, custom environments) -> 201 Created with full plan DTO and persisted plan/envs/activity
    [Fact]
    public async Task CreateTestPlanAsync_WithFullValidRequest_ReturnsCreated()
    {
        SetupAuthorized();
        var request = new CreateTestPlanRequest
        {
            Name = "  Release 1.0  ",
            StartDate = "2026-12-01",
            EndDate = "2026-12-31",
            Environments = new List<string> { "DEV", " QA ", "STAGING" }
        };

        var result = await TestingService.CreateTestPlanAsync(ProjectId, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.PlanId > 0);
        Assert.Equal(ProjectId, result.Data.ProjectId);
        Assert.Equal("Release 1.0", result.Data.Name);
        Assert.Equal("2026-12-01", result.Data.StartDate);
        Assert.Equal("2026-12-31", result.Data.EndDate);
        Assert.Equal(TestPlanStatuses.Open, result.Data.Status);
        Assert.Equal("2026-11-21T10:00:00.000Z", result.Data.CreatedAt);
        Assert.Null(result.Data.Handover);
        Assert.Empty(result.Data.Modules);
        Assert.Empty(result.Data.Assignments);
        Assert.Empty(result.Data.People);
        Assert.Equal(new[] { "DEV", "QA", "STAGING" }, result.Data.Environments.Select(e => e.Name));
        Assert.Equal(new[] { 1, 2, 3 }, result.Data.Environments.Select(e => e.Position));
        Assert.All(result.Data.Environments, e =>
        {
            Assert.Null(e.Url);
            Assert.Null(e.Note);
            Assert.Null(e.CurrentBuild);
            Assert.Empty(e.Builds);
        });
        Assert.Equal(14, result.Data.Layout.Order.Count);

        var plan = await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.Data.PlanId, plan.Id);
        Assert.Equal("Release 1.0", plan.Name);
        Assert.Equal(new DateOnly(2026, 12, 1), plan.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), plan.EndDate);
        Assert.Equal(TestPlanStatuses.Open, plan.Status);
        Assert.Equal(UserId, plan.CreatedById);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, plan.CreatedAt);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, plan.UpdatedAt);
        Assert.False(string.IsNullOrWhiteSpace(plan.SheetLayout));

        var envs = await Db.TestEnvironments.OrderBy(e => e.Position).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, envs.Count);
        Assert.All(envs, e =>
        {
            Assert.Equal(plan.Id, e.PlanId);
            Assert.Equal(string.Empty, e.Url);
            Assert.Equal(string.Empty, e.Note);
            Assert.False(e.IsDeleted);
        });

        var activity = await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(plan.Id, activity.PlanId);
        Assert.Equal(TestActivityTypes.Setup, activity.Type);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Created test plan Release 1.0", activity.Text);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, activity.At);

        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Environments is null -> default environments SIT and UAT are created
    [Fact]
    public async Task CreateTestPlanAsync_WhenEnvironmentsIsNull_CreatesDefaultSitAndUat()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan", Environments = null }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "SIT", "UAT" }, result.Data!.Environments.Select(e => e.Name));
        var envs = await Db.TestEnvironments.OrderBy(e => e.Position).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "SIT", "UAT" }, envs.Select(e => e.Name));
        Assert.Equal(new[] { 1, 2 }, envs.Select(e => e.Position));
    }

    // UTC03: Environments is an empty list -> default environments SIT and UAT are created
    [Fact]
    public async Task CreateTestPlanAsync_WhenEnvironmentsIsEmpty_CreatesDefaultSitAndUat()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan", Environments = new List<string>() }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "SIT", "UAT" }, (await Db.TestEnvironments.OrderBy(e => e.Position).ToListAsync(TestContext.Current.CancellationToken)).Select(e => e.Name));
    }

    // UTC04: Environments contain only blank entries -> blank entries are ignored and defaults are used
    [Fact]
    public async Task CreateTestPlanAsync_WhenEnvironmentsAreAllBlank_CreatesDefaultSitAndUat()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan", Environments = new List<string> { "", "   ", null! } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "SIT", "UAT" }, result.Data!.Environments.Select(e => e.Name));
        Assert.Equal(2, await Db.TestEnvironments.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC05: Environment names duplicated ignoring case/whitespace -> duplicates removed (first spelling kept), blanks skipped
    [Fact]
    public async Task CreateTestPlanAsync_WhenEnvironmentsHaveDuplicatesAndBlanks_DeduplicatesCaseInsensitively()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId, new CreateTestPlanRequest
        {
            Name = "Plan",
            Environments = new List<string> { "Prod", " ", "prod", " PROD ", "Dev" }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Prod", "Dev" }, result.Data!.Environments.Select(e => e.Name));
        Assert.Equal(new[] { 1, 2 }, result.Data.Environments.Select(e => e.Position));
        Assert.Equal(2, await Db.TestEnvironments.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC06: Dates omitted -> plan is created with no dates (DTO returns null dates)
    [Fact]
    public async Task CreateTestPlanAsync_WhenDatesAreNull_CreatesPlanWithNullDates()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan", StartDate = null, EndDate = null }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.StartDate);
        Assert.Null(result.Data.EndDate);
        var plan = await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DateOnly.MinValue, plan.StartDate);
        Assert.Equal(DateOnly.MinValue, plan.EndDate);
    }

    // UTC07: Date in non-ISO but parseable format -> parsed with the invariant culture fallback
    [Fact]
    public async Task CreateTestPlanAsync_WhenDateUsesNonIsoFormat_ParsesWithFallback()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan", StartDate = "12/01/2026", EndDate = " 2026-12-31 " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-12-01", result.Data!.StartDate);
        Assert.Equal("2026-12-31", result.Data.EndDate);
    }

    // UTC08: Date is garbage / whitespace -> treated as unspecified (null in DTO) and plan is still created
    [Theory]
    [InlineData("not-a-date")] // UTC08a
    [InlineData("   ")]        // UTC08b
    [InlineData("2026-13-45")] // UTC08c
    public async Task CreateTestPlanAsync_WhenDateIsInvalid_TreatsDateAsUnset(string badDate)
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan", StartDate = badDate, EndDate = badDate }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.StartDate);
        Assert.Null(result.Data.EndDate);
        Assert.Equal(1, await Db.TestPlans.CountAsync(TestContext.Current.CancellationToken));
    }

    // UTC09: Plans in another project are untouched; new plan belongs to the requested project only
    [Fact]
    public async Task CreateTestPlanAsync_WhenOtherPlansExist_OnlyAddsNewPlanForRequestedProject()
    {
        SetupAuthorized();
        H.AddPlan(Db, projectId: 99, name: "Other project plan");

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Mine" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await Db.TestPlans.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await Db.TestPlans.CountAsync(p => p.ProjectId == ProjectId, TestContext.Current.CancellationToken));
        Assert.Equal(2, await Db.TestEnvironments.CountAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC10: Caller is not authenticated -> 401 and nothing is persisted
    [Fact]
    public async Task CreateTestPlanAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Null(result.Data);
        Assert.Empty(await Db.TestPlans.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestEnvironments.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC11: Caller lacks testcase.manage permission -> 403 FORBIDDEN and nothing is persisted
    [Fact]
    public async Task CreateTestPlanAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseCreate, result.RequiredPermission);
        Assert.Empty(await Db.TestPlans.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestEnvironments.ToListAsync(TestContext.Current.CancellationToken));
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC12: Name is empty / whitespace / null -> 400 VALIDATION_ERROR on "name" and nothing is persisted
    [Theory]
    [InlineData("")]     // UTC12a
    [InlineData("   ")]  // UTC12b
    [InlineData(null)]   // UTC12c
    public async Task CreateTestPlanAsync_WhenNameIsBlank_ReturnsValidationError(string? name)
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = name! }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.NotNull(result.Errors);
        Assert.Equal(new[] { "Plan name is required (max 200 chars)." }, result.Errors["name"]);
        Assert.Empty(await Db.TestPlans.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestEnvironments.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC13: Name longer than 200 chars -> 400 VALIDATION_ERROR on "name"
    [Fact]
    public async Task CreateTestPlanAsync_WhenNameExceeds200Chars_ReturnsValidationError()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = new string('a', 201) }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("name"));
        Assert.Empty(await Db.TestPlans.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC14: Name has exactly 1 character (min) -> created
    [Fact]
    public async Task CreateTestPlanAsync_WhenNameHasOneChar_ReturnsCreated()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "A" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("A", result.Data!.Name);
        Assert.Equal("A", (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).Name);
    }

    // UTC15: Name has exactly 200 characters (max) -> created
    [Fact]
    public async Task CreateTestPlanAsync_WhenNameHas200Chars_ReturnsCreated()
    {
        SetupAuthorized();
        var name = new string('b', 200);

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(name, result.Data!.Name);
        Assert.Equal(name, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).Name);
    }

    // UTC16: Name has 200 chars plus surrounding spaces (length checked after trimming) -> created with trimmed name
    [Fact]
    public async Task CreateTestPlanAsync_WhenNameHas200CharsWithSurroundingSpaces_IsTrimmedBeforeLengthCheck()
    {
        SetupAuthorized();
        var name = new string('c', 200);

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = $"   {name}   " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(name, result.Data!.Name);
    }

    // UTC17: Name has 201 characters (max + 1) -> validation error (covered with UTC13 content, here with trimmed boundary)
    [Fact]
    public async Task CreateTestPlanAsync_WhenNameHas201CharsAfterTrim_ReturnsValidationError()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = $" {new string('d', 201)} " }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Empty(await Db.TestPlans.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC18: Exactly one custom environment (min non-default) -> only that environment is created (no defaults added)
    [Fact]
    public async Task CreateTestPlanAsync_WhenSingleCustomEnvironment_CreatesOnlyThatEnvironment()
    {
        SetupAuthorized();

        var result = await TestingService.CreateTestPlanAsync(ProjectId,
            new CreateTestPlanRequest { Name = "Plan", Environments = new List<string> { "Prod" } }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var env = Assert.Single(result.Data!.Environments);
        Assert.Equal("Prod", env.Name);
        Assert.Equal(1, env.Position);
        Assert.Equal(1, await Db.TestEnvironments.CountAsync(TestContext.Current.CancellationToken));
    }
}
