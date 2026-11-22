using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SSAM_Server.DTOs.Testing;
using SSAM_Server.Models;
using SSAM_Server.Security;
using H = SSAM_Server.UnitTests.Services.TestingServiceTests.TestingSeedHelpersPlan;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public class UpdateEnvironmentAsyncTests : TestingServiceTestBase
{
    private const long ProjectId = 5;

    private (TestPlan Plan, TestEnvironment Env) SetupAuthorizedEnv()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id, "SIT", 1, url: "https://old", note: "old note");
        H.AllowGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);
        return (plan, env);
    }

    private async Task<TestEnvironment> ReloadAsync(long id)
    {
        Db.ChangeTracker.Clear();
        return await Db.TestEnvironments.SingleAsync(e => e.Id == id, TestContext.Current.CancellationToken);
    }

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC01: Update all fields -> 200 OK, DTO and DB updated, plan UpdatedAt touched
    [Fact]
    public async Task UpdateEnvironmentAsync_WithAllFields_ReturnsSuccessAndPersistsChanges()
    {
        var (plan, env) = SetupAuthorizedEnv();
        var request = new UpdateTestEnvironmentRequest { Name = "  Renamed  ", Url = "  https://new  ", Note = "  new note  ", Position = 4 };

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(env.Id, result.Data!.EnvId);
        Assert.Equal("Renamed", result.Data.Name);
        Assert.Equal("https://new", result.Data.Url);
        Assert.Equal("new note", result.Data.Note);
        Assert.Equal(4, result.Data.Position);
        Assert.Null(result.Data.CurrentBuild);
        Assert.Empty(result.Data.Builds);

        var saved = await ReloadAsync(env.Id);
        Assert.Equal("Renamed", saved.Name);
        Assert.Equal("https://new", saved.Url);
        Assert.Equal("new note", saved.Note);
        Assert.Equal(4, saved.Position);
        Assert.False(saved.IsDeleted);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(ProjectId, PermissionKeys.TestCaseUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC02: Empty request -> nothing changes except plan UpdatedAt
    [Fact]
    public async Task UpdateEnvironmentAsync_WithEmptyRequest_KeepsAllValues()
    {
        var (plan, env) = SetupAuthorizedEnv();

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("SIT", result.Data!.Name);
        Assert.Equal("https://old", result.Data.Url);
        Assert.Equal("old note", result.Data.Note);
        Assert.Equal(1, result.Data.Position);
        var saved = await ReloadAsync(env.Id);
        Assert.Equal("SIT", saved.Name);
        Assert.Equal("https://old", saved.Url);
        Assert.Equal("old note", saved.Note);
        Assert.Equal(1, saved.Position);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, (await Db.TestPlans.SingleAsync(p => p.Id == plan.Id, TestContext.Current.CancellationToken)).UpdatedAt);
    }

    // UTC03: Name null / empty / whitespace -> name is kept
    [Theory]
    [InlineData(null)]  // UTC03a
    [InlineData("")]    // UTC03b
    [InlineData("   ")] // UTC03c
    public async Task UpdateEnvironmentAsync_WhenNameIsBlank_KeepsExistingName(string? name)
    {
        var (_, env) = SetupAuthorizedEnv();

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Name = name, Note = "changed" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("SIT", result.Data!.Name);
        Assert.Equal("changed", result.Data.Note);
        Assert.Equal("SIT", (await ReloadAsync(env.Id)).Name);
    }

    // UTC04: Url set to empty string -> cleared (stored empty, DTO null)
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenUrlIsEmptyString_ClearsUrl()
    {
        var (_, env) = SetupAuthorizedEnv();

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Url = "" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Url);
        Assert.Equal("old note", result.Data.Note);
        Assert.Equal(string.Empty, (await ReloadAsync(env.Id)).Url);
    }

    // UTC05: Note set to whitespace -> trimmed to empty (DTO null); Url null -> untouched
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenNoteIsWhitespace_ClearsNoteAndKeepsUrl()
    {
        var (_, env) = SetupAuthorizedEnv();

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Note = "   " }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Note);
        Assert.Equal("https://old", result.Data.Url);
        var saved = await ReloadAsync(env.Id);
        Assert.Equal(string.Empty, saved.Note);
        Assert.Equal("https://old", saved.Url);
    }

    // UTC06: Position provided -> updated; position omitted -> unchanged
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenPositionOmitted_KeepsPosition()
    {
        var (_, env) = SetupAuthorizedEnv();

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Name = "Only name" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Position);
        Assert.Equal(1, (await ReloadAsync(env.Id)).Position);
    }

    // UTC07: Environment has builds -> DTO contains builds newest first with CurrentBuild = newest version, result counts and fixed bugs mapped
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenEnvironmentHasBuilds_ReturnsBuildsNewestFirst()
    {
        var (_, env) = SetupAuthorizedEnv();
        var user = new User { AccountId = 1, FullName = "Dev One", Email = "dev@example.com" };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Db.TestBuilds.Add(new TestBuild { EnvId = env.Id, Version = "1.0.0", DeployedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), DeployedById = user.Id, FixedIssueIds = "[]" });
        Db.TestBuilds.Add(new TestBuild { EnvId = env.Id, Version = "1.1.0", DeployedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), DeployedById = user.Id, FixedIssueIds = "[42]" });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Name = "SIT2" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.1.0", result.Data!.CurrentBuild);
        Assert.Equal(new[] { "1.1.0", "1.0.0" }, result.Data.Builds.Select(b => b.Version));
        Assert.Equal("2026-06-01T00:00:00.000Z", result.Data.Builds[0].DeployedAt);
        Assert.Equal("Dev One", result.Data.Builds[0].DeployedBy!.FullName);
        var fixedBug = Assert.Single(result.Data.Builds[0].FixedBugs);
        Assert.Equal(42, fixedBug.IssueId);
        Assert.Equal("#42", fixedBug.Key);
        Assert.Empty(result.Data.Builds[1].FixedBugs);
        Assert.All(result.Data.Builds, b => Assert.Equal(0, b.ResultCount));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC08: Environment does not exist -> 404 ENVIRONMENT_NOT_FOUND, guard never consulted
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenEnvironmentNotFound_ReturnsNotFound()
    {
        var result = await TestingService.UpdateEnvironmentAsync(999, new UpdateTestEnvironmentRequest { Name = "X" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ENVIRONMENT_NOT_FOUND", result.ErrorCode);
        AccessGuardMock.Verify(g => g.EnsureProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // UTC09: Environment is soft-deleted -> treated as not found, data untouched
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenEnvironmentIsSoftDeleted_ReturnsNotFound()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id, "OLD", 1, isDeleted: true);

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Name = "Revived" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ENVIRONMENT_NOT_FOUND", result.ErrorCode);
        Assert.Equal("OLD", (await ReloadAsync(env.Id)).Name);
    }

    // UTC10: Caller is not authenticated -> 401, environment unchanged
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id, "SIT");
        H.UnauthenticatedGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Name = "Hacked" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal("SIT", (await ReloadAsync(env.Id)).Name);
    }

    // UTC11: Caller lacks testcase.manage -> 403 FORBIDDEN, environment and plan unchanged
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenPermissionDenied_ReturnsForbidden()
    {
        var plan = H.AddPlan(Db, ProjectId);
        var env = H.AddEnv(Db, plan.Id, "SIT");
        H.DenyGuard(AccessGuardMock, ProjectId, PermissionKeys.TestCaseUpdate);

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Name = "Hacked", Position = 9 }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.TestCaseUpdate, result.RequiredPermission);
        var saved = await ReloadAsync(env.Id);
        Assert.Equal("SIT", saved.Name);
        Assert.Equal(1, saved.Position);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), (await Db.TestPlans.SingleAsync(TestContext.Current.CancellationToken)).UpdatedAt);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC12: Position boundary values (0, negative, int.MaxValue) -> accepted verbatim (no range validation)
    [Theory]
    [InlineData(0)]            // UTC12a
    [InlineData(-1)]           // UTC12b
    [InlineData(int.MaxValue)] // UTC12c
    public async Task UpdateEnvironmentAsync_WhenPositionIsAtBoundary_IsStoredVerbatim(int position)
    {
        var (_, env) = SetupAuthorizedEnv();

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Position = position }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(position, result.Data!.Position);
        Assert.Equal(position, (await ReloadAsync(env.Id)).Position);
    }

    // UTC13: Name with exactly 1 character -> accepted
    [Fact]
    public async Task UpdateEnvironmentAsync_WhenNameHasOneChar_Updates()
    {
        var (_, env) = SetupAuthorizedEnv();

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Name = "Z" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Z", result.Data!.Name);
    }

    // UTC14: Very long name (201 / 1000 chars) -> accepted (no max-length validation on update)
    [Theory]
    [InlineData(201)]  // UTC14a
    [InlineData(1000)] // UTC14b
    public async Task UpdateEnvironmentAsync_WhenNameIsLong_IsAcceptedBecauseNoLengthValidation(int length)
    {
        var (_, env) = SetupAuthorizedEnv();
        var name = new string('q', length);

        var result = await TestingService.UpdateEnvironmentAsync(env.Id, new UpdateTestEnvironmentRequest { Name = name }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(name, (await ReloadAsync(env.Id)).Name);
    }
}
