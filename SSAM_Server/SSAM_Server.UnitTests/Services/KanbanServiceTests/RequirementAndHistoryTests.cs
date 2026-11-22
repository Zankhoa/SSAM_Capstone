using Microsoft.AspNetCore.Http;
using SSAM_Server.Common;
using SSAM_Server.DTOs.Kanban;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class RequirementAndHistoryTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Acknowledging a changed requirement refreshes its hash and clears the changed flag.
    [Fact]
    public async Task AcknowledgeIssueRequirementsAsync_WithChangedLink_RefreshesLink()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var document = SeedRequirementDocument();
        Db.IssueRequirementLinks.Add(new IssueRequirementLink
        {
            IssueId = issue.Id,
            DocumentId = document.Id,
            ReqUid = "REQ-001",
            ReqHash = "old-hash",
            ChangedVersionLabel = "2.0",
            LinkedById = 1
        });
        issue.ReqChanged = true;
        await Db.SaveChangesAsync();

        var result = await KanbanService.AcknowledgeIssueRequirementsAsync(issue.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var link = Assert.Single(Db.IssueRequirementLinks);
        Assert.Equal("hash-1", link.ReqHash);
        Assert.Null(link.ChangedVersionLabel);
        Assert.False((await Db.Issues.FindAsync(issue.Id))!.ReqChanged);
        Assert.Contains(Db.IssueActivities, a => a.Action == IssueActivityActions.ReqAcked);
    }

    // UTC002: Comments are returned in chronological order with replies nested one level.
    [Fact]
    public async Task GetIssueCommentsAsync_WithReplies_ReturnsNestedComments()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var parent = new IssueComment { Id = 600, IssueId = issue.Id, Body = "Parent", AuthorId = 1, CreatedAt = Clock.GetUtcNow().UtcDateTime.AddMinutes(-2) };
        var reply = new IssueComment { Id = 601, IssueId = issue.Id, ParentId = parent.Id, Body = "Reply", AuthorId = 1, CreatedAt = Clock.GetUtcNow().UtcDateTime.AddMinutes(-1) };
        Db.IssueComments.AddRange(parent, reply);
        await Db.SaveChangesAsync();

        var result = await KanbanService.GetIssueCommentsAsync(issue.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Items.Count);
        var comment = Assert.Single(result.Data.Items.Where(c => c.CommentId == parent.Id));
        Assert.Equal("Parent", comment.Body);
        Assert.Equal("Reply", Assert.Single(comment.Replies).Body);
    }

    // UTC003: Issue activities are returned newest first.
    [Fact]
    public async Task GetIssueActivitiesAsync_WithActivities_ReturnsNewestFirst()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        Db.IssueActivities.AddRange(
            new IssueActivity { Id = 700, IssueId = issue.Id, Action = "OLD", ActorId = 1, CreatedAt = Clock.GetUtcNow().UtcDateTime.AddMinutes(-2) },
            new IssueActivity { Id = 701, IssueId = issue.Id, Action = "NEW", ActorId = 1, CreatedAt = Clock.GetUtcNow().UtcDateTime.AddMinutes(-1) });
        await Db.SaveChangesAsync();

        var result = await KanbanService.GetIssueActivitiesAsync(issue.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "NEW", "OLD" }, result.Data!.Items.Select(a => a.Action));
    }

    // UTC004: Sprints are filtered by status and active sprints are ordered first.
    [Fact]
    public async Task GetProjectSprintsAsync_WithStatusFilter_ReturnsMatchingSprints()
    {
        SeedProject();
        SeedSprint(400, SprintStatuses.Active);
        SeedSprint(401, SprintStatuses.Planned);

        var result = await KanbanService.GetProjectSprintsAsync(100, "active", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var sprint = Assert.Single(result.Data!.Items);
        Assert.Equal(400, sprint.SprintId);
        Assert.Equal(SprintStatuses.Active, sprint.Status);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC005: A missing issue returns not found for requirement acknowledgement.
    [Fact]
    public async Task AcknowledgeIssueRequirementsAsync_WhenIssueIsMissing_ReturnsNotFound()
    {
        var result = await KanbanService.AcknowledgeIssueRequirementsAsync(9999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ISSUE_NOT_FOUND", result.ErrorCode);
    }

    // UTC006: A missing issue returns not found for comment history.
    [Fact]
    public async Task GetIssueCommentsAsync_WhenIssueIsMissing_ReturnsNotFound()
    {
        var result = await KanbanService.GetIssueCommentsAsync(9999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    // UTC007: A missing issue returns not found for activity history.
    [Fact]
    public async Task GetIssueActivitiesAsync_WhenIssueIsMissing_ReturnsNotFound()
    {
        var result = await KanbanService.GetIssueActivitiesAsync(9999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }
}
