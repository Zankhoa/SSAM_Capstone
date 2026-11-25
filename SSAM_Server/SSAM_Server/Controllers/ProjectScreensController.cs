using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;
using SSAM_Server.Security;
using SSAM_Server.Services.Common;

namespace SSAM_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:long}/screens")]
public class ProjectScreensController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly INotificationService _notifications;
    private readonly IAiService _aiService;
    private readonly ILogger<ProjectScreensController> _logger;
    private readonly string _uploadRoot;

    public ProjectScreensController(
        AppDbContext db,
        ICurrentUser currentUser,
        IProjectAccessGuard accessGuard,
        INotificationService notifications,
        IAiService aiService,
        ILogger<ProjectScreensController> logger,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
        _notifications = notifications;
        _aiService = aiService;
        _logger = logger;
        _uploadRoot = configuration["Storage:UploadPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
    }

    [HttpGet]
    public async Task<IActionResult> List(long projectId, CancellationToken ct)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.ProjectView, ct);
        if (!auth.IsSuccess) return auth.ToActionResult(this);

        var items = await _db.ProjectScreens
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.CreatedBy)
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);

        return Ok(new ItemsDto<ProjectScreenDto>(items.Select(MapDto).ToList()));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Create(long projectId, [FromForm] UpsertProjectScreenForm form, CancellationToken ct)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.ProjectView, ct);
        if (!auth.IsSuccess) return auth.ToActionResult(this);

        var title = form.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return BadRequest(new ErrorResponseDto("VALIDATION_ERROR", "Tên screen/layout phải từ 1 đến 200 ký tự.", null, HttpContext.TraceIdentifier));

        var kind = (form.Kind ?? "SCREEN").Trim().ToUpperInvariant();
        if (kind != "SCREEN" && kind != "LAYOUT")
            return BadRequest(new ErrorResponseDto("VALIDATION_ERROR", "Loại chỉ có thể là SCREEN hoặc LAYOUT.", null, HttpContext.TraceIdentifier));

        if (form.DocumentId.HasValue)
        {
            var docExists = await _db.DocumentNodes.AsNoTracking().AnyAsync(d => d.Id == form.DocumentId.Value && d.ProjectId == projectId && !d.IsDeleted, ct);
            if (!docExists)
                return BadRequest(new ErrorResponseDto("DOCUMENT_NOT_FOUND", "Không tìm thấy tài liệu trong dự án này.", null, HttpContext.TraceIdentifier));
        }

        string? storedName = null;
        string? fileName = null;
        string? contentType = null;
        if (form.Image != null && form.Image.Length > 0)
        {
            if (form.Image.Length > 20 * 1024 * 1024)
                return BadRequest(new ErrorResponseDto("FILE_TOO_LARGE", "Dung lượng ảnh tối đa là 20 MB.", null, HttpContext.TraceIdentifier));

            (fileName, storedName, contentType) = await SaveImageAsync(projectId, form.Image, ct);
        }

        var userId = _currentUser.UserIdOrNull!.Value;
        var now = DateTime.UtcNow;
        var entity = new ProjectScreen
        {
            ProjectId = projectId,
            Title = title,
            Kind = kind,
            FigmaUrl = string.IsNullOrWhiteSpace(form.FigmaUrl) ? null : form.FigmaUrl.Trim(),
            DocumentId = form.DocumentId,
            ReqUidsJson = form.DocumentId.HasValue ? NormalizeReqUidsJson(form.ReqUidsJson) : null,
            Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim(),
            ImageFileName = fileName,
            ImageStoredName = storedName,
            ImageContentType = contentType,
            CreatedById = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ProjectScreens.Add(entity);
        await _db.SaveChangesAsync(ct);

        if (entity.DocumentId.HasValue)
        {
            var affectedCaseInfo = await _db.TestCaseRequirementLinks
                .AsNoTracking()
                .Include(l => l.TestCase)
                    .ThenInclude(tc => tc.Plan)
                .Where(l => l.DocumentId == entity.DocumentId.Value &&
                            l.TestCase.Plan.ProjectId == projectId &&
                            l.TestCase.Status == TestCaseStatuses.Active)
                .Select(l => new { l.TestCase.Id, l.TestCase.Code, l.TestCase.ModuleId })
                .Distinct()
                .ToListAsync(ct);

            if (affectedCaseInfo.Count > 0)
            {
                var actorId = _currentUser.UserIdOrNull ?? 0;
                var moduleIds = affectedCaseInfo.Select(x => x.ModuleId).Distinct().ToList();
                var targetUserIds = await _db.TestAssignments
                    .AsNoTracking()
                    .Where(a => moduleIds.Contains(a.ModuleId))
                    .Select(a => a.AssigneeId)
                    .Distinct()
                    .ToListAsync(ct);

                if (targetUserIds.Count == 0)
                {
                    targetUserIds = await _db.ProjectMembers
                        .AsNoTracking()
                        .Where(pm => pm.ProjectId == projectId && pm.IsActive)
                        .Select(pm => pm.UserId)
                        .Distinct()
                        .ToListAsync(ct);
                }

                var sampleCodes = string.Join(", ", affectedCaseInfo.Select(x => x.Code).Take(3));
                var msg = $"Màn hình/Layout \"{entity.Title}\" vừa cập nhật, có thể ảnh hưởng {affectedCaseInfo.Count} test case ({sampleCodes}).";
                foreach (var uid in targetUserIds)
                {
                    if (uid == actorId) continue;
                    await _notifications.NotifyAsync(
                        uid,
                        "TESTCASE_DESIGN_CHANGED",
                        $"Cảnh báo UI/UX cập nhật: {entity.Title}",
                        msg,
                        $"/projects/{projectId}/tests",
                        ct);
                }
            }
        }

        var loaded = await _db.ProjectScreens
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.CreatedBy)
            .FirstAsync(s => s.Id == entity.Id, ct);

        return Ok(MapDto(loaded));
    }

    [HttpPatch("{screenId:long}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Update(long projectId, long screenId, [FromForm] UpsertProjectScreenForm form, CancellationToken ct)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.ProjectView, ct);
        if (!auth.IsSuccess) return auth.ToActionResult(this);

        var entity = await _db.ProjectScreens.FirstOrDefaultAsync(s => s.Id == screenId && s.ProjectId == projectId, ct);
        if (entity == null)
            return NotFound(new ErrorResponseDto("NOT_FOUND", "Không tìm thấy screen/layout.", null, HttpContext.TraceIdentifier));

        var title = form.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return BadRequest(new ErrorResponseDto("VALIDATION_ERROR", "Tên screen/layout phải từ 1 đến 200 ký tự.", null, HttpContext.TraceIdentifier));

        var kind = (form.Kind ?? "SCREEN").Trim().ToUpperInvariant();
        if (kind != "SCREEN" && kind != "LAYOUT")
            return BadRequest(new ErrorResponseDto("VALIDATION_ERROR", "Loại chỉ có thể là SCREEN hoặc LAYOUT.", null, HttpContext.TraceIdentifier));

        if (form.DocumentId.HasValue)
        {
            var docExists = await _db.DocumentNodes.AsNoTracking().AnyAsync(d => d.Id == form.DocumentId.Value && d.ProjectId == projectId && !d.IsDeleted, ct);
            if (!docExists)
                return BadRequest(new ErrorResponseDto("DOCUMENT_NOT_FOUND", "Không tìm thấy tài liệu trong dự án này.", null, HttpContext.TraceIdentifier));
        }

        if (form.RemoveImage && !string.IsNullOrEmpty(entity.ImageStoredName))
        {
            DeleteImageFile(projectId, entity.ImageStoredName);
            entity.ImageFileName = null;
            entity.ImageStoredName = null;
            entity.ImageContentType = null;
        }

        if (form.Image != null && form.Image.Length > 0)
        {
            if (form.Image.Length > 20 * 1024 * 1024)
                return BadRequest(new ErrorResponseDto("FILE_TOO_LARGE", "Dung lượng ảnh tối đa là 20 MB.", null, HttpContext.TraceIdentifier));

            if (!string.IsNullOrEmpty(entity.ImageStoredName))
                DeleteImageFile(projectId, entity.ImageStoredName);

            var (fileName, storedName, contentType) = await SaveImageAsync(projectId, form.Image, ct);
            entity.ImageFileName = fileName;
            entity.ImageStoredName = storedName;
            entity.ImageContentType = contentType;
        }

        entity.Title = title;
        entity.Kind = kind;
        entity.FigmaUrl = string.IsNullOrWhiteSpace(form.FigmaUrl) ? null : form.FigmaUrl.Trim();
        entity.DocumentId = form.DocumentId;
        entity.ReqUidsJson = form.DocumentId.HasValue ? NormalizeReqUidsJson(form.ReqUidsJson) : null;
        entity.Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();

        await _db.SaveChangesAsync(ct);

        if (entity.DocumentId.HasValue)
        {
            var selectedReqUids = ParseReqUids(entity.ReqUidsJson);
            var linkQuery = _db.TestCaseRequirementLinks
                .AsNoTracking()
                .Include(l => l.TestCase)
                    .ThenInclude(tc => tc.Plan)
                .Where(l => l.DocumentId == entity.DocumentId.Value &&
                            l.TestCase.Plan.ProjectId == projectId &&
                            l.TestCase.Status == TestCaseStatuses.Active);

            if (selectedReqUids.Count > 0)
            {
                var upperUids = selectedReqUids.Select(u => u.ToUpperInvariant()).ToList();
                linkQuery = linkQuery.Where(l => upperUids.Contains(l.ReqUid.ToUpper()));
            }

            var affectedCaseInfo = await linkQuery
                .Select(l => new { l.TestCase.Id, l.TestCase.Code, l.TestCase.ModuleId })
                .Distinct()
                .ToListAsync(ct);

            if (affectedCaseInfo.Count > 0)
            {
                var actorId = _currentUser.UserIdOrNull ?? 0;
                var moduleIds = affectedCaseInfo.Select(x => x.ModuleId).Distinct().ToList();
                var targetUserIds = await _db.TestAssignments
                    .AsNoTracking()
                    .Where(a => moduleIds.Contains(a.ModuleId))
                    .Select(a => a.AssigneeId)
                    .Distinct()
                    .ToListAsync(ct);

                if (targetUserIds.Count == 0)
                {
                    targetUserIds = await _db.ProjectMembers
                        .AsNoTracking()
                        .Where(pm => pm.ProjectId == projectId && pm.IsActive)
                        .Select(pm => pm.UserId)
                        .Distinct()
                        .ToListAsync(ct);
                }

                var sampleCodes = string.Join(", ", affectedCaseInfo.Select(x => x.Code).Take(3));
                var msg = $"Màn hình/Layout \"{entity.Title}\" vừa cập nhật, có thể ảnh hưởng {affectedCaseInfo.Count} test case ({sampleCodes}).";
                foreach (var uid in targetUserIds)
                {
                    if (uid == actorId) continue;
                    await _notifications.NotifyAsync(
                        uid,
                        "TESTCASE_DESIGN_CHANGED",
                        $"Cảnh báo UI/UX cập nhật: {entity.Title}",
                        msg,
                        $"/projects/{projectId}/tests",
                        ct);
                }
            }
        }

        var loaded = await _db.ProjectScreens
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.CreatedBy)
            .FirstAsync(s => s.Id == entity.Id, ct);

        return Ok(MapDto(loaded));
    }

    [HttpGet("{screenId:long}/image")]
    public async Task<IActionResult> GetImage(long projectId, long screenId, CancellationToken ct)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.ProjectView, ct);
        if (!auth.IsSuccess) return auth.ToActionResult(this);

        var entity = await _db.ProjectScreens.AsNoTracking().FirstOrDefaultAsync(s => s.Id == screenId && s.ProjectId == projectId, ct);
        if (entity == null || string.IsNullOrEmpty(entity.ImageStoredName))
            return NotFound();

        var fullPath = Path.Combine(_uploadRoot, "screens", projectId.ToString(CultureInfo.InvariantCulture), entity.ImageStoredName);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, entity.ImageContentType ?? "image/png");
    }

    [HttpDelete("{screenId:long}")]
    public async Task<IActionResult> Delete(long projectId, long screenId, CancellationToken ct)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.ProjectView, ct);
        if (!auth.IsSuccess) return auth.ToActionResult(this);

        var entity = await _db.ProjectScreens.FirstOrDefaultAsync(s => s.Id == screenId && s.ProjectId == projectId, ct);
        if (entity == null)
            return NotFound(new ErrorResponseDto("NOT_FOUND", "Không tìm thấy screen/layout.", null, HttpContext.TraceIdentifier));

        var storedName = entity.ImageStoredName;
        _db.ProjectScreens.Remove(entity);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(storedName))
            DeleteImageFile(projectId, storedName);

        return NoContent();
    }

    [HttpPost("generate-layout")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> GenerateLayout(long projectId, [FromForm] GenerateLayoutRequestDto request, CancellationToken ct)
    {
        var auth = await _accessGuard.EnsureProjectPermissionAsync(projectId, PermissionKeys.ProjectView, ct);
        if (!auth.IsSuccess) return auth.ToActionResult(this);

        var requestedDocIds = new List<long>();
        if (request.DocumentId > 0) requestedDocIds.Add(request.DocumentId);
        if (request.DocumentIds != null) requestedDocIds.AddRange(request.DocumentIds.Where(id => id > 0));
        requestedDocIds.AddRange(ParseDocIds(request.DocumentIdsJson));
        var docIds = requestedDocIds.Distinct().ToList();

        if (docIds.Count == 0)
            return BadRequest(new ErrorResponseDto("VALIDATION_ERROR", "Vui lòng chọn ít nhất một tài liệu.", null, HttpContext.TraceIdentifier));

        var docs = await _db.DocumentNodes
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && docIds.Contains(d.Id) && !d.IsDeleted)
            .ToListAsync(ct);
        if (docs.Count == 0)
            return NotFound(new ErrorResponseDto("DOCUMENT_NOT_FOUND", "Không tìm thấy tài liệu trong dự án này.", null, HttpContext.TraceIdentifier));

        // Giữ thứ tự tài liệu theo lựa chọn của người dùng
        docs = docIds
            .Select(id => docs.FirstOrDefault(d => d.Id == id))
            .Where(d => d != null)
            .Cast<DocumentNode>()
            .ToList();

        var versionIds = docs
            .Select(d => d.BaselineVersionId ?? d.CurrentVersionId)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var allReqs = versionIds.Count > 0
            ? await _db.RequirementItems
                .AsNoTracking()
                .Where(r => versionIds.Contains(r.VersionId))
                .OrderBy(r => r.Id)
                .ToListAsync(ct)
            : new List<RequirementItem>();

        var selectedUids = (request.ReqUids ?? new List<string>())
            .Concat(ParseReqUids(request.ReqUidsJson))
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pairedReqs = new List<(DocumentNode Doc, RequirementItem Req)>();
        foreach (var d in docs)
        {
            var vid = d.BaselineVersionId ?? d.CurrentVersionId;
            if (!vid.HasValue) continue;
            var docReqs = allReqs.Where(r => r.VersionId == vid.Value);
            if (selectedUids.Count > 0)
                docReqs = docReqs.Where(r => selectedUids.Contains(r.ReqUid, StringComparer.OrdinalIgnoreCase));
            foreach (var r in docReqs)
                pairedReqs.Add((d, r));
        }

        byte[]? refImageBytes = null;
        string? refImageMime = null;
        if (request.ReferenceImage != null && request.ReferenceImage.Length > 0)
        {
            if (request.ReferenceImage.Length > 20L * 1024 * 1024)
                return BadRequest(new ErrorResponseDto("IMAGE_TOO_LARGE", "Ảnh tham khảo tối đa 20 MB.", null, HttpContext.TraceIdentifier));
            using var ms = new MemoryStream();
            await request.ReferenceImage.CopyToAsync(ms, ct);
            refImageBytes = ms.ToArray();
            refImageMime = string.IsNullOrWhiteSpace(request.ReferenceImage.ContentType) ? "image/png" : request.ReferenceImage.ContentType;
        }

        var hasRefImage = refImageBytes != null && refImageBytes.Length > 0;
        var isFlow = request.GenerateFlow == true || docs.Count > 1;
        var maxScreens = Math.Clamp(request.MaxScreens ?? 4, 2, 6);
        var userId = _currentUser.UserIdOrNull!.Value;
        var now = DateTime.UtcNow;
        var dir = Path.Combine(_uploadRoot, "screens", projectId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);

        if (isFlow)
        {
            var prompt = LayoutSvgGenerator.BuildFlowPrompt(docs, pairedReqs, request.Title, request.Notes, hasRefImage, maxScreens);
            List<LayoutSvgGenerator.FlowScreenBlueprint>? flowBps = null;
            var usedAi = false;
            try
            {
                var raw = hasRefImage
                    ? await _aiService.GenerateTextWithImageAsync(prompt, refImageBytes, refImageMime, ct)
                    : await _aiService.GenerateTextAsync(prompt, ct);
                flowBps = LayoutSvgGenerator.TryParseFlowBlueprints(raw, maxScreens);
                if (flowBps != null && flowBps.Count > 0) usedAi = true;
                else _logger.LogWarning("AI returned unparseable flow layout JSON for project {ProjectId}. Raw length: {Len}", projectId, raw?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI flow layout generation failed for project {ProjectId}, using structured fallback.", projectId);
            }

            flowBps ??= LayoutSvgGenerator.BuildFlowFallbackBlueprints(docs, pairedReqs, request.Title, request.Notes, maxScreens);

            var primaryDoc = docs[0];
            var createdEntities = new List<(ProjectScreen Entity, string Svg, int StepOrder, string? WorkflowStep)>();

            for (var idx = 0; idx < flowBps.Count; idx++)
            {
                var stepBp = flowBps[idx];
                var stepOrder = stepBp.StepOrder > 0 ? stepBp.StepOrder : idx + 1;
                var rawTitle = string.IsNullOrWhiteSpace(stepBp.ScreenTitle)
                    ? $"{primaryDoc.Title} · Step {stepOrder}"
                    : stepBp.ScreenTitle.Trim();
                var screenTitle = $"{stepOrder}. {rawTitle}";
                if (screenTitle.Length > 200) screenTitle = screenTitle[..200];

                var svg = LayoutSvgGenerator.RenderSvg(stepBp.ToUiBlueprint());
                var safeSlug = string.Concat(screenTitle.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')).Trim('_');
                if (string.IsNullOrWhiteSpace(safeSlug)) safeSlug = $"layout_{stepOrder}";
                if (safeSlug.Length > 60) safeSlug = safeSlug[..60];
                var originalFileName = $"{safeSlug}.svg";
                var storedName = $"{Guid.NewGuid():N}_{originalFileName}";
                await System.IO.File.WriteAllTextAsync(Path.Combine(dir, storedName), svg, Encoding.UTF8, ct);

                var stepReqUids = stepBp.ReqUids
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Select(u => u.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var matchedPair = stepReqUids.Count > 0
                    ? pairedReqs.FirstOrDefault(p => stepReqUids.Contains(p.Req.ReqUid, StringComparer.OrdinalIgnoreCase))
                    : default;
                var linkedDocId = matchedPair.Doc?.Id ?? primaryDoc.Id;
                var notePrefix = string.IsNullOrWhiteSpace(stepBp.WorkflowStep) ? $"Step {stepOrder}" : stepBp.WorkflowStep.Trim();
                var combinedNotes = string.IsNullOrWhiteSpace(request.Notes)
                    ? $"{notePrefix} · {(usedAi ? "AI workflow layout (SVG)" : "Auto-generated workflow layout (SVG)")}"
                    : $"{notePrefix} · {request.Notes.Trim()}";

                var entity = new ProjectScreen
                {
                    ProjectId = projectId,
                    Title = screenTitle,
                    Kind = "LAYOUT",
                    DocumentId = linkedDocId,
                    ReqUidsJson = stepReqUids.Count > 0 ? JsonSerializer.Serialize(stepReqUids) : (selectedUids.Count > 0 ? JsonSerializer.Serialize(selectedUids) : null),
                    Notes = combinedNotes,
                    ImageFileName = originalFileName,
                    ImageStoredName = storedName,
                    ImageContentType = "image/svg+xml",
                    CreatedById = userId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _db.ProjectScreens.Add(entity);
                createdEntities.Add((entity, svg, stepOrder, stepBp.WorkflowStep));
            }

            await _db.SaveChangesAsync(ct);

            var ids = createdEntities.Select(x => x.Entity.Id).ToList();
            var loadedList = await _db.ProjectScreens
                .AsNoTracking()
                .Include(s => s.Document)
                .Include(s => s.CreatedBy)
                .Where(s => ids.Contains(s.Id))
                .ToListAsync(ct);

            var itemsDto = createdEntities.Select(x =>
            {
                var loaded = loadedList.First(s => s.Id == x.Entity.Id);
                return new GenerateLayoutItemDto
                {
                    Screen = MapDto(loaded),
                    SvgContent = x.Svg,
                    UsedAi = usedAi,
                    StepOrder = x.StepOrder,
                    WorkflowStep = x.WorkflowStep
                };
            }).ToList();

            var first = itemsDto[0];
            return Ok(new GenerateLayoutResponseDto
            {
                Screen = first.Screen,
                SvgContent = first.SvgContent,
                UsedAi = usedAi,
                Items = itemsDto
            });
        }

        // Single-screen generation (backward compatible)
        var doc = docs[0];
        var targetReqs = pairedReqs.Select(p => p.Req).ToList();
        var layoutTitle = !string.IsNullOrWhiteSpace(request.Title)
            ? request.Title.Trim()
            : $"{doc.Title} · Layout";
        if (layoutTitle.Length > 200) layoutTitle = layoutTitle[..200];

        var singlePrompt = LayoutSvgGenerator.BuildPrompt(layoutTitle, doc, targetReqs, request.Notes, hasRefImage);
        LayoutSvgGenerator.UiBlueprint? blueprint = null;
        var singleUsedAi = false;
        try
        {
            var raw = hasRefImage
                ? await _aiService.GenerateTextWithImageAsync(singlePrompt, refImageBytes, refImageMime, ct)
                : await _aiService.GenerateTextAsync(singlePrompt, ct);
            blueprint = LayoutSvgGenerator.TryParseBlueprint(raw);
            if (blueprint != null) singleUsedAi = true;
            else _logger.LogWarning("AI returned unparseable layout JSON for document {DocumentId}. Raw length: {Len}", doc.Id, raw?.Length ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI layout generation failed for document {DocumentId}, using structured fallback.", doc.Id);
        }

        blueprint ??= LayoutSvgGenerator.BuildFallbackBlueprint(layoutTitle, doc, targetReqs, request.Notes);
        var singleSvg = LayoutSvgGenerator.RenderSvg(blueprint);

        var singleSlug = string.Concat(layoutTitle.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')).Trim('_');
        if (string.IsNullOrWhiteSpace(singleSlug)) singleSlug = "layout";
        if (singleSlug.Length > 60) singleSlug = singleSlug[..60];
        var singleFileName = $"{singleSlug}.svg";
        var singleStoredName = $"{Guid.NewGuid():N}_{singleFileName}";
        await System.IO.File.WriteAllTextAsync(Path.Combine(dir, singleStoredName), singleSvg, Encoding.UTF8, ct);

        var singleEntity = new ProjectScreen
        {
            ProjectId = projectId,
            Title = layoutTitle,
            Kind = "LAYOUT",
            DocumentId = doc.Id,
            ReqUidsJson = selectedUids.Count > 0 ? JsonSerializer.Serialize(selectedUids) : null,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? (singleUsedAi ? "AI generated wireframe (SVG)" : "Auto-generated wireframe (SVG)")
                : request.Notes.Trim(),
            ImageFileName = singleFileName,
            ImageStoredName = singleStoredName,
            ImageContentType = "image/svg+xml",
            CreatedById = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ProjectScreens.Add(singleEntity);
        await _db.SaveChangesAsync(ct);

        var singleLoaded = await _db.ProjectScreens
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.CreatedBy)
            .FirstAsync(s => s.Id == singleEntity.Id, ct);

        var singleDto = MapDto(singleLoaded);
        return Ok(new GenerateLayoutResponseDto
        {
            Screen = singleDto,
            SvgContent = singleSvg,
            UsedAi = singleUsedAi,
            Items = new List<GenerateLayoutItemDto>
            {
                new()
                {
                    Screen = singleDto,
                    SvgContent = singleSvg,
                    UsedAi = singleUsedAi,
                    StepOrder = 1
                }
            }
        });
    }

    private async Task<(string FileName, string StoredName, string ContentType)> SaveImageAsync(long projectId, IFormFile file, CancellationToken ct)
    {
        var originalName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalName)) originalName = "screen.png";
        if (originalName.Length > 200) originalName = originalName[^200..];

        var safeStoredName = $"{Guid.NewGuid():N}_{string.Concat(originalName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch))}";
        var dir = Path.Combine(_uploadRoot, "screens", projectId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        var fullPath = Path.Combine(dir, safeStoredName);

        await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, ct);

        var ctType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/png" : file.ContentType;
        return (originalName, safeStoredName, ctType);
    }

    private void DeleteImageFile(long projectId, string storedName)
    {
        var fullPath = Path.Combine(_uploadRoot, "screens", projectId.ToString(CultureInfo.InvariantCulture), storedName);
        if (System.IO.File.Exists(fullPath))
        {
            try { System.IO.File.Delete(fullPath); } catch { /* ignore */ }
        }
    }

    private static ProjectScreenDto MapDto(ProjectScreen s) => new()
    {
        ScreenId = s.Id,
        ProjectId = s.ProjectId,
        Title = s.Title,
        Kind = s.Kind,
        FigmaUrl = s.FigmaUrl,
        HasImage = !string.IsNullOrEmpty(s.ImageStoredName),
        ImageFileName = s.ImageFileName,
        DocumentId = s.DocumentId,
        DocumentTitle = s.Document?.Title,
        ReqUids = ParseReqUids(s.ReqUidsJson),
        Notes = s.Notes,
        CreatedBy = ServiceMappingExtensions.MapUserRef(s.CreatedBy),
        CreatedAt = s.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        UpdatedAt = s.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)
    };

    private static string? NormalizeReqUidsJson(string? raw)
    {
        var list = ParseReqUids(raw);
        return list.Count == 0 ? null : JsonSerializer.Serialize(list);
    }

    private static List<long> ParseDocIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<long>();
        try
        {
            return (JsonSerializer.Deserialize<List<long>>(raw) ?? new List<long>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }
        catch
        {
            return new List<long>();
        }
    }

    private static List<string> ParseReqUids(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(raw);
            return items?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
