using Microsoft.AspNetCore.Http;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class UpdateIssueRequirementsAsyncTests : KanbanServiceTestBase
{
    // UTC001: Updating requirement references replaces the existing links.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_WithValidReference_ReplacesLinks()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var document = SeedRequirementDocument();
        Db.IssueRequirementLinks.Add(new IssueRequirementLink
        {
            IssueId = issue.Id,
            DocumentId = document.Id,
            ReqUid = "OLD-001",
            ReqHash = "old",
            LinkedById = 1
        });
        await Db.SaveChangesAsync();

        var result = await KanbanService.UpdateIssueRequirementsAsync(issue.Id, new UpdateIssueRequirementsRequest
        {
            Refs = new List<RequirementRefInputDto> { new() { DocumentId = document.Id, ReqUid = "REQ-001" } }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var link = Assert.Single(Db.IssueRequirementLinks);
        Assert.Equal("REQ-001", link.ReqUid);
        Assert.False((await Db.Issues.FindAsync(issue.Id))!.ReqChanged);
    }

    // UTC002: Updating requirements on a missing issue returns not found.
    [Fact]
    public async Task UpdateIssueRequirementsAsync_WhenIssueIsMissing_ReturnsNotFound()
    {
        var result = await KanbanService.UpdateIssueRequirementsAsync(9999, new UpdateIssueRequirementsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }
}
