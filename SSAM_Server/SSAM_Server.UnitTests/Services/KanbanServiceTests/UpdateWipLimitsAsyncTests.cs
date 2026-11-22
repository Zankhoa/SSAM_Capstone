using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class UpdateWipLimitsAsyncTests : KanbanServiceTestBase
{
    // UTC001: Updating a valid status WIP limit persists the limit and returns the board.
    [Fact]
    public async Task UpdateWipLimitsAsync_WithValidLimit_ReturnsBoardAndPersistsLimit()
    {
        var (_, _, todo, _) = SeedWorkflow();

        var result = await KanbanService.UpdateWipLimitsAsync(100, new UpdateWipLimitsRequest
        {
            Limits = new List<WipLimitItemInputDto> { new() { StatusId = todo.Id, Limit = 3 } }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        var limit = await Db.BoardWipLimits.FindAsync(100L, todo.Id);
        Assert.NotNull(limit);
        Assert.Equal(3, limit.WipLimit);
    }

    // UTC002: An unknown status cannot receive a WIP limit.
    [Fact]
    public async Task UpdateWipLimitsAsync_WhenStatusDoesNotBelongToProject_ReturnsNotFound()
    {
        SeedWorkflow();

        var result = await KanbanService.UpdateWipLimitsAsync(100, new UpdateWipLimitsRequest
        {
            Limits = new List<WipLimitItemInputDto> { new() { StatusId = 9999, Limit = 3 } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("STATUS_NOT_FOUND", result.ErrorCode);
    }

    // UTC003: A zero WIP limit is rejected.
    [Fact]
    public async Task UpdateWipLimitsAsync_WhenLimitIsLessThanOne_ReturnsValidationError()
    {
        var (_, _, todo, _) = SeedWorkflow();

        var result = await KanbanService.UpdateWipLimitsAsync(100, new UpdateWipLimitsRequest
        {
            Limits = new List<WipLimitItemInputDto> { new() { StatusId = todo.Id, Limit = 0 } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }
}
