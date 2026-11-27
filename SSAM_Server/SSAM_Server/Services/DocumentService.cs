using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Documents;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;
    private readonly TimeProvider _clock;
    private readonly IAiService _aiService;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IRequirementSyncService _requirementSync;
    private readonly IFigmaWatchService _figmaWatchService;
    private readonly INotificationService _notifications;

    public DocumentService(
        IDocumentRepository db,
        ICurrentUser currentUser,
        IPermissionService permissions,
        TimeProvider clock,
        IAiService aiService,
        IProjectAccessGuard accessGuard,
        IRequirementSyncService requirementSync,
        IFigmaWatchService figmaWatchService,
        INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _permissions = permissions;
        _clock = clock;
        _aiService = aiService;
        _accessGuard = accessGuard;
        _requirementSync = requirementSync;
        _figmaWatchService = figmaWatchService;
        _notifications = notifications;
    }

    public async Task<ServiceResult<ItemsDto<DocumentNodeDto>>> GetProjectDocumentsAsync(
        long projectId,
        string? search,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.DocumentView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<DocumentNodeDto>>();

        var nodes = await _db.DocumentNodes
            .AsNoTracking()
            .Include(d => d.CurrentVersion)
            .Where(d => d.ProjectId == projectId && !d.IsDeleted)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);

        var currentVersionIds = nodes
            .Where(n => n.CurrentVersionId.HasValue)
            .Select(n => n.CurrentVersionId!.Value)
            .Distinct()
            .ToList();

        var reqCounts = currentVersionIds.Count == 0
            ? new Dictionary<long, int>()
            : await _db.RequirementItems
                .AsNoTracking()
                .Where(r => currentVersionIds.Contains(r.VersionId))
                .GroupBy(r => r.VersionId)
                .Select(g => new { VersionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VersionId, x => x.Count, ct);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            var matchingReqDocIds = await _db.RequirementItems
                .AsNoTracking()
                .Where(r => currentVersionIds.Contains(r.VersionId) &&
                            (r.ReqUid.Contains(q) || r.Title.Contains(q) || r.BodyPreview.Contains(q)))
                .Select(r => r.DocumentId)
                .Distinct()
                .ToListAsync(ct);

            var matchedIds = new HashSet<long>(matchingReqDocIds);
            foreach (var n in nodes)
            {
                if (n.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (n.Code != null && n.Code.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (n.CurrentVersion != null && n.CurrentVersion.ContentText.Contains(q, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedIds.Add(n.Id);
                }
            }

            // Include ancestor folders so tree stays connected
            var byId = nodes.ToDictionary(n => n.Id);
            var keepIds = new HashSet<long>(matchedIds);
            foreach (var id in matchedIds)
            {
                var curr = byId.GetValueOrDefault(id);
                while (curr?.ParentId != null && byId.TryGetValue(curr.ParentId.Value, out var parent))
                {
                    keepIds.Add(parent.Id);
                    curr = parent;
                }
            }

            nodes = nodes.Where(n => keepIds.Contains(n.Id)).ToList();
        }

        var approversByNode = await ResolveApproversMapAsync(nodes, ct);
        var dtos = nodes.Select(n => MapNodeDto(
            n,
            n.CurrentVersionId.HasValue ? reqCounts.GetValueOrDefault(n.CurrentVersionId.Value) : 0,
            approversByNode.GetValueOrDefault(n.Id))).ToList();
        return ServiceResult<ItemsDto<DocumentNodeDto>>.Success(new ItemsDto<DocumentNodeDto>(dtos));
    }

    public async Task<ServiceResult<object>> CreateDocumentNodeAsync(
        long projectId,
        CreateDocumentNodeRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.DocumentCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<object>();

        var errors = new Dictionary<string, string[]>();
        var kind = request.Kind?.Trim().ToUpperInvariant() ?? string.Empty;
        var title = request.Title?.Trim() ?? string.Empty;

        if (!DocumentNodeKinds.IsValid(kind))
            errors["kind"] = new[] { "Kind must be FOLDER or PAGE." };
        if (string.IsNullOrWhiteSpace(title) || title.Length > 150)
            errors["title"] = new[] { "Title is required and must be at most 150 characters." };

        string? docType = request.DocType?.Trim().ToUpperInvariant();
        if (kind == DocumentNodeKinds.Page)
        {
            if (string.IsNullOrWhiteSpace(docType) || !DocumentTypes.IsValid(docType))
                errors["docType"] = new[] { "Valid docType (BRD, SRS, ADR, UI_SPEC, TEST_PLAN, OTHER) is required for PAGE." };
        }
        else if (kind == DocumentNodeKinds.Folder)
        {
            docType = null;
        }

        if (request.Subtitle != null && request.Subtitle.Length > 300)
            errors["subtitle"] = new[] { "Subtitle must be at most 300 characters." };

        if (errors.Count > 0)
            return ServiceResult<object>.ValidationFailure(errors);

        if (request.ParentId.HasValue)
        {
            var parent = await _db.DocumentNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.ParentId.Value && d.ProjectId == projectId && !d.IsDeleted, ct);
            if (parent == null || (kind == DocumentNodeKinds.Folder && parent.Kind != DocumentNodeKinds.Folder))
                return ServiceResult<object>.BadRequest("INVALID_PARENT", "Parent must be an existing folder (or page for child pages) in the same project.");
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var maxSort = await _db.DocumentNodes
            .Where(d => d.ProjectId == projectId && d.ParentId == request.ParentId && !d.IsDeleted)
            .Select(d => (int?)d.SortOrder)
            .MaxAsync(ct) ?? -1;

        if (kind == DocumentNodeKinds.Folder)
        {
            var folder = new DocumentNode
            {
                ProjectId = projectId,
                ParentId = request.ParentId,
                Kind = DocumentNodeKinds.Folder,
                Title = title,
                DocType = null,
                Code = null,
                SortOrder = maxSort + 1,
                CreatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Add(folder);
            await _db.SaveChangesAsync(ct);

            return ServiceResult<object>.Created(MapNodeDto(folder, 0));
        }

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var code = await _db.AllocateDocumentCodeAsync(projectId, docType!, request.ParentId, innerCt);
            string normalizedJson;
            string plainText;
            List<DocumentContentHelper.ExtractedRequirementNode> extractedReqs;

            if (request.Content.HasValue && request.Content.Value.ValueKind == JsonValueKind.Object)
            {
                (normalizedJson, plainText, extractedReqs) = DocumentContentHelper.ProcessContent(request.Content.Value);
            }
            else
            {
                string? sampleReqUid = null;
                if (docType is DocumentTypes.Urd or DocumentTypes.Brd or DocumentTypes.Srs)
                {
                    sampleReqUid = await _db.AllocateReqUidAsync(projectId, innerCt);
                }

                var templateJson = DocumentContentHelper.BuildInitialTemplateContentJson(docType!, title, sampleReqUid);
                using var jsonDoc = JsonDocument.Parse(templateJson);
                (normalizedJson, plainText, extractedReqs) = DocumentContentHelper.ProcessContent(jsonDoc.RootElement);
            }

            var node = new DocumentNode
            {
                ProjectId = projectId,
                ParentId = request.ParentId,
                Kind = DocumentNodeKinds.Page,
                Title = title,
                DocType = docType,
                Code = code,
                SortOrder = maxSort + 1,
                CreatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Add(node);
            await _db.SaveChangesAsync(innerCt);

            var version = new DocumentVersion
            {
                DocumentId = node.Id,
                Label = "1.0",
                Status = DocumentVersionStatuses.Draft,
                Subtitle = request.Subtitle?.Trim(),
                Content = normalizedJson,
                ContentText = plainText,
                RowVersion = 1,
                AuthorId = userId,
                UpdatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Add(version);
            await _db.SaveChangesAsync(innerCt);

            node.CurrentVersionId = version.Id;

            foreach (var r in extractedReqs)
            {
                _db.Add(new RequirementItem
                {
                    DocumentId = node.Id,
                    VersionId = version.Id,
                    ReqUid = r.ReqUid,
                    AnchorId = r.AnchorId,
                    Title = r.Title,
                    BodyPreview = r.BodyPreview,
                    BodyLength = r.BodyLength,
                    Actor = r.Actor,
                    Preconditions = r.Preconditions,
                    Constraints = JsonSerializer.Serialize(r.Constraints),
                    AcceptanceCriteria = JsonSerializer.Serialize(r.AcceptanceCriteria),
                    HasTable = r.HasTable,
                    HasDiagram = r.HasDiagram,
                    HasFigma = r.HasFigma,
                    ContentHash = r.ContentHash,
                    SortOrder = r.SortOrder
                });
            }

            _db.Add(new DocumentActivity
            {
                DocumentId = node.Id,
                VersionId = version.Id,
                Action = DocumentActivityActions.Created,
                ActorId = userId,
                Note = $"Khởi tạo tài liệu {code} v1.0",
                CreatedAt = now
            });

            await _db.SaveChangesAsync(innerCt);

            node.CurrentVersion = version;
            return ServiceResult<object>.Created(MapNodeDto(node, extractedReqs.Count));
        }, ct);
    }

    public async Task<ServiceResult<DocumentNodeDto>> UpdateDocumentNodeAsync(
        long documentId,
        UpdateDocumentNodeRequest request,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .Include(d => d.CurrentVersion)
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentNodeDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentNodeDto>();

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        if (request.Title != null)
        {
            var newTitle = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(newTitle) || newTitle.Length > 150)
            {
                return ServiceResult<DocumentNodeDto>.ValidationFailure(new Dictionary<string, string[]>
                {
                    ["title"] = new[] { "Title must be between 1 and 150 characters." }
                });
            }

            if (!string.Equals(node.Title, newTitle, StringComparison.Ordinal))
            {
                var oldTitle = node.Title;
                node.Title = newTitle;
                if (node.Kind == DocumentNodeKinds.Page)
                {
                    _db.Add(new DocumentActivity
                    {
                        DocumentId = node.Id,
                        VersionId = node.CurrentVersionId,
                        Action = DocumentActivityActions.Renamed,
                        ActorId = userId,
                        Note = $"Đổi tên từ \"{oldTitle}\" sang \"{newTitle}\"",
                        CreatedAt = now
                    });
                }
            }
        }

        if (request.ParentIdSpecified && request.ParentId != node.ParentId)
        {
            if (request.ParentId.HasValue)
            {
                if (request.ParentId.Value == node.Id)
                    return ServiceResult<DocumentNodeDto>.BadRequest("INVALID_PARENT", "Cannot move a node into itself.");

                var allProjectNodes = await _db.DocumentNodes
                    .AsNoTracking()
                    .Where(d => d.ProjectId == node.ProjectId && !d.IsDeleted)
                    .ToListAsync(ct);

                var byId = allProjectNodes.ToDictionary(d => d.Id);
                if (!byId.TryGetValue(request.ParentId.Value, out var targetParent) ||
                    (node.Kind == DocumentNodeKinds.Folder && targetParent.Kind != DocumentNodeKinds.Folder))
                    return ServiceResult<DocumentNodeDto>.BadRequest("INVALID_PARENT", "Target parent must be an existing folder (or page for child pages) in the same project.");

                // Check cycle
                var cursor = targetParent;
                while (cursor != null)
                {
                    if (cursor.Id == node.Id)
                        return ServiceResult<DocumentNodeDto>.BadRequest("INVALID_PARENT", "Cannot move a folder into one of its descendants.");
                    cursor = cursor.ParentId.HasValue ? byId.GetValueOrDefault(cursor.ParentId.Value) : null;
                }
            }

            node.ParentId = request.ParentId;
            if (node.Kind == DocumentNodeKinds.Page)
            {
                _db.Add(new DocumentActivity
                {
                    DocumentId = node.Id,
                    VersionId = node.CurrentVersionId,
                    Action = DocumentActivityActions.Moved,
                    ActorId = userId,
                    Note = "Di chuyển vị trí tài liệu trong cây thư mục",
                    CreatedAt = now
                });
            }
        }

        if (request.SortOrder.HasValue)
        {
            node.SortOrder = request.SortOrder.Value;
        }

        node.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var reqCount = node.CurrentVersionId.HasValue
            ? await _db.RequirementItems.CountAsync(r => r.VersionId == node.CurrentVersionId.Value, ct)
            : 0;

        var approversByNode = await ResolveApproversMapAsync(new[] { node }, ct);
        return ServiceResult<DocumentNodeDto>.Success(MapNodeDto(node, reqCount, approversByNode.GetValueOrDefault(node.Id)));
    }

    public async Task<ServiceResult> DeleteDocumentNodeAsync(long documentId, CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentDelete, ct);
        if (!auth.IsSuccess) return auth;

        var allNodes = await _db.DocumentNodes
            .Where(d => d.ProjectId == node.ProjectId && !d.IsDeleted)
            .ToListAsync(ct);

        var subtreeIds = CollectSubtreeIds(node.Id, allNodes);
        var subtreeNodes = allNodes.Where(n => subtreeIds.Contains(n.Id)).ToList();

        if (subtreeNodes.Any(n => n.BaselineVersionId != null))
        {
            return ServiceResult.Conflict(
                "BASELINED_DOCUMENT_CANNOT_DELETE",
                "Không thể xoá tài liệu đã có phiên bản được phê duyệt (baseline).");
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var n in subtreeNodes)
        {
            n.IsDeleted = true;
            n.UpdatedAt = now;
            if (n.Kind == DocumentNodeKinds.Page)
            {
                _db.Add(new DocumentActivity
                {
                    DocumentId = n.Id,
                    VersionId = n.CurrentVersionId,
                    Action = DocumentActivityActions.Deleted,
                    ActorId = userId,
                    Note = "Xoá tài liệu",
                    CreatedAt = now
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult<DocumentDetailDto>> GetDocumentDetailAsync(
        long documentId,
        long? versionId,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .AsNoTracking()
            .Include(d => d.CurrentVersion)
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentDetailDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentView, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentDetailDto>();

        if (node.Kind != DocumentNodeKinds.Page)
            return ServiceResult<DocumentDetailDto>.BadRequest("NOT_A_PAGE", "Folder nodes do not have version details.");

        var versions = await _db.DocumentVersions
            .AsNoTracking()
            .Include(v => v.UpdatedBy)
            .Include(v => v.Author)
            .Include(v => v.SubmittedBy)
            .Include(v => v.ReviewedBy)
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.Id)
            .ToListAsync(ct);

        var targetVersionId = versionId ?? node.CurrentVersionId;
        var currentVer = versions.FirstOrDefault(v => v.Id == targetVersionId);
        if (currentVer == null)
            return ServiceResult<DocumentDetailDto>.NotFound("VERSION_NOT_FOUND", "Document version not found.");

        var reqCount = await _db.RequirementItems
            .AsNoTracking()
            .CountAsync(r => r.VersionId == currentVer.Id, ct);

        var userId = _currentUser.UserId;
        var userPerms = await _permissions.GetProjectPermissionsAsync(userId, node.ProjectId, ct);
        var permSet = new HashSet<string>(userPerms, StringComparer.OrdinalIgnoreCase);

        var requiredApproverIds = ParseRequiredApprovers(node.RequiredApprovers);
        var headStatus = node.CurrentVersion?.Status ?? currentVer.Status;
        var can = new DocumentPermissionsDto
        {
            Edit = permSet.Contains(PermissionKeys.DocumentUpdate) &&
                   currentVer.Status is DocumentVersionStatuses.Draft or DocumentVersionStatuses.ChangesRequested,
            Approve = permSet.Contains(PermissionKeys.DocumentApprove) &&
                      currentVer.Status == DocumentVersionStatuses.InReview,
            Revise = permSet.Contains(PermissionKeys.DocumentCreate) &&
                     headStatus == DocumentVersionStatuses.Approved,
            Delete = permSet.Contains(PermissionKeys.DocumentDelete) &&
                     node.BaselineVersionId == null
        };

        var approversByNode = await ResolveApproversMapAsync(new[] { node }, ct);
        var detail = new DocumentDetailDto
        {
            Node = MapNodeDto(node, reqCount, approversByNode.GetValueOrDefault(node.Id)),
            Versions = versions.Select(MapVersionSummaryDto).ToList(),
            Current = MapVersionDto(currentVer),
            Can = can
        };

        return ServiceResult<DocumentDetailDto>.Success(detail);
    }

    public async Task<ServiceResult<SaveContentResultDto>> SaveVersionContentAsync(
        long documentId,
        long versionId,
        SaveDocumentContentRequest request,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<SaveContentResultDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<SaveContentResultDto>();

        var version = await _db.DocumentVersions
            .Include(v => v.UpdatedBy)
            .Include(v => v.Author)
            .Include(v => v.SubmittedBy)
            .Include(v => v.ReviewedBy)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.DocumentId == documentId, ct);
        if (version == null)
            return ServiceResult<SaveContentResultDto>.NotFound("VERSION_NOT_FOUND", "Document version not found.");

        if (version.Status is not (DocumentVersionStatuses.Draft or DocumentVersionStatuses.ChangesRequested))
        {
            return ServiceResult<SaveContentResultDto>.Conflict(
                "VERSION_LOCKED",
                "Phiên bản hiện tại đã khoá, không thể chỉnh sửa nội dung.");
        }

        if (request.RowVersion != version.RowVersion)
        {
            return ServiceResult<SaveContentResultDto>.VersionConflict(
                "Nội dung tài liệu đã được người khác cập nhật.",
                new VersionConflictCurrentDto
                {
                    RowVersion = version.RowVersion,
                    UpdatedAt = FormatUtc(version.UpdatedAt),
                    UpdatedBy = new UserLiteDto
                    {
                        UserId = version.UpdatedBy.Id,
                        FullName = version.UpdatedBy.FullName
                    }
                });
        }

        if (request.SubtitleSpecified && request.Subtitle != null && request.Subtitle.Length > 300)
        {
            return ServiceResult<SaveContentResultDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["subtitle"] = new[] { "Subtitle must be at most 300 characters." }
            });
        }

        var (normalizedJson, plainText, extractedReqs) = DocumentContentHelper.ProcessContent(request.Content);
        await _figmaWatchService.RegisterFilesFromContentAsync(normalizedJson, ct);

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var oldReqs = await _db.RequirementItems
                .Where(r => r.VersionId == versionId)
                .OrderBy(r => r.SortOrder)
                .ToListAsync(innerCt);

            _db.RemoveRange(oldReqs);

            var newReqEntities = extractedReqs.Select(r => new RequirementItem
            {
                DocumentId = documentId,
                VersionId = versionId,
                ReqUid = r.ReqUid,
                AnchorId = r.AnchorId,
                Title = r.Title,
                BodyPreview = r.BodyPreview,
                BodyLength = r.BodyLength,
                AcceptanceCriteria = JsonSerializer.Serialize(r.AcceptanceCriteria),
                HasTable = r.HasTable,
                HasDiagram = r.HasDiagram,
                HasFigma = r.HasFigma,
                ContentHash = r.ContentHash,
                SortOrder = r.SortOrder
            }).ToList();

            _db.AddRange(newReqEntities);

            version.Content = normalizedJson;
            version.ContentText = plainText;
            if (request.SubtitleSpecified)
            {
                version.Subtitle = request.Subtitle?.Trim();
            }
            version.RowVersion += 1;
            version.UpdatedById = userId;
            version.UpdatedAt = now;
            node.UpdatedAt = now;
            _db.Update(version);
            _db.Update(node);

            var summary = DocumentContentHelper.SummarizeReqChanges(oldReqs, newReqEntities);
            if (summary.Added.Count > 0 || summary.Modified.Count > 0 || summary.Removed.Count > 0)
            {
                _db.Add(new DocumentActivity
                {
                    DocumentId = documentId,
                    VersionId = versionId,
                    Action = DocumentActivityActions.ContentSaved,
                    ActorId = userId,
                    Note = DocumentContentHelper.FormatReqSummary(summary),
                    CreatedAt = now
                });
            }

            await _db.SaveChangesAsync(innerCt);

            // Reload UpdatedBy if needed
            var updatedVer = await _db.DocumentVersions
                .AsNoTracking()
                .Include(v => v.UpdatedBy)
                .Include(v => v.Author)
                .Include(v => v.SubmittedBy)
                .Include(v => v.ReviewedBy)
                .FirstAsync(v => v.Id == versionId, innerCt);

            return ServiceResult<SaveContentResultDto>.Success(new SaveContentResultDto
            {
                Version = MapVersionDto(updatedVer),
                Requirements = newReqEntities.Select(MapRequirementDto).ToList()
            });
        }, ct);
    }

    public async Task<ServiceResult<DocumentNodeDto>> SetDocumentApproversAsync(
        long documentId,
        UpdateDocumentApproversRequest request,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .Include(d => d.CurrentVersion)
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentNodeDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentNodeDto>();

        if (node.Kind != DocumentNodeKinds.Page)
        {
            return ServiceResult<DocumentNodeDto>.BadRequest(
                "INVALID_DOCUMENT_KIND",
                "Chỉ có thể cấu hình người duyệt cho tài liệu dạng PAGE.");
        }

        var rawIds = request.ApproverIds ?? request.UserIds ?? new List<long>();
        var distinctIds = rawIds.Where(id => id > 0).Distinct().ToList();

        if (distinctIds.Count > 0)
        {
            var validMemberIds = await _db.ProjectMembers
                .AsNoTracking()
                .Where(pm => pm.ProjectId == node.ProjectId && distinctIds.Contains(pm.UserId))
                .Select(pm => pm.UserId)
                .ToListAsync(ct);

            var invalidIds = distinctIds.Except(validMemberIds).ToList();
            if (invalidIds.Count > 0)
            {
                return ServiceResult<DocumentNodeDto>.ValidationFailure(new Dictionary<string, string[]>
                {
                    ["approverIds"] = new[] { $"Người dùng không thuộc dự án: {string.Join(", ", invalidIds)}" }
                });
            }
        }

        node.RequiredApprovers = JsonSerializer.Serialize(distinctIds);
        node.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        var reqCount = node.CurrentVersionId.HasValue
            ? await _db.RequirementItems.CountAsync(r => r.VersionId == node.CurrentVersionId.Value, ct)
            : 0;
        var approversByNode = await ResolveApproversMapAsync(new[] { node }, ct);
        return ServiceResult<DocumentNodeDto>.Success(MapNodeDto(node, reqCount, approversByNode.GetValueOrDefault(node.Id)));
    }

    public async Task<ServiceResult<DocumentDetailDto>> SubmitVersionAsync(
        long documentId,
        long versionId,
        SubmitDocumentRequest? request = null,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentDetailDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentDetailDto>();

        var version = await _db.DocumentVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.DocumentId == documentId, ct);
        if (version == null)
            return ServiceResult<DocumentDetailDto>.NotFound("VERSION_NOT_FOUND", "Document version not found.");

        if (version.Status is not (DocumentVersionStatuses.Draft or DocumentVersionStatuses.ChangesRequested))
        {
            return ServiceResult<DocumentDetailDto>.Conflict(
                "INVALID_VERSION_STATE",
                "Chỉ có thể gửi duyệt phiên bản ở trạng thái DRAFT hoặc CHANGES_REQUESTED.");
        }

        var reqUids = await _db.RequirementItems
            .AsNoTracking()
            .Where(r => r.VersionId == versionId)
            .Select(r => r.ReqUid)
            .ToListAsync(ct);

        if (reqUids.Count == 0)
        {
            return ServiceResult<DocumentDetailDto>.Conflict(
                "NO_REQUIREMENTS",
                "Phiên bản chưa có yêu cầu (REQ) nào.");
        }

        var needsConfirmCount = CountOccurrences(version.ContentText, "[Cần BA xác nhận]");
        if (needsConfirmCount > 0)
        {
            return ServiceResult<DocumentDetailDto>.Conflict(
                "NEEDS_CONFIRMATION",
                $"Còn {needsConfirmCount} chỗ [Cần BA xác nhận] — xác nhận hoặc sửa nội dung rồi mới gửi duyệt được.");
        }

        var userId = _currentUser.UserId;
        var requiredApproverIds = ParseRequiredApprovers(node.RequiredApprovers);
        if (requiredApproverIds.Count == 1 && requiredApproverIds[0] == userId)
        {
            return ServiceResult<DocumentDetailDto>.Conflict(
                "SOLE_APPROVER_IS_SUBMITTER",
                "Bạn là người duyệt duy nhất được chỉ định cho tài liệu này nên không thể tự gửi duyệt.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        var existingReqChanges = await _db.DocumentVersionReqChanges
            .Where(c => c.VersionId == versionId)
            .ToListAsync(ct);
        if (existingReqChanges.Count > 0)
        {
            _db.RemoveRange(existingReqChanges);
        }

        var minorSet = (request?.MinorReqUids ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .ToHashSet();

        foreach (var reqUid in reqUids.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _db.Add(new DocumentVersionReqChange
            {
                VersionId = versionId,
                ReqUid = reqUid,
                Level = minorSet.Contains(reqUid.ToUpperInvariant()) ? "MINOR" : "MAJOR"
            });
        }

        version.Status = DocumentVersionStatuses.InReview;
        version.SubmittedById = userId;
        version.SubmittedAt = now;
        version.ReviewedById = null;
        version.ReviewedAt = null;
        version.ReviewNote = null;
        version.UpdatedById = userId;
        version.UpdatedAt = now;
        version.RowVersion += 1;
        node.UpdatedAt = now;

        _db.Add(new DocumentActivity
        {
            DocumentId = documentId,
            VersionId = versionId,
            Action = DocumentActivityActions.Submitted,
            ActorId = userId,
            Note = $"Gửi duyệt phiên bản v{version.Label}",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        var approverIds = ParseRequiredApprovers(node.RequiredApprovers);
        foreach (var approverId in approverIds.Where(id => id != userId))
        {
            await _notifications.NotifyAsync(
                approverId,
                "DOC_SUBMITTED",
                $"Tài liệu chờ duyệt: {node.Title}",
                $"Phiên bản v{version.Label} vừa được gửi duyệt.",
                $"/projects/{node.ProjectId}/documents/{node.Id}",
                ct);
        }

        return await GetDocumentDetailAsync(documentId, versionId, ct);
    }

    public async Task<ServiceResult<DocumentDetailDto>> ApproveVersionAsync(
        long documentId,
        long versionId,
        ApproveDocumentRequest request,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentDetailDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentApprove, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentDetailDto>();

        var version = await _db.DocumentVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.DocumentId == documentId, ct);
        if (version == null)
            return ServiceResult<DocumentDetailDto>.NotFound("VERSION_NOT_FOUND", "Document version not found.");

        if (version.Status != DocumentVersionStatuses.InReview)
        {
            return ServiceResult<DocumentDetailDto>.Conflict(
                "INVALID_VERSION_STATE",
                "Chỉ có thể phê duyệt phiên bản đang ở trạng thái IN_REVIEW.");
        }

        var userId = _currentUser.UserId;

        if (version.SubmittedById == userId)
        {
            return ServiceResult<DocumentDetailDto>.Conflict(
                "SELF_APPROVAL_NOT_ALLOWED",
                "Người gửi duyệt không thể tự phê duyệt phiên bản của chính mình.");
        }

        var requiredApproverIds = ParseRequiredApprovers(node.RequiredApprovers);
        if (requiredApproverIds.Count > 0 && !requiredApproverIds.Contains(userId))
        {
            return ServiceResult<DocumentDetailDto>.Forbidden(
                "NOT_REQUIRED_APPROVER",
                "Bạn không nằm trong danh sách người duyệt được chỉ định cho tài liệu này.");
        }

        var openCommentsCount = await _db.DocumentComments
            .CountAsync(c => c.DocumentId == documentId &&
                             c.VersionId == versionId &&
                             c.ParentId == null &&
                             c.ResolvedAt == null, ct);
        if (openCommentsCount > 0)
        {
            return new ServiceResult<DocumentDetailDto>
            {
                StatusCode = StatusCodes.Status409Conflict,
                ErrorCode = "OPEN_COMMENTS",
                ErrorMessage = $"Còn {openCommentsCount} chuỗi bình luận chưa giải quyết.",
                Errors = new Dictionary<string, string[]>
                {
                    ["openComments"] = new[] { openCommentsCount.ToString() }
                }
            };
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var prevBaselineId = node.BaselineVersionId;
            List<RequirementItem> oldReqs = new();

            if (prevBaselineId.HasValue && prevBaselineId.Value != version.Id)
            {
                var prevBaseline = await _db.DocumentVersions
                    .FirstOrDefaultAsync(v => v.Id == prevBaselineId.Value, innerCt);
                if (prevBaseline != null)
                {
                    prevBaseline.Status = DocumentVersionStatuses.Superseded;
                    prevBaseline.UpdatedAt = now;
                }

                oldReqs = await _db.RequirementItems
                    .AsNoTracking()
                    .Where(r => r.VersionId == prevBaselineId.Value)
                    .ToListAsync(innerCt);
            }

            var newReqs = await _db.RequirementItems
                .AsNoTracking()
                .Where(r => r.VersionId == version.Id)
                .ToListAsync(innerCt);

            version.Status = DocumentVersionStatuses.Approved;
            version.ReviewedById = userId;
            version.ReviewedAt = now;
            version.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            version.UpdatedById = userId;
            version.UpdatedAt = now;
            version.RowVersion += 1;

            node.BaselineVersionId = version.Id;
            node.CurrentVersionId = version.Id;
            node.UpdatedAt = now;
            _db.Update(version);
            _db.Update(node);

            // Cross-module propagation for changed/removed requirements
            await _requirementSync.PropagateBaselineChangesAsync(node, version, newReqs, userId, now, innerCt);

            string? activityNote = version.ReviewNote;
            if (string.IsNullOrWhiteSpace(activityNote) && prevBaselineId.HasValue)
            {
                var diffSummary = DocumentContentHelper.SummarizeReqChanges(oldReqs, newReqs);
                activityNote = DocumentContentHelper.FormatReqSummary(diffSummary);
            }

            _db.Add(new DocumentActivity
            {
                DocumentId = documentId,
                VersionId = version.Id,
                Action = DocumentActivityActions.Approved,
                ActorId = userId,
                Note = activityNote ?? $"Phê duyệt baseline v{version.Label}",
                CreatedAt = now
            });

            await _db.SaveChangesAsync(innerCt);

            if (version.SubmittedById.HasValue && version.SubmittedById.Value != userId)
            {
                await _notifications.NotifyAsync(
                    version.SubmittedById.Value,
                    "DOC_APPROVED",
                    $"Đã phê duyệt: {node.Title}",
                    $"Phiên bản v{version.Label} đã được phê duyệt.",
                    $"/projects/{node.ProjectId}/documents/{node.Id}",
                    innerCt);
            }

            return await GetDocumentDetailAsync(documentId, version.Id, innerCt);
        }, ct);
    }

    public async Task<ServiceResult<DocumentDetailDto>> RequestChangesAsync(
        long documentId,
        long versionId,
        RequestChangesRequest request,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentDetailDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentApprove, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentDetailDto>();

        var note = request.Note?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(note) || note.Length > 1000)
        {
            return ServiceResult<DocumentDetailDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["note"] = new[] { "Note is required and must be at most 1000 characters." }
            });
        }

        var version = await _db.DocumentVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.DocumentId == documentId, ct);
        if (version == null)
            return ServiceResult<DocumentDetailDto>.NotFound("VERSION_NOT_FOUND", "Document version not found.");

        if (version.Status != DocumentVersionStatuses.InReview)
        {
            return ServiceResult<DocumentDetailDto>.Conflict(
                "INVALID_VERSION_STATE",
                "Chỉ có thể yêu cầu chỉnh sửa đối với phiên bản đang ở trạng thái IN_REVIEW.");
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        version.Status = DocumentVersionStatuses.ChangesRequested;
        version.ReviewedById = userId;
        version.ReviewedAt = now;
        version.ReviewNote = note;
        version.UpdatedById = userId;
        version.UpdatedAt = now;
        version.RowVersion += 1;
        node.UpdatedAt = now;

        _db.Add(new DocumentActivity
        {
            DocumentId = documentId,
            VersionId = version.Id,
            Action = DocumentActivityActions.ChangesRequested,
            ActorId = userId,
            Note = note,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        if (version.SubmittedById.HasValue && version.SubmittedById.Value != userId)
        {
            await _notifications.NotifyAsync(
                version.SubmittedById.Value,
                "DOC_CHANGES_REQUESTED",
                $"Yêu cầu chỉnh sửa: {node.Title}",
                note,
                $"/projects/{node.ProjectId}/documents/{node.Id}",
                ct);
        }

        return await GetDocumentDetailAsync(documentId, version.Id, ct);
    }

    public async Task<ServiceResult<DocumentDetailDto>> CreateRevisionAsync(long documentId, CreateRevisionRequest? request = null, CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .Include(d => d.CurrentVersion)
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentDetailDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentDetailDto>();

        if (node.Kind != DocumentNodeKinds.Page || node.CurrentVersion == null)
            return ServiceResult<DocumentDetailDto>.BadRequest("NOT_A_PAGE", "Only page documents have versions.");

        var currentVer = node.CurrentVersion;
        if (currentVer.Status != DocumentVersionStatuses.Approved)
        {
            return ServiceResult<DocumentDetailDto>.Conflict(
                "REVISION_ALREADY_OPEN",
                "Tài liệu đã có bản nháp đang mở. Chỉ tạo được revision mới khi phiên bản hiện hành đã APPROVED.");
        }

        var nextLabel = ComputeNextVersionLabel(currentVer.Label, request?.Bump);
        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;
        var summaryText = !string.IsNullOrWhiteSpace(request?.ChangeSummary)
            ? request.ChangeSummary.Trim()
            : (!string.IsNullOrWhiteSpace(request?.Summary) ? request.Summary.Trim() : null);

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var newVersion = new DocumentVersion
            {
                DocumentId = documentId,
                Label = nextLabel,
                Status = DocumentVersionStatuses.Draft,
                Subtitle = currentVer.Subtitle,
                ReviewNote = summaryText,
                Content = currentVer.Content,
                ContentText = currentVer.ContentText,
                BaseVersionId = currentVer.Id,
                RowVersion = 1,
                AuthorId = userId,
                UpdatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Add(newVersion);
            await _db.SaveChangesAsync(innerCt);

            var currentReqs = await _db.RequirementItems
                .AsNoTracking()
                .Where(r => r.VersionId == currentVer.Id)
                .OrderBy(r => r.SortOrder)
                .ToListAsync(innerCt);

            foreach (var r in currentReqs)
            {
                _db.Add(new RequirementItem
                {
                    DocumentId = documentId,
                    VersionId = newVersion.Id,
                    ReqUid = r.ReqUid,
                    AnchorId = r.AnchorId,
                    Title = r.Title,
                    BodyPreview = r.BodyPreview,
                    BodyLength = r.BodyLength,
                    Actor = r.Actor,
                    Preconditions = r.Preconditions,
                    Constraints = r.Constraints,
                    AcceptanceCriteria = r.AcceptanceCriteria,
                    HasTable = r.HasTable,
                    HasDiagram = r.HasDiagram,
                    HasFigma = r.HasFigma,
                    ContentHash = r.ContentHash,
                    SortOrder = r.SortOrder
                });
            }

            node.CurrentVersionId = newVersion.Id;
            node.UpdatedAt = now;
            _db.Update(node);

            _db.Add(new DocumentActivity
            {
                DocumentId = documentId,
                VersionId = newVersion.Id,
                Action = DocumentActivityActions.RevisionCreated,
                ActorId = userId,
                Note = $"Tạo bản nháp v{nextLabel} từ baseline v{currentVer.Label}",
                CreatedAt = now
            });

            await _db.SaveChangesAsync(innerCt);

            var detailRes = await GetDocumentDetailAsync(documentId, newVersion.Id, innerCt);
            return ServiceResult<DocumentDetailDto>.Created(detailRes.Data!);
        }, ct);
    }

    public async Task<ServiceResult<DocumentGenerationDto>> GetDocumentGenerationAsync(long documentId, CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentGenerationDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentView, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentGenerationDto>();

        var gen = await _db.DocumentGenerations
            .AsNoTracking()
            .Include(g => g.CreatedBy)
            .FirstOrDefaultAsync(g => g.DocumentId == documentId, ct);
        if (gen == null)
            return ServiceResult<DocumentGenerationDto>.NotFound("GENERATION_NOT_FOUND", "Document was not generated from meeting notes.");

        var fields = SafeDeserialize<NormalizedFieldsDto>(gen.Fields) ?? new NormalizedFieldsDto();
        var items = SafeDeserialize<List<GeneratedItemDto>>(gen.Items) ?? new List<GeneratedItemDto>();

        return ServiceResult<DocumentGenerationDto>.Success(new DocumentGenerationDto
        {
            RawNotes = gen.RawNotes,
            Fields = fields,
            Items = items,
            CreatedBy = MapUserRef(gen.CreatedBy),
            CreatedAt = FormatUtc(gen.CreatedAt)
        });
    }

    public async Task<ServiceResult<ItemsDto<DocSectionDto>>> GetDocumentSectionsAsync(
        long documentId,
        long? versionId,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<ItemsDto<DocSectionDto>>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var docAuth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentView, ct);
        if (!docAuth.IsSuccess)
        {
            var tcAuth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.TestCaseView, ct);
            if (!tcAuth.IsSuccess) return docAuth.Cast<ItemsDto<DocSectionDto>>();
        }

        var targetVersionId = versionId ?? node.BaselineVersionId ?? node.CurrentVersionId;
        if (!targetVersionId.HasValue)
            return ServiceResult<ItemsDto<DocSectionDto>>.Success(new ItemsDto<DocSectionDto>());

        var version = await _db.DocumentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == targetVersionId.Value && v.DocumentId == documentId, ct);
        if (version == null)
            return ServiceResult<ItemsDto<DocSectionDto>>.NotFound("VERSION_NOT_FOUND", "Document version not found.");

        var sections = DocumentContentHelper.ExtractDocSections(version.Content);
        return ServiceResult<ItemsDto<DocSectionDto>>.Success(new ItemsDto<DocSectionDto>(sections));
    }

    public async Task<ServiceResult<ItemsDto<RequirementItemDto>>> GetDocumentRequirementsAsync(
        long documentId,
        long? versionId,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<ItemsDto<RequirementItemDto>>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<RequirementItemDto>>();

        var targetVersionId = versionId ?? node.CurrentVersionId;
        if (!targetVersionId.HasValue)
            return ServiceResult<ItemsDto<RequirementItemDto>>.Success(new ItemsDto<RequirementItemDto>());

        var version = await _db.DocumentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == targetVersionId.Value && v.DocumentId == documentId, ct);
        if (version == null)
            return ServiceResult<ItemsDto<RequirementItemDto>>.NotFound("VERSION_NOT_FOUND", "Document version not found.");

        var reqs = await _db.RequirementItems
            .AsNoTracking()
            .Where(r => r.VersionId == targetVersionId.Value)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var baselineHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (node.BaselineVersionId.HasValue && node.BaselineVersionId.Value != targetVersionId.Value)
        {
            var baseReqs = await _db.RequirementItems
                .AsNoTracking()
                .Where(r => r.VersionId == node.BaselineVersionId.Value)
                .ToListAsync(ct);
            foreach (var br in baseReqs)
                baselineHashes[br.ReqUid] = br.ContentHash;
        }

        var staleUids = await _requirementSync.GetStaleRequirementUidsAsync(documentId, ct);

        var priorities = ExtractPrioritiesFromContent(version.Content);

        var dtos = reqs.Select((r, idx) =>
        {
            var dto = MapRequirementDto(r, idx);
            if (priorities.TryGetValue(r.ReqUid, out var prio))
                dto.Priority = prio;
            if (staleUids.Contains(r.ReqUid))
                dto.Freshness = "STALE";
            if (node.BaselineVersionId.HasValue && node.BaselineVersionId.Value != targetVersionId.Value)
            {
                dto.ChangedSinceBaseline = !baselineHashes.TryGetValue(r.ReqUid, out var bh) ||
                                           !string.Equals(bh, r.ContentHash, StringComparison.OrdinalIgnoreCase);
            }
            return dto;
        }).ToList();

        return ServiceResult<ItemsDto<RequirementItemDto>>.Success(new ItemsDto<RequirementItemDto>(dtos));
    }

    public async Task<ServiceResult<RequirementItemDto>> UpdateRequirementAsync(
        long requirementId,
        UpdateRequirementRequest request,
        CancellationToken ct = default)
    {
        var req = await _db.RequirementItems
            .Include(r => r.Document)
            .Include(r => r.Version)
            .FirstOrDefaultAsync(r => r.Id == requirementId && !r.Document.IsDeleted, ct);
        if (req == null)
            return ServiceResult<RequirementItemDto>.NotFound("REQUIREMENT_NOT_FOUND", "Requirement not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(req.Document.ProjectId, PermissionKeys.DocumentUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<RequirementItemDto>();

        var prio = (request?.Priority ?? "MEDIUM").Trim().ToUpperInvariant();
        if (prio is not ("HIGH" or "MEDIUM" or "LOW")) prio = "MEDIUM";

        try
        {
            var root = System.Text.Json.Nodes.JsonNode.Parse(req.Version.Content) as System.Text.Json.Nodes.JsonObject;
            if (root?["content"] is System.Text.Json.Nodes.JsonArray blocks)
            {
                foreach (var b in blocks)
                {
                    if (b is System.Text.Json.Nodes.JsonObject obj &&
                        obj["type"]?.GetValue<string>() == "heading" &&
                        obj["attrs"] is System.Text.Json.Nodes.JsonObject attrs)
                    {
                        var uid = attrs["reqUid"]?.GetValue<string>();
                        var aid = attrs["id"]?.GetValue<string>() ?? attrs["anchorId"]?.GetValue<string>();
                        if (string.Equals(uid, req.ReqUid, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(aid, req.AnchorId, StringComparison.OrdinalIgnoreCase))
                        {
                            attrs["priority"] = prio;
                        }
                    }
                }
                req.Version.Content = root.ToJsonString();
                _db.Update(req.Version);
                await _db.SaveChangesAsync(ct);
            }
        }
        catch
        {
            // Ignore content parse errors
        }

        var dto = MapRequirementDto(req);
        dto.Priority = prio;
        return ServiceResult<RequirementItemDto>.Success(dto);
    }

    public async Task<ServiceResult<RequirementItemDto>> ConfirmRequirementFreshByIdAsync(
        long requirementId,
        CancellationToken ct = default)
    {
        var req = await _db.RequirementItems
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requirementId, ct);
        if (req == null)
            return ServiceResult<RequirementItemDto>.NotFound("REQUIREMENT_NOT_FOUND", "Requirement not found.");

        var res = await ConfirmRequirementFreshAsync(req.DocumentId, req.ReqUid, ct);
        if (!res.IsSuccess) return res.Cast<RequirementItemDto>();

        var dto = MapRequirementDto(req);
        dto.Freshness = "FRESH";
        return ServiceResult<RequirementItemDto>.Success(dto);
    }

    public async Task<ServiceResult> ConfirmRequirementFreshAsync(long documentId, string reqUid, CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentUpdate, ct);
        if (!auth.IsSuccess) return auth;

        var targetVersionId = node.BaselineVersionId ?? node.CurrentVersionId;
        if (!targetVersionId.HasValue)
            return ServiceResult.NotFound("REQUIREMENT_NOT_FOUND", "Requirement not found.");

        var normalizedUid = reqUid.Trim().ToUpperInvariant();
        var req = await _db.RequirementItems
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.VersionId == targetVersionId.Value && r.ReqUid == normalizedUid, ct);
        if (req == null)
            return ServiceResult.NotFound("REQUIREMENT_NOT_FOUND", "Requirement not found in baseline/current version.");

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        await _requirementSync.ConfirmRequirementFreshAsync(documentId, normalizedUid, req.ContentHash, now, ct);

        _db.Add(new DocumentActivity
        {
            DocumentId = documentId,
            VersionId = targetVersionId.Value,
            Action = DocumentActivityActions.ReqConfirmedFresh,
            ActorId = userId,
            Note = $"Xác nhận {normalizedUid} đã đồng bộ",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult<DocumentDiffDto>> GetDocumentDiffAsync(
        long documentId,
        long fromVersionId,
        long toVersionId,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentDiffDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentView, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentDiffDto>();

        var fromVer = await _db.DocumentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == fromVersionId && v.DocumentId == documentId, ct);
        var toVer = await _db.DocumentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == toVersionId && v.DocumentId == documentId, ct);

        if (fromVer == null || toVer == null)
            return ServiceResult<DocumentDiffDto>.NotFound("VERSION_NOT_FOUND", "One or both document versions were not found.");

        var fromReqs = await _db.RequirementItems
            .AsNoTracking()
            .Where(r => r.VersionId == fromVersionId)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var toReqs = await _db.RequirementItems
            .AsNoTracking()
            .Where(r => r.VersionId == toVersionId)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var fromMap = fromReqs.ToDictionary(r => r.ReqUid, StringComparer.OrdinalIgnoreCase);
        var toMap = toReqs.ToDictionary(r => r.ReqUid, StringComparer.OrdinalIgnoreCase);

        var items = new List<RequirementDiffItemDto>();
        int added = 0, modified = 0, removed = 0, unchanged = 0;

        foreach (var after in toReqs)
        {
            if (!fromMap.TryGetValue(after.ReqUid, out var before))
            {
                added++;
                items.Add(new RequirementDiffItemDto
                {
                    ReqUid = after.ReqUid,
                    Title = after.Title,
                    Change = "ADDED",
                    Before = null,
                    After = MapSnapshot(after)
                });
            }
            else if (!string.Equals(before.ContentHash, after.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                modified++;
                items.Add(new RequirementDiffItemDto
                {
                    ReqUid = after.ReqUid,
                    Title = after.Title,
                    Change = "MODIFIED",
                    Before = MapSnapshot(before),
                    After = MapSnapshot(after)
                });
            }
            else
            {
                unchanged++;
                items.Add(new RequirementDiffItemDto
                {
                    ReqUid = after.ReqUid,
                    Title = after.Title,
                    Change = "UNCHANGED",
                    Before = MapSnapshot(before),
                    After = MapSnapshot(after)
                });
            }
        }

        foreach (var before in fromReqs)
        {
            if (!toMap.ContainsKey(before.ReqUid))
            {
                removed++;
                items.Add(new RequirementDiffItemDto
                {
                    ReqUid = before.ReqUid,
                    Title = before.Title,
                    Change = "REMOVED",
                    Before = MapSnapshot(before),
                    After = null
                });
            }
        }

        return ServiceResult<DocumentDiffDto>.Success(new DocumentDiffDto
        {
            FromVersion = new DocumentVersionRefDto
            {
                VersionId = fromVer.Id,
                Label = fromVer.Label,
                Status = fromVer.Status
            },
            ToVersion = new DocumentVersionRefDto
            {
                VersionId = toVer.Id,
                Label = toVer.Label,
                Status = toVer.Status
            },
            Summary = new DocumentDiffSummaryDto
            {
                Added = added,
                Modified = modified,
                Removed = removed,
                Unchanged = unchanged
            },
            Items = items,
            TextBefore = fromVer.ContentText,
            TextAfter = toVer.ContentText
        });
    }

    public async Task<ServiceResult<ItemsDto<DocumentCommentDto>>> GetDocumentCommentsAsync(
        long documentId,
        long? versionId,
        bool includeResolved,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<ItemsDto<DocumentCommentDto>>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<DocumentCommentDto>>();

        var query = _db.DocumentComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.ResolvedBy)
            .Include(c => c.Mentions)
                .ThenInclude(m => m.User)
            .Where(c => c.DocumentId == documentId);

        if (versionId.HasValue)
        {
            query = query.Where(c => c.VersionId == versionId.Value);
        }

        var allComments = await query
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        var roots = allComments
            .Where(c => c.ParentId == null && (includeResolved || c.ResolvedAt == null))
            .ToList();

        var activeRootIds = roots.Select(r => r.Id).ToHashSet();

        var repliesByParent = allComments
            .Where(c => c.ParentId.HasValue && activeRootIds.Contains(c.ParentId.Value))
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dtos = allComments
            .Where(c => (c.ParentId == null && activeRootIds.Contains(c.Id)) ||
                        (c.ParentId.HasValue && activeRootIds.Contains(c.ParentId.Value)))
            .Select(c => MapCommentDto(
                c,
                c.ParentId == null ? (repliesByParent.GetValueOrDefault(c.Id) ?? new List<DocumentComment>()) : new List<DocumentComment>()))
            .ToList();
        return ServiceResult<ItemsDto<DocumentCommentDto>>.Success(new ItemsDto<DocumentCommentDto>(dtos));
    }

    public async Task<ServiceResult<DocumentCommentDto>> CreateDocumentCommentAsync(
        long documentId,
        CreateDocumentCommentRequest request,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<DocumentCommentDto>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentComment, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentCommentDto>();

        var body = request.Body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000)
        {
            return ServiceResult<DocumentCommentDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["body"] = new[] { "Comment body is required and must be at most 2000 characters." }
            });
        }

        var version = await _db.DocumentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VersionId && v.DocumentId == documentId, ct);
        if (version == null)
            return ServiceResult<DocumentCommentDto>.NotFound("VERSION_NOT_FOUND", "Document version not found.");

        if (request.ParentId.HasValue)
        {
            var parent = await _db.DocumentComments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ParentId.Value && c.DocumentId == documentId, ct);
            if (parent == null)
                return ServiceResult<DocumentCommentDto>.NotFound("COMMENT_NOT_FOUND", "Parent comment not found.");
            if (parent.ParentId.HasValue)
                return ServiceResult<DocumentCommentDto>.BadRequest("COMMENT_NESTING_LIMIT", "Replies can only be nested 1 level deep.");
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var comment = new DocumentComment
        {
            DocumentId = documentId,
            VersionId = request.VersionId,
            ParentId = request.ParentId,
            AnchorId = string.IsNullOrWhiteSpace(request.AnchorId) ? null : request.AnchorId.Trim(),
            Quote = string.IsNullOrWhiteSpace(request.Quote) ? null : (request.Quote.Trim().Length > 300 ? request.Quote.Trim()[..300] : request.Quote.Trim()),
            Body = body,
            AuthorId = userId,
            CreatedAt = now
        };

        _db.Add(comment);
        await _db.SaveChangesAsync(ct);

        if (request.MentionUserIds != null && request.MentionUserIds.Count > 0)
        {
            var validUserIds = await _db.ProjectMembers
                .AsNoTracking()
                .Where(pm => pm.ProjectId == node.ProjectId && request.MentionUserIds.Distinct().Contains(pm.UserId))
                .Select(pm => pm.UserId)
                .ToListAsync(ct);

            foreach (var uid in validUserIds)
            {
                _db.Add(new DocumentCommentMention
                {
                    CommentId = comment.Id,
                    UserId = uid
                });
            }
        }

        _db.Add(new DocumentActivity
        {
            DocumentId = documentId,
            VersionId = request.VersionId,
            Action = DocumentActivityActions.Commented,
            ActorId = userId,
            Note = body.Length > 120 ? body[..120] + "…" : body,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        var loaded = await _db.DocumentComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.ResolvedBy)
            .Include(c => c.Mentions)
                .ThenInclude(m => m.User)
            .FirstAsync(c => c.Id == comment.Id, ct);

        return ServiceResult<DocumentCommentDto>.Created(MapCommentDto(loaded, new List<DocumentComment>()));
    }

    public async Task<ServiceResult<DocumentCommentDto>> ResolveCommentAsync(long commentId, CancellationToken ct = default)
    {
        var comment = await _db.DocumentComments
            .Include(c => c.Document)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.Document.IsDeleted, ct);
        if (comment == null)
            return ServiceResult<DocumentCommentDto>.NotFound("COMMENT_NOT_FOUND", "Comment not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(comment.Document.ProjectId, PermissionKeys.DocumentComment, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentCommentDto>();

        var rootId = comment.ParentId ?? comment.Id;
        var threadComments = await _db.DocumentComments
            .Where(c => c.Id == rootId || c.ParentId == rootId)
            .ToListAsync(ct);

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var c in threadComments)
        {
            c.ResolvedById = userId;
            c.ResolvedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        var reloadedThread = await _db.DocumentComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.ResolvedBy)
            .Include(c => c.Mentions)
                .ThenInclude(m => m.User)
            .Where(c => c.Id == rootId || c.ParentId == rootId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var root = reloadedThread.First(c => c.Id == rootId);
        var replies = reloadedThread.Where(c => c.ParentId == rootId).ToList();

        return ServiceResult<DocumentCommentDto>.Success(MapCommentDto(root, replies));
    }

    public async Task<ServiceResult<ItemsDto<DocumentActivityDto>>> GetDocumentActivitiesAsync(
        long documentId,
        CancellationToken ct = default)
    {
        var node = await _db.DocumentNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted, ct);
        if (node == null)
            return ServiceResult<ItemsDto<DocumentActivityDto>>.NotFound("DOCUMENT_NOT_FOUND", "Document not found.");

        var auth = await _accessGuard.EnsureProjectPermissionAsync(node.ProjectId, PermissionKeys.DocumentView, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<DocumentActivityDto>>();

        var activities = await _db.DocumentActivities
            .AsNoTracking()
            .Include(a => a.Actor)
            .Include(a => a.Version)
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        var dtos = activities.Select(a => new DocumentActivityDto
        {
            ActivityId = a.Id,
            DocumentId = a.DocumentId,
            VersionId = a.VersionId,
            VersionLabel = a.Version?.Label,
            Action = a.Action,
            Actor = MapUserRef(a.Actor),
            Note = a.Note,
            CreatedAt = FormatUtc(a.CreatedAt)
        }).ToList();

        return ServiceResult<ItemsDto<DocumentActivityDto>>.Success(new ItemsDto<DocumentActivityDto>(dtos));
    }

    public async Task<ServiceResult<NormalizedFieldsDto>> NormalizeNotesAsync(
        long projectId,
        NormalizeNotesRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.DocumentCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<NormalizedFieldsDto>();

        var raw = request.RawNotes?.Trim() ?? string.Empty;
        if (raw.Length < 10 || raw.Length > 20000)
        {
            return ServiceResult<NormalizedFieldsDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["rawNotes"] = new[] { "rawNotes must be between 10 and 20,000 characters." }
            });
        }

        var normalized = DocumentGeneratorHelper.NormalizeNotes(raw);
        return ServiceResult<NormalizedFieldsDto>.Success(normalized);
    }

    public async Task<ServiceResult<ItemsDto<ExtractedRequirementDto>>> ExtractRequirementsAsync(
        long projectId,
        ExtractRequirementsRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.DocumentCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<ItemsDto<ExtractedRequirementDto>>();

        var aiAuth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.AiUse, ct);
        if (!aiAuth.IsSuccess) return aiAuth.Cast<ItemsDto<ExtractedRequirementDto>>();

        var aiEnabled = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => (bool?)p.AiEnabled)
            .SingleOrDefaultAsync(ct);
        if (aiEnabled != true)
        {
            return ServiceResult<ItemsDto<ExtractedRequirementDto>>.Forbidden(
                "AI_DISABLED",
                "AI features are disabled for this project.");
        }

        var fields = request.Fields ?? new NormalizedFieldsDto();
        if (string.IsNullOrWhiteSpace(fields.Scope) &&
            string.IsNullOrWhiteSpace(fields.Actors) &&
            string.IsNullOrWhiteSpace(fields.MainFlows) &&
            string.IsNullOrWhiteSpace(fields.BusinessRules) &&
            string.IsNullOrWhiteSpace(request.RawNotes))
        {
            return ServiceResult<ItemsDto<ExtractedRequirementDto>>.ValidationFailure(
                new Dictionary<string, string[]>
                {
                    ["fields"] = new[] { "Provide normalized fields or raw notes to generate requirements." }
                });
        }

        var items = await _aiService.GenerateRequirementDraftsAsync(fields, request.RawNotes, ct);
        return ServiceResult<ItemsDto<ExtractedRequirementDto>>.Success(new ItemsDto<ExtractedRequirementDto>(items));
    }

    public async Task<ServiceResult<DocumentDetailDto>> GenerateDocumentAsync(
        long projectId,
        GenerateDocumentRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.DocumentCreate, ct);
        if (!auth.IsSuccess) return auth.Cast<DocumentDetailDto>();

        var errors = new Dictionary<string, string[]>();
        var title = request.Title?.Trim() ?? string.Empty;
        var docType = request.DocType?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title) || title.Length > 150)
            errors["title"] = new[] { "Title is required and must be at most 150 characters." };
        if (!DocumentTypes.IsValid(docType))
            errors["docType"] = new[] { "docType must be valid (BRD, SRS, ADR, UI_SPEC, TEST_PLAN, OTHER)." };
        if (request.Items == null || request.Items.Count == 0)
            errors["items"] = new[] { "At least 1 requirement item is required." };

        var acceptedItems = request.Items?
            .Where(item => item is not null &&
                           !string.Equals(item.ReviewStatus, ExtractedRequirementDto.ReviewRejected, StringComparison.OrdinalIgnoreCase))
            .ToList() ?? new List<ExtractedRequirementDto>();
        if (request.Items is { Count: > 0 } && acceptedItems.Count == 0)
            errors["items"] = new[] { "Accept at least one requirement before creating the document." };

        if (errors.Count > 0)
            return ServiceResult<DocumentDetailDto>.ValidationFailure(errors);

        if (request.ParentId.HasValue)
        {
            var parent = await _db.DocumentNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.ParentId.Value && d.ProjectId == projectId && !d.IsDeleted, ct);
            if (parent == null)
                return ServiceResult<DocumentDetailDto>.BadRequest("INVALID_PARENT", "Parent must be an existing folder or page in the same project.");
        }

        var userId = _currentUser.UserId;
        var now = _clock.GetUtcNow().UtcDateTime;

        return await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var code = await _db.AllocateDocumentCodeAsync(projectId, docType, request.ParentId, innerCt);

            var generatedItems = new List<GeneratedItemDto>();
            foreach (var item in acceptedItems)
            {
                var reqUid = await _db.AllocateReqUidAsync(projectId, innerCt);
                var acceptanceCriteria = item.AcceptanceCriteria ?? new List<string>();
                var diagram = item.Diagram;
                if (docType == DocumentTypes.Srs &&
                    (diagram is null || string.IsNullOrWhiteSpace(diagram.Mermaid)))
                {
                    diagram = DiagramGeneratorHelper.TryGenerateDiagram(
                        string.Join(Environment.NewLine, acceptanceCriteria),
                        "flowchart");
                }

                generatedItems.Add(new GeneratedItemDto
                {
                    ReqUid = reqUid,
                    Title = item.Title.Trim(),
                    Description = item.Description.Trim(),
                    Priority = item.Priority,
                    AcceptanceCriteria = acceptanceCriteria,
                    SourceLines = item.SourceLines ?? new List<int>(),
                    Confidence = item.Confidence,
                    Diagram = diagram,
                    IsAiGenerated = true,
                    ReviewStatus = ExtractedRequirementDto.ReviewAccepted
                });
            }

            var fields = request.Fields ?? new NormalizedFieldsDto();
            var contentJson = docType switch
            {
                DocumentTypes.Srs => DocumentGeneratorHelper.BuildSrsGeneratedContentJson(fields, generatedItems),
                DocumentTypes.Brd => DocumentGeneratorHelper.BuildBrdGeneratedContentJson(fields, generatedItems),
                DocumentTypes.Urd => DocumentGeneratorHelper.BuildUrdGeneratedContentJson(fields, generatedItems),
                _ => DocumentGeneratorHelper.BuildGeneratedContentJson(fields, generatedItems)
            };
            using var jsonDoc = JsonDocument.Parse(contentJson);
            var (normalizedJson, plainText, extractedReqs) = DocumentContentHelper.ProcessContent(jsonDoc.RootElement);

            var maxSort = await _db.DocumentNodes
                .Where(d => d.ProjectId == projectId && d.ParentId == request.ParentId && !d.IsDeleted)
                .Select(d => (int?)d.SortOrder)
                .MaxAsync(innerCt) ?? -1;

            var node = new DocumentNode
            {
                ProjectId = projectId,
                ParentId = request.ParentId,
                Kind = DocumentNodeKinds.Page,
                Title = title,
                DocType = docType,
                Code = code,
                SortOrder = maxSort + 1,
                CreatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Add(node);
            await _db.SaveChangesAsync(innerCt);

            var version = new DocumentVersion
            {
                DocumentId = node.Id,
                Label = "1.0",
                Status = DocumentVersionStatuses.Draft,
                Subtitle = request.Subtitle?.Trim(),
                Content = normalizedJson,
                ContentText = plainText,
                RowVersion = 1,
                AuthorId = userId,
                UpdatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Add(version);
            await _db.SaveChangesAsync(innerCt);

            node.CurrentVersionId = version.Id;

            foreach (var r in extractedReqs)
            {
                _db.Add(new RequirementItem
                {
                    DocumentId = node.Id,
                    VersionId = version.Id,
                    ReqUid = r.ReqUid,
                    AnchorId = r.AnchorId,
                    Title = r.Title,
                    BodyPreview = r.BodyPreview,
                    BodyLength = r.BodyLength,
                    Actor = r.Actor,
                    Preconditions = r.Preconditions,
                    Constraints = JsonSerializer.Serialize(r.Constraints),
                    AcceptanceCriteria = JsonSerializer.Serialize(r.AcceptanceCriteria),
                    HasTable = r.HasTable,
                    HasDiagram = r.HasDiagram,
                    HasFigma = r.HasFigma,
                    ContentHash = r.ContentHash,
                    SortOrder = r.SortOrder
                });
            }

            _db.Add(new DocumentGeneration
            {
                ProjectId = projectId,
                DocumentId = node.Id,
                RawNotes = request.RawNotes ?? string.Empty,
                Fields = JsonSerializer.Serialize(request.Fields ?? new NormalizedFieldsDto()),
                Items = JsonSerializer.Serialize(generatedItems),
                CreatedById = userId,
                CreatedAt = now
            });

            _db.Add(new DocumentActivity
            {
                DocumentId = node.Id,
                VersionId = version.Id,
                Action = DocumentActivityActions.GeneratedFromNotes,
                ActorId = userId,
                Note = $"Tạo từ ghi chú cuộc họp ({generatedItems.Count} yêu cầu)",
                CreatedAt = now
            });

            await _db.SaveChangesAsync(innerCt);

            var detailRes = await GetDocumentDetailAsync(node.Id, version.Id, innerCt);
            return ServiceResult<DocumentDetailDto>.Created(detailRes.Data!);
        }, ct);
    }

    public async Task<ServiceResult<DiagramProposalDto>> GenerateDiagramAsync(
        long projectId,
        GenerateDiagramRequest request,
        CancellationToken ct = default)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.DocumentUpdate, ct);
        if (!auth.IsSuccess) return auth.Cast<DiagramProposalDto>();

        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length < 10 || text.Length > 4000)
        {
            return ServiceResult<DiagramProposalDto>.ValidationFailure(new Dictionary<string, string[]>
            {
                ["text"] = new[] { "text must be between 10 and 4000 characters." }
            });
        }

        var proposal = DiagramGeneratorHelper.GenerateDiagram(text, request.Kind);
        return ServiceResult<DiagramProposalDto>.Success(proposal);
    }

    // --- Helpers ---

    private static HashSet<long> CollectSubtreeIds(long rootId, List<DocumentNode> allNodes)
    {
        var childrenLookup = allNodes
            .Where(n => n.ParentId.HasValue)
            .GroupBy(n => n.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var visited = new HashSet<long> { rootId };
        var queue = new Queue<long>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (childrenLookup.TryGetValue(current, out var children))
            {
                foreach (var childId in children)
                {
                    if (visited.Add(childId))
                    {
                        queue.Enqueue(childId);
                    }
                }
            }
        }

        return visited;
    }

    private static string ComputeNextVersionLabel(string currentLabel, string? bump = null)
    {
        var raw = (currentLabel ?? "1.0").TrimStart('v', 'V');
        var parts = raw.Split('.');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor))
        {
            if (string.Equals(bump, "MAJOR", StringComparison.OrdinalIgnoreCase))
                return $"{major + 1}.0";
            return $"{major}.{minor + 1}";
        }
        return "1.1";
    }

    private static Dictionary<string, string> ExtractPrioritiesFromContent(string? contentJson)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(contentJson)) return map;
        try
        {
            var root = System.Text.Json.Nodes.JsonNode.Parse(contentJson) as System.Text.Json.Nodes.JsonObject;
            if (root?["content"] is System.Text.Json.Nodes.JsonArray blocks)
            {
                foreach (var b in blocks)
                {
                    if (b is System.Text.Json.Nodes.JsonObject obj &&
                        obj["type"]?.GetValue<string>() == "heading" &&
                        obj["attrs"] is System.Text.Json.Nodes.JsonObject attrs)
                    {
                        var uid = attrs["reqUid"]?.GetValue<string>();
                        var prio = attrs["priority"]?.GetValue<string>()?.ToUpperInvariant();
                        if (!string.IsNullOrWhiteSpace(uid) && prio is "HIGH" or "MEDIUM" or "LOW")
                        {
                            map[uid] = prio;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed JSON
        }
        return map;
    }

    private static DocumentNodeDto MapNodeDto(DocumentNode n, int reqCount, List<UserRefDto>? requiredApprovers = null)
    {
        return new DocumentNodeDto
        {
            DocumentId = n.Id,
            ProjectId = n.ProjectId,
            ParentId = n.ParentId,
            Kind = n.Kind,
            Title = n.Title,
            DocType = n.DocType,
            Code = n.Code,
            SortOrder = n.SortOrder,
            CurrentVersion = n.CurrentVersion == null
                ? null
                : new DocumentVersionRefDto
                {
                    VersionId = n.CurrentVersion.Id,
                    Label = n.CurrentVersion.Label,
                    Status = n.CurrentVersion.Status
                },
            IsBaselined = n.BaselineVersionId.HasValue,
            BaselineVersionId = n.BaselineVersionId,
            ReqCount = reqCount,
            RequiredApprovers = requiredApprovers ?? new List<UserRefDto>(),
            UpdatedAt = FormatUtc(n.UpdatedAt)
        };
    }

    private static List<long> ParseRequiredApprovers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<long>();
        var list = SafeDeserialize<List<long>>(json);
        return list == null ? new List<long>() : list.Where(id => id > 0).Distinct().ToList();
    }

    private async Task<Dictionary<long, List<UserRefDto>>> ResolveApproversMapAsync(
        IEnumerable<DocumentNode> nodes,
        CancellationToken ct)
    {
        var nodeList = nodes.ToList();
        var idsByNode = nodeList.ToDictionary(n => n.Id, n => ParseRequiredApprovers(n.RequiredApprovers));
        var allUserIds = idsByNode.Values.SelectMany(x => x).Distinct().ToList();
        if (allUserIds.Count == 0)
        {
            return nodeList.ToDictionary(n => n.Id, _ => new List<UserRefDto>());
        }

        var usersById = await _db.Users
            .AsNoTracking()
            .Where(u => allUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => MapUserRef(u), ct);

        var result = new Dictionary<long, List<UserRefDto>>();
        foreach (var (nodeId, userIds) in idsByNode)
        {
            var refs = new List<UserRefDto>();
            foreach (var uid in userIds)
            {
                if (usersById.TryGetValue(uid, out var userRef))
                    refs.Add(userRef);
            }
            result[nodeId] = refs;
        }
        return result;
    }

    private static int CountOccurrences(string? text, string needle)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle)) return 0;
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static DocumentVersionSummaryDto MapVersionSummaryDto(DocumentVersion v)
    {
        return new DocumentVersionSummaryDto
        {
            VersionId = v.Id,
            Label = v.Label,
            Status = v.Status,
            Author = v.Author != null ? MapUserRef(v.Author) : MapUserRef(v.UpdatedBy),
            CreatedAt = FormatUtc(v.CreatedAt),
            UpdatedAt = FormatUtc(v.UpdatedAt),
            UpdatedBy = MapUserRef(v.UpdatedBy),
            SubmittedAt = v.SubmittedAt.HasValue ? FormatUtc(v.SubmittedAt.Value) : null,
            ReviewedBy = v.ReviewedBy != null ? MapUserRef(v.ReviewedBy) : null,
            ReviewedAt = v.ReviewedAt.HasValue ? FormatUtc(v.ReviewedAt.Value) : null,
            ReviewNote = v.ReviewNote,
            ChangeSummary = v.ReviewNote ?? v.Subtitle
        };
    }

    private static DocumentVersionDto MapVersionDto(DocumentVersion v)
    {
        JsonElement contentEl;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(v.Content) ? "{}" : v.Content);
            contentEl = DocumentContentHelper.NormalizeMermaidBlocksForEditor(doc.RootElement);
        }
        catch
        {
            using var doc = JsonDocument.Parse("{}");
            contentEl = doc.RootElement.Clone();
        }

        return new DocumentVersionDto
        {
            VersionId = v.Id,
            DocumentId = v.DocumentId,
            Label = v.Label,
            Status = v.Status,
            Subtitle = v.Subtitle,
            Content = contentEl,
            BaseVersionId = v.BaseVersionId,
            RowVersion = v.RowVersion,
            CreatedAt = FormatUtc(v.CreatedAt),
            UpdatedAt = FormatUtc(v.UpdatedAt),
            UpdatedBy = MapUserRef(v.UpdatedBy),
            Author = MapUserRef(v.Author),
            SubmittedBy = v.SubmittedBy != null ? MapUserRef(v.SubmittedBy) : null,
            SubmittedAt = v.SubmittedAt.HasValue ? FormatUtc(v.SubmittedAt.Value) : null,
            ReviewedBy = v.ReviewedBy != null ? MapUserRef(v.ReviewedBy) : null,
            ReviewedAt = v.ReviewedAt.HasValue ? FormatUtc(v.ReviewedAt.Value) : null,
            ReviewNote = v.ReviewNote
        };
    }

    private static RequirementItemDto MapRequirementDto(RequirementItem r, int index = -1)
    {
        var acStrings = SafeDeserialize<List<string>>(r.AcceptanceCriteria) ?? new List<string>();
        var acDtos = acStrings.Select(s => new AcceptanceCriterionDto { Text = s, Checked = true }).ToList();
        var reqKey = r.ReqUid.StartsWith("REQ-", StringComparison.OrdinalIgnoreCase) && r.ReqUid.Length <= 8
            ? r.ReqUid.ToUpperInvariant()
            : $"REQ-{(index >= 0 ? index + 1 : r.SortOrder + 1):D2}";

        return new RequirementItemDto
        {
            RequirementId = r.Id,
            DocumentId = r.DocumentId,
            VersionId = r.VersionId,
            ReqUid = r.ReqUid,
            ReqKey = reqKey,
            AnchorId = r.AnchorId,
            Title = r.Title,
            Priority = "MEDIUM",
            Freshness = "FRESH",
            Actor = r.Actor,
            Preconditions = r.Preconditions,
            Constraints = SafeDeserialize<List<ConstraintRowDto>>(r.Constraints) ?? new List<ConstraintRowDto>(),
            ChangedSinceBaseline = false,
            BodyPreview = r.BodyPreview,
            BodyLength = r.BodyLength,
            AcceptanceCriteria = acDtos,
            HasTable = r.HasTable,
            HasDiagram = r.HasDiagram,
            HasFigma = r.HasFigma,
            ContentHash = r.ContentHash,
            SortOrder = r.SortOrder
        };
    }

    private static RequirementSnapshotDto MapSnapshot(RequirementItem r)
    {
        return new RequirementSnapshotDto
        {
            Title = r.Title,
            BodyPreview = r.BodyPreview,
            AcceptanceCriteria = SafeDeserialize<List<string>>(r.AcceptanceCriteria) ?? new List<string>()
        };
    }

    private static DocumentCommentDto MapCommentDto(DocumentComment c, List<DocumentComment> replies)
    {
        return new DocumentCommentDto
        {
            CommentId = c.Id,
            DocumentId = c.DocumentId,
            VersionId = c.VersionId,
            ParentId = c.ParentId,
            AnchorId = c.AnchorId,
            Quote = c.Quote,
            Body = c.Body,
            Mentions = c.Mentions.Select(m => MapUserRef(m.User)).ToList(),
            Author = MapUserRef(c.Author),
            CreatedAt = FormatUtc(c.CreatedAt),
            ResolvedBy = c.ResolvedBy != null ? MapUserRef(c.ResolvedBy) : null,
            ResolvedAt = c.ResolvedAt.HasValue ? FormatUtc(c.ResolvedAt.Value) : null,
            Replies = replies.Select(rep => MapCommentDto(rep, new List<DocumentComment>())).ToList()
        };
    }
}
