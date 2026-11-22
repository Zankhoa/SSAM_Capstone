using System.Text;
using Microsoft.AspNetCore.Http;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class AttachmentMethodTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Uploading a valid file stores metadata and creates a physical file.
    [Fact]
    public async Task UploadIssueAttachmentAsync_WithValidFile_ReturnsAttachment()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var content = new MemoryStream(Encoding.UTF8.GetBytes("file content"));
        var file = new FormFile(content, 0, content.Length, "file", "notes.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var result = await KanbanService.UploadIssueAttachmentAsync(issue.Id, file, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        var attachment = Assert.Single(Db.IssueAttachments);
        Assert.Equal("notes.txt", attachment.FileName);
        Assert.True(File.Exists(Path.Combine(UploadRoot, "issues", issue.Id.ToString(), attachment.StoredFileName)));
    }

    // UTC002: Deleting an existing attachment removes metadata and the physical file.
    [Fact]
    public async Task DeleteIssueAttachmentAsync_WithExistingAttachment_ReturnsNoContent()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var directory = Path.Combine(UploadRoot, "issues", issue.Id.ToString());
        Directory.CreateDirectory(directory);
        const string storedName = "stored.txt";
        var path = Path.Combine(directory, storedName);
        await File.WriteAllTextAsync(path, "content");
        var attachment = new IssueAttachment
        {
            Id = 700,
            IssueId = issue.Id,
            FileName = "original.txt",
            StoredFileName = storedName,
            ContentType = "text/plain",
            FileSize = 7,
            UploadedById = 1
        };
        Db.IssueAttachments.Add(attachment);
        await Db.SaveChangesAsync();

        var result = await KanbanService.DeleteIssueAttachmentAsync(issue.Id, attachment.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.Empty(Db.IssueAttachments);
        Assert.False(File.Exists(path));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC003: An empty file is rejected.
    [Fact]
    public async Task UploadIssueAttachmentAsync_WhenFileIsEmpty_ReturnsBadRequest()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var file = new FormFile(new MemoryStream(), 0, 0, "file", "empty.txt");

        var result = await KanbanService.UploadIssueAttachmentAsync(issue.Id, file, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FILE", result.ErrorCode);
    }

    // UTC004: Deleting a missing attachment returns not found.
    [Fact]
    public async Task DeleteIssueAttachmentAsync_WhenAttachmentIsMissing_ReturnsNotFound()
    {
        SeedWorkflow();

        var result = await KanbanService.DeleteIssueAttachmentAsync(100, 9999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ATTACHMENT_NOT_FOUND", result.ErrorCode);
    }
}
