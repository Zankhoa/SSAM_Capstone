using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class CreateIssuesFromRequirementsAsyncTests : KanbanServiceTestBase
{
    // UTC001: A requirement item creates an issue linked to the requirement.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WithValidRequirement_ReturnsCreatedIssue()
    {
        SeedWorkflow();
        SeedRequirementDocument();

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto>
            {
                new() { DocumentId = 500, ReqUid = "req-001", Type = "STORY" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var issue = Assert.Single(Db.Issues.Where(i => i.ProjectId == 100));
        Assert.Contains("REQ-001", issue.Title);
        Assert.Single(Db.IssueRequirementLinks);
    }

    // UTC002: An empty request is rejected.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WhenItemsAreEmpty_ReturnsValidationError()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // UTC003: A missing document returns not found.
    [Fact]
    public async Task CreateIssuesFromRequirementsAsync_WhenDocumentIsMissing_ReturnsNotFound()
    {
        SeedWorkflow();

        var result = await KanbanService.CreateIssuesFromRequirementsAsync(100, new CreateIssuesFromRequirementsRequest
        {
            Items = new List<CreateIssueFromRequirementItemDto> { new() { DocumentId = 9999, ReqUid = "REQ-001" } }
        }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_NOT_FOUND", result.ErrorCode);
    }
}
