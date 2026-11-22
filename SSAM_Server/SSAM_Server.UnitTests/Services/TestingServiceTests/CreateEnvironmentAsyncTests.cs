using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class CreateEnvironmentAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;
    private const long UserId = 7;

    private TestPlan SetupAuthorizedPlan()
    {
        var plan = H.AddPlan(Db, ProjectId);
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);
        return plan;
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Create first environment with name, url and note -> 201 Created, position 1, persisted + activity + plan touched
    [Fact]
    public async Task CreateEnvironmentAsync_WithFullRequestOnEmptyPlan_ReturnsCreatedWithPositionOne()
    {
        var plan = SetupAuthorizedPlan();
        var request = new CreateTestEnvironmentRequest { Name = "  Staging  ", Url = "  https://stg.example.com  ", Note = "  nightly  " };

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.EnvId > 0);
        Assert.Equal("Staging", result.Data.Name);
        Assert.Equal("https://stg.example.com", result.Data.Url);
        Assert.Equal("nightly", result.Data.Note);
        Assert.Equal(1, result.Data.Position);
        Assert.Null(result.Data.CurrentBuild);
        Assert.Empty(result.Data.Builds);

        var env = await Db.TestEnvironments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.Data.EnvId, env.Id);
        Assert.Equal(plan.Id, env.PlanId);
        Assert.Equal("Staging", env.Name);
        Assert.Equal("https://stg.example.com", env.Url);
        Assert.Equal("nightly", env.Note);
        Assert.Equal(1, env.Position);
        Assert.False(env.IsDeleted);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, env.CreatedAt);

        var activity = await Db.TestActivities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(plan.Id, activity.PlanId);
        Assert.Equal(TestActivityTypes.Setup, activity.Type);
        Assert.Equal(UserId, activity.ActorId);
        Assert.Equal("Added environment Staging", activity.Text);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, activity.At);

        Db.ChangeTracker.Clear();
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseCreate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Url and Note omitted -> stored as empty strings, DTO returns null for both
    [Fact]
    public async Task CreateEnvironmentAsync_WithoutUrlAndNote_StoresEmptyStringsAndReturnsNulls()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "UAT", Url = null, Note = null }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Url);
        Assert.Null(result.Data.Note);
        var env = await Db.TestEnvironments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, env.Url);
        Assert.Equal(string.Empty, env.Note);
    }

    // UTC03: Url and Note whitespace only -> trimmed to empty and returned as null in DTO
    [Fact]
    public async Task CreateEnvironmentAsync_WithWhitespaceUrlAndNote_TrimsToEmptyAndReturnsNulls()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "UAT", Url = "   ", Note = "  " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Url);
        Assert.Null(result.Data.Note);
        var env = await Db.TestEnvironments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, env.Url);
        Assert.Equal(string.Empty, env.Note);
    }

    // UTC04: Plan already has environments -> new environment gets max(position)+1
    [Fact]
    public async Task CreateEnvironmentAsync_WhenPlanHasEnvironments_UsesNextPosition()
    {
        var plan = SetupAuthorizedPlan();
        H.AddEnv(Db, plan.Id, "SIT", 1);
        H.AddEnv(Db, plan.Id, "UAT", 5);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "PROD" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Data!.Position);
        Assert.Equal(3, await Db.TestEnvironments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, (await Db.TestEnvironments.SingleAsync(e => e.Name == "PROD", TestContext.Current.CancellationToken)).Position);
    }

    // UTC05: Soft-deleted environments and environments of other plans are ignored when computing the position
    [Fact]
    public async Task CreateEnvironmentAsync_IgnoresDeletedAndForeignEnvironmentsForPosition()
    {
        var plan = SetupAuthorizedPlan();
        var otherPlan = H.AddPlan(Db, ProjectId, "Other");
        H.AddEnv(Db, plan.Id, "SIT", 2);
        H.AddEnv(Db, plan.Id, "OLD", 9, isDeleted: true);
        H.AddEnv(Db, otherPlan.Id, "FOREIGN", 20);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "NEW" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Position);
    }

    // UTC06: Only deleted environments exist -> position restarts at 1
    [Fact]
    public async Task CreateEnvironmentAsync_WhenOnlyDeletedEnvironmentsExist_StartsAtPositionOne()
    {
        var plan = SetupAuthorizedPlan();
        H.AddEnv(Db, plan.Id, "OLD", 4, isDeleted: true);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "NEW" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Position);
    }

    // UTC07: Name equal to an existing environment -> allowed (no uniqueness rule in current implementation)
    [Fact]
    public async Task CreateEnvironmentAsync_WhenNameDuplicatesExisting_StillCreates()
    {
        var plan = SetupAuthorizedPlan();
        H.AddEnv(Db, plan.Id, "SIT", 1);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "SIT" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(2, await Db.TestEnvironments.CountAsync(e => e.Name == "SIT", TestContext.Current.CancellationToken));
    }

    // UTC08: Plan is already handed over -> environment can still be added (no status rule in current implementation)
    [Fact]
    public async Task CreateEnvironmentAsync_WhenPlanHandedOver_StillCreates()
    {
        var plan = H.AddPlan(Db, ProjectId, "Done", TestPlanStatuses.HandedOver);
        CurrentUserMock.Setup(u => u.UserId).Returns(UserId);
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "HOTFIX" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await Db.TestEnvironments.CountAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC09: Plan does not exist -> 404 TEST_PLAN_NOT_FOUND, guard never consulted
    [Fact]
    public async Task CreateEnvironmentAsync_WhenPlanNotFound_ReturnsNotFound()
    {
        var result = await TestingService.CreateEnvironmentAsync(999, new CreateTestEnvironmentRequest { Name = "SIT" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("TEST_PLAN_NOT_FOUND", result.ErrorCode);
        Assert.Empty(await Db.TestEnvironments.ToListAsync(TestContext.Current.CancellationToken));
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC10: Caller is not authenticated -> 401, nothing persisted
    [Fact]
    public async Task CreateEnvironmentAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId);
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "SIT" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Empty(await Db.TestEnvironments.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC11: Caller lacks testcase.manage -> 403 FORBIDDEN, nothing persisted
    [Fact]
    public async Task CreateEnvironmentAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId);
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseCreate);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "SIT" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseCreate, result.RequiredPermission);
        Assert.Empty(await Db.TestEnvironments.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // UTC12: Name empty / whitespace / null -> 400 VALIDATION_ERROR on "name", nothing persisted
    [Theory]
    [InlineData("")]     // UTC12a
    [InlineData("   ")]  // UTC12b
    [InlineData(null)]   // UTC12c
    public async Task CreateEnvironmentAsync_WhenNameIsBlank_ReturnsValidationError(string? name)
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = name! }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(new[] { "Environment name is required." }, result.Errors!["name"]);
        Assert.Empty(await Db.TestEnvironments.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db.TestActivities.ToListAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC13: Name with exactly 1 character (min) -> created
    [Fact]
    public async Task CreateEnvironmentAsync_WhenNameHasOneChar_ReturnsCreated()
    {
        var plan = SetupAuthorizedPlan();

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "X" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("X", result.Data!.Name);
    }

    // UTC14: Very long name (200 / 201 / 1000 chars) -> accepted, there is no max-length validation for environment names
    [Theory]
    [InlineData(200)]  // UTC14a
    [InlineData(201)]  // UTC14b
    [InlineData(1000)] // UTC14c
    public async Task CreateEnvironmentAsync_WhenNameIsLong_IsAcceptedBecauseNoLengthValidation(int length)
    {
        var plan = SetupAuthorizedPlan();
        var name = new string('e', length);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(length, result.Data!.Name.Length);
        Assert.Equal(name, (await Db.TestEnvironments.SingleAsync(TestContext.Current.CancellationToken)).Name);
    }

    // UTC15: Existing max position is 0 (boundary of the "?? 0" path) -> new position is 1
    [Fact]
    public async Task CreateEnvironmentAsync_WhenExistingMaxPositionIsZero_NewPositionIsOne()
    {
        var plan = SetupAuthorizedPlan();
        H.AddEnv(Db, plan.Id, "SIT", 0);

        var result = await TestingService.CreateEnvironmentAsync(plan.Id, new CreateTestEnvironmentRequest { Name = "UAT" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Position);
    }
}
