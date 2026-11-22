using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class SprintMethodTests : KanbanServiceTestBase
{
    // UTC001: Creating a valid sprint starts in PLANNED state.
    [Fact]
    public async Task CreateSprintAsync_WithValidRequest_ReturnsCreatedPlannedSprint()
    {
        SeedProject();

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest
        {
            Name = "  Sprint One  ", Goal = "Goal", StartDate = "2026-12-01", EndDate = "2026-12-14", CapacityPoints = 20
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var sprint = Assert.Single(Db.Sprints);
        Assert.Equal("Sprint One", sprint.Name);
        Assert.Equal(SprintStatuses.Planned, sprint.Status);
    }

    // UTC002: An invalid sprint date range is rejected.
    [Fact]
    public async Task CreateSprintAsync_WhenEndDatePrecedesStartDate_ReturnsValidationError()
    {
        SeedProject();

        var result = await KanbanService.CreateSprintAsync(100, new CreateSprintRequest { Name = "Sprint", StartDate = "2026-12-14", EndDate = "2026-12-01" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // UTC003: Updating a planned sprint persists the changed fields.
    [Fact]
    public async Task UpdateSprintAsync_WithValidRequest_ReturnsUpdatedSprint()
    {
        SeedProject();
        var sprint = SeedSprint();

        var result = await KanbanService.UpdateSprintAsync(sprint.Id, new UpdateSprintRequest { Name = "Updated Sprint", Goal = "Updated goal" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Sprint", (await Db.Sprints.FindAsync(sprint.Id))!.Name);
    }

    // UTC004: Starting a planned sprint changes it to ACTIVE and supplies default dates.
    [Fact]
    public async Task StartSprintAsync_WithPlannedSprint_ReturnsActiveSprint()
    {
        SeedProject();
        var sprint = SeedSprint();

        var result = await KanbanService.StartSprintAsync(sprint.Id, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var saved = await Db.Sprints.FindAsync(sprint.Id);
        Assert.Equal(SprintStatuses.Active, saved!.Status);
        Assert.Equal(DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime), saved.StartDate);
    }

    // UTC005: A second active sprint is rejected.
    [Fact]
    public async Task StartSprintAsync_WhenAnotherSprintIsActive_ReturnsConflict()
    {
        SeedProject();
        var active = SeedSprint(400, SprintStatuses.Active);
        var planned = SeedSprint(401, SprintStatuses.Planned);

        var result = await KanbanService.StartSprintAsync(planned.Id, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ACTIVE_SPRINT_EXISTS", result.ErrorCode);
    }

    // UTC006: Completing an active sprint changes it to COMPLETED.
    [Fact]
    public async Task CompleteSprintAsync_WithActiveSprint_ReturnsCompletedSprint()
    {
        SeedProject();
        var sprint = SeedSprint(400, SprintStatuses.Active);

        var result = await KanbanService.CompleteSprintAsync(sprint.Id, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SprintStatuses.Completed, (await Db.Sprints.FindAsync(sprint.Id))!.Status);
    }

    // UTC007: Completing a planned sprint is rejected.
    [Fact]
    public async Task CompleteSprintAsync_WhenSprintIsNotActive_ReturnsConflict()
    {
        SeedProject();
        var sprint = SeedSprint();

        var result = await KanbanService.CompleteSprintAsync(sprint.Id, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_SPRINT_STATE", result.ErrorCode);
    }
}
