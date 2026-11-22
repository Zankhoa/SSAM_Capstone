using System.Text;
using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.KanbanServiceTests;

public class AttachmentDownloadTests : KanbanServiceTestBase
{
    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: An existing attachment can be downloaded with its metadata.
    [Fact]
    public async Task DownloadIssueAttachmentAsync_WithExistingFile_ReturnsStreamAndMetadata()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var directory = Path.Combine(UploadRoot, "issues", issue.Id.ToString());
        Directory.CreateDirectory(directory);
        const string storedName = "stored.txt";
        await File.WriteAllTextAsync(Path.Combine(directory, storedName), "download content");
        Db.IssueAttachments.Add(new IssueAttachment
        {
            Id = 700,
            IssueId = issue.Id,
            FileName = "original.txt",
            StoredFileName = storedName,
            ContentType = "text/plain",
            FileSize = Encoding.UTF8.GetByteCount("download content"),
            UploadedById = 1
        });
        await Db.SaveChangesAsync();

        var result = await KanbanService.DownloadIssueAttachmentAsync(issue.Id, 700, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("text/plain", result.Data!.ContentType);
        Assert.Equal("original.txt", result.Data.FileName);
        using var reader = new StreamReader(result.Data.Stream);
        Assert.Equal("download content", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC002: A missing attachment returns not found.
    [Fact]
    public async Task DownloadIssueAttachmentAsync_WhenAttachmentIsMissing_ReturnsNotFound()
    {
        SeedWorkflow();

        var result = await KanbanService.DownloadIssueAttachmentAsync(100, 9999, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ATTACHMENT_NOT_FOUND", result.ErrorCode);
    }

    // UTC003: Missing storage content returns file-not-found.
    [Fact]
    public async Task DownloadIssueAttachmentAsync_WhenStorageFileIsMissing_ReturnsNotFound()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        Db.IssueAttachments.Add(new IssueAttachment
        {
            Id = 700,
            IssueId = issue.Id,
            FileName = "original.txt",
            StoredFileName = "missing.txt",
            ContentType = "text/plain",
            FileSize = 1,
            UploadedById = 1
        });
        await Db.SaveChangesAsync();

        var result = await KanbanService.DownloadIssueAttachmentAsync(issue.Id, 700, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("FILE_NOT_FOUND", result.ErrorCode);
    }

    // UTC004: A denied view permission blocks attachment download.
    [Fact]
    public async Task DownloadIssueAttachmentAsync_WhenPermissionIsDenied_ReturnsForbidden()
    {
        SeedWorkflow();
        var issue = SeedIssue();
        var directory = Path.Combine(UploadRoot, "issues", issue.Id.ToString());
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "stored.txt"), "content");
        Db.IssueAttachments.Add(new IssueAttachment
        {
            Id = 700,
            IssueId = issue.Id,
            FileName = "original.txt",
            StoredFileName = "stored.txt",
            ContentType = "text/plain",
            FileSize = 7,
            UploadedById = 1
        });
        await Db.SaveChangesAsync();
        AccessGuardMock.Setup(g => g.EnsureProjectPermissionAsync(100, PermissionKeys.IssueView, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Forbidden("FORBIDDEN", "Permission denied."));

        var result = await KanbanService.DownloadIssueAttachmentAsync(issue.Id, 700, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }
}
