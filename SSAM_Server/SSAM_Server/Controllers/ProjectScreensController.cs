using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SSAM_Server.Common;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Common;
using SSAM_Server.DTOs.Projects;
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
    private readonly string _uploadRoot;

    public ProjectScreensController(
        AppDbContext db,
        ICurrentUser currentUser,
        IProjectAccessGuard accessGuard,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _accessGuard = accessGuard;
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
        entity.Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();

        await _db.SaveChangesAsync(ct);

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
        Notes = s.Notes,
        CreatedBy = ServiceMappingExtensions.MapUserRef(s.CreatedBy),
        CreatedAt = s.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        UpdatedAt = s.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)
    };
}
