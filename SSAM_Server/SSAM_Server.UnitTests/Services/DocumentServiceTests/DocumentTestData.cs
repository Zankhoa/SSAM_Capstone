using System.Text.Json;
using Moq;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

/// <summary>Shared seeding / mocking helpers for the DocumentService test classes.</summary>
internal static class DocumentTestData
{
    public static void GuardAllow(Mock<IProjectAccessGuard> guard, long projectId, string permission)
    {
        guard
            .Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());
    }

    public static void GuardDeny(Mock<IProjectAccessGuard> guard, long projectId, string permission)
    {
        guard
            .Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.ForbiddenPermission(permission));
    }

    public static void GuardUnauthenticated(Mock<IProjectAccessGuard> guard, long projectId, string permission)
    {
        guard
            .Setup(g => g.EnsureProjectPermissionAsync(projectId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Unauthorized("Your session is invalid or has expired."));
    }

    public static User AddUser(AppDbContext db, long id, string? fullName = null)
    {
        var user = new User
        {
            Id = id,
            AccountId = id,
            FullName = fullName ?? $"User {id}",
            Email = $"user{id}@ssam.vn",
            AvatarUrl = $"https://cdn.ssam.vn/avatar{id}.png",
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static ProjectMember AddProjectMember(AppDbContext db, long projectId, long userId)
    {
        var member = new ProjectMember { ProjectId = projectId, UserId = userId, RoleId = 1, IsActive = true };
        db.ProjectMembers.Add(member);
        db.SaveChanges();
        return member;
    }

    public static DocumentNode AddNode(
        AppDbContext db,
        DateTime now,
        long id,
        long projectId = 10,
        string kind = DocumentNodeKinds.Page,
        string title = "Node",
        string? docType = DocumentTypes.Srs,
        string? code = null,
        long? parentId = null,
        int sortOrder = 0,
        bool isDeleted = false,
        string requiredApprovers = "[]")
    {
        var node = new DocumentNode
        {
            Id = id,
            ProjectId = projectId,
            Kind = kind,
            Title = title,
            DocType = kind == DocumentNodeKinds.Folder ? null : docType,
            Code = code,
            ParentId = parentId,
            SortOrder = sortOrder,
            IsDeleted = isDeleted,
            RequiredApprovers = requiredApprovers,
            CreatedById = 1,
            CreatedAt = now,
            UpdatedAt = now.AddDays(-1)
        };
        db.DocumentNodes.Add(node);
        db.SaveChanges();
        return node;
    }

    /// <summary>Adds a PAGE node (documentId) with one version (versionId) and makes it current. Seeds users 1 and 2 when missing.</summary>
    public static (DocumentNode Node, DocumentVersion Version) AddPageWithVersion(
        AppDbContext db,
        DateTime now,
        long documentId = 100,
        long versionId = 1000,
        long projectId = 10,
        string status = DocumentVersionStatuses.Draft,
        string label = "1.0",
        string content = "{\"type\":\"doc\",\"content\":[]}",
        string contentText = "",
        int rowVersion = 1,
        string title = "SRS Page",
        string? code = "SRS-01",
        string requiredApprovers = "[]",
        long authorId = 1,
        long? submittedById = null)
    {
        if (!db.Users.Any(u => u.Id == 1)) AddUser(db, 1, "Author One");
        if (!db.Users.Any(u => u.Id == 2)) AddUser(db, 2, "Reviewer Two");

        var node = AddNode(db, now, documentId, projectId, DocumentNodeKinds.Page, title, DocumentTypes.Srs, code, requiredApprovers: requiredApprovers);
        var version = new DocumentVersion
        {
            Id = versionId,
            DocumentId = documentId,
            Label = label,
            Status = status,
            Content = content,
            ContentText = contentText,
            RowVersion = rowVersion,
            AuthorId = authorId,
            UpdatedById = authorId,
            SubmittedById = submittedById,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-1)
        };
        db.DocumentVersions.Add(version);
        db.SaveChanges();

        node.CurrentVersionId = versionId;
        db.SaveChanges();
        return (node, version);
    }

    public static RequirementItem AddRequirement(
        AppDbContext db,
        long documentId,
        long versionId,
        string reqUid,
        string title = "Requirement",
        string contentHash = "hash",
        int sortOrder = 0)
    {
        var item = new RequirementItem
        {
            DocumentId = documentId,
            VersionId = versionId,
            ReqUid = reqUid,
            AnchorId = $"a-{reqUid}",
            Title = title,
            BodyPreview = "preview",
            BodyLength = 7,
            ContentHash = contentHash,
            SortOrder = sortOrder
        };
        db.RequirementItems.Add(item);
        db.SaveChanges();
        return item;
    }

    /// <summary>Builds a ProseMirror-like doc with one REQ heading per (uid,title) pair, each followed by a paragraph.</summary>
    public static JsonElement DocWithReqs(params (string Uid, string Title)[] reqs)
    {
        var blocks = new List<string>();
        foreach (var (uid, title) in reqs)
        {
            blocks.Add(
                "{\"type\":\"heading\",\"attrs\":{\"level\":2,\"req\":true,\"reqUid\":\"" + uid + "\",\"id\":\"anchor-" + uid + "\"}," +
                "\"content\":[{\"type\":\"text\",\"text\":\"" + uid + " — " + title + "\"}]}");
            blocks.Add("{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Body of " + uid + "\"}]}");
        }
        var json = "{\"type\":\"doc\",\"content\":[" + string.Join(",", blocks) + "]}";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    public static string DocJsonWithReqs(params (string Uid, string Title)[] reqs) =>
        DocWithReqs(reqs).GetRawText();
}
