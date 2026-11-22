using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAM_Server.Common;
using SSAM_Server.Common.Options;
using SSAM_Server.Data;
using SSAM_Server.DTOs.Designs;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Models;

namespace SSAM_Server.Services;

public class FigmaWatchService : IFigmaWatchService
{
    private static readonly Regex FigmaPathRegex = new(
        @"^/(?:design|file|proto|board)/([A-Za-z0-9_-]+)(?:/branch/([A-Za-z0-9_-]+))?(?:/|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FigmaContentUrlRegex = new(
        @"https?://(?:www\.)?figma\.com/(?:design|file|proto|board)/([A-Za-z0-9_-]+)(?:/branch/([A-Za-z0-9_-]+))?[^\s""'<>]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FigmaOptions _options;
    private readonly TimeProvider _clock;
    private readonly INotificationService _notifications;
    private readonly ILogger<FigmaWatchService> _logger;

    public FigmaWatchService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<FigmaOptions> options,
        TimeProvider clock,
        INotificationService notifications,
        ILogger<FigmaWatchService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _clock = clock;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task RegisterFileAsync(string figmaUrlOrFileKey, string? fileName = null, CancellationToken ct = default)
    {
        if (!TryParseFigmaTarget(figmaUrlOrFileKey, out var fileKey, out _))
            return;

        var now = _clock.GetUtcNow().UtcDateTime;
        var existing = await _db.FigmaFileWatches.FirstOrDefaultAsync(x => x.FileKey == fileKey, ct);
        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                await _db.SaveChangesAsync(ct);
            }
            return;
        }

        var meta = await FetchFigmaFileMetaAsync(fileKey, ct);
        _db.FigmaFileWatches.Add(new FigmaFileWatch
        {
            FileKey = fileKey,
            FileName = !string.IsNullOrWhiteSpace(meta?.Name) ? meta.Name : (!string.IsNullOrWhiteSpace(fileName) ? fileName.Trim() : fileKey),
            LastVersion = meta?.Version,
            LastModified = meta?.LastModified,
            LastCheckedAt = meta != null ? now : null,
            IsActive = true,
            CreatedAt = now
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Ignore race condition on unique FileKey
        }
    }

    public async Task RegisterFilesFromContentAsync(string? documentContent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(documentContent))
            return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in FigmaContentUrlRegex.Matches(documentContent))
        {
            var key = !string.IsNullOrEmpty(match.Groups[2].Value)
                ? match.Groups[2].Value
                : match.Groups[1].Value;
            if (seen.Add(key))
            {
                await RegisterFileAsync(key, null, ct);
            }
        }
    }

    public async Task LinkTestCaseAsync(long testCaseId, string figmaUrlOrFileKey, CancellationToken ct = default)
    {
        if (!TryParseFigmaTarget(figmaUrlOrFileKey, out var fileKey, out var nodeId))
            return;

        var exists = await _db.TestCaseFigmaLinks
            .AsNoTracking()
            .AnyAsync(x => x.TestCaseId == testCaseId && x.FileKey == fileKey, ct);

        if (!exists)
        {
            _db.TestCaseFigmaLinks.Add(new TestCaseFigmaLink
            {
                TestCaseId = testCaseId,
                FileKey = fileKey,
                NodeId = nodeId,
                Url = figmaUrlOrFileKey.Length > 1000 ? figmaUrlOrFileKey[..1000] : figmaUrlOrFileKey,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            });
            await _db.SaveChangesAsync(ct);
        }

        await RegisterFileAsync(fileKey, null, ct);
    }

    public async Task<FigmaCheckUpdatesResponse> CheckAllWatchedFilesAsync(CancellationToken ct = default)
    {
        var response = new FigmaCheckUpdatesResponse();
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
            return response;

        var watches = await _db.FigmaFileWatches
            .Where(w => w.IsActive)
            .ToListAsync(ct);

        response.CheckedCount = watches.Count;
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var watch in watches)
        {
            var meta = await FetchFigmaFileMetaAsync(watch.FileKey, ct);
            if (meta == null)
                continue;

            watch.LastCheckedAt = now;
            if (!string.IsNullOrWhiteSpace(meta.Name))
                watch.FileName = meta.Name;

            var hadBaseline = !string.IsNullOrWhiteSpace(watch.LastVersion) || watch.LastModified.HasValue;
            var versionChanged = !string.IsNullOrWhiteSpace(meta.Version)
                && !string.Equals(watch.LastVersion, meta.Version, StringComparison.Ordinal);
            var modifiedChanged = meta.LastModified.HasValue
                && (!watch.LastModified.HasValue || meta.LastModified.Value > watch.LastModified.Value);

            if (hadBaseline && (versionChanged || modifiedChanged))
            {
                watch.LastChangedAt = now;
                watch.LastVersion = meta.Version ?? watch.LastVersion;
                watch.LastModified = meta.LastModified ?? watch.LastModified;

                var flagged = await FlagAffectedTestCasesAsync(watch.FileKey, watch.FileName, now, ct);
                response.ChangedFiles.Add(watch.FileName);
                response.FlaggedTestCases += flagged;
            }
            else if (!hadBaseline)
            {
                watch.LastVersion = meta.Version;
                watch.LastModified = meta.LastModified;
            }
        }

        await _db.SaveChangesAsync(ct);
        return response;
    }

    public async Task<ServiceResult<FigmaCheckUpdatesResponse>> HandleWebhookAsync(FigmaWebhookPayload payload, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.WebhookPasscode) &&
            !string.Equals(payload.Passcode, _options.WebhookPasscode, StringComparison.Ordinal))
        {
            return ServiceResult<FigmaCheckUpdatesResponse>.Unauthorized("Invalid Figma webhook passcode.");
        }

        var response = new FigmaCheckUpdatesResponse();
        if (!string.Equals(payload.EventType, "FILE_UPDATE", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(payload.FileKey))
        {
            return ServiceResult<FigmaCheckUpdatesResponse>.Ok(response);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var fileKey = payload.FileKey.Trim();
        var watch = await _db.FigmaFileWatches.FirstOrDefaultAsync(w => w.FileKey == fileKey, ct);
        if (watch == null)
        {
            watch = new FigmaFileWatch
            {
                FileKey = fileKey,
                FileName = string.IsNullOrWhiteSpace(payload.FileName) ? fileKey : payload.FileName.Trim(),
                IsActive = true,
                CreatedAt = now
            };
            _db.FigmaFileWatches.Add(watch);
        }

        if (!string.IsNullOrWhiteSpace(payload.FileName))
            watch.FileName = payload.FileName.Trim();

        watch.LastCheckedAt = now;
        watch.LastChangedAt = now;

        var meta = await FetchFigmaFileMetaAsync(fileKey, ct);
        if (meta != null)
        {
            if (!string.IsNullOrWhiteSpace(meta.Name)) watch.FileName = meta.Name;
            if (!string.IsNullOrWhiteSpace(meta.Version)) watch.LastVersion = meta.Version;
            if (meta.LastModified.HasValue) watch.LastModified = meta.LastModified;
        }

        var flagged = await FlagAffectedTestCasesAsync(fileKey, watch.FileName, now, ct);
        await _db.SaveChangesAsync(ct);

        response.CheckedCount = 1;
        response.ChangedFiles.Add(watch.FileName);
        response.FlaggedTestCases = flagged;
        return ServiceResult<FigmaCheckUpdatesResponse>.Ok(response);
    }

    private async Task<int> FlagAffectedTestCasesAsync(string fileKey, string? fileName, DateTime now, CancellationToken ct)
    {
        // 1. Test cases explicitly linked to this Figma file
        var directCaseIds = await _db.TestCaseFigmaLinks
            .AsNoTracking()
            .Where(l => l.FileKey == fileKey)
            .Select(l => l.TestCaseId)
            .ToListAsync(ct);

        // 2. Test cases linked to Documents whose CurrentVersion content embeds this Figma fileKey
        var docIdsWithFigma = await _db.DocumentNodes
            .AsNoTracking()
            .Where(d => !d.IsDeleted && d.CurrentVersionId != null && d.CurrentVersion!.Content.Contains(fileKey))
            .Select(d => d.Id)
            .ToListAsync(ct);

        var docLinkedCaseIds = docIdsWithFigma.Count > 0
            ? await _db.TestCaseRequirementLinks
                .AsNoTracking()
                .Where(l => docIdsWithFigma.Contains(l.DocumentId))
                .Select(l => l.TestCaseId)
                .ToListAsync(ct)
            : new List<long>();

        var affectedCaseIds = directCaseIds
            .Concat(docLinkedCaseIds)
            .Distinct()
            .ToList();

        if (affectedCaseIds.Count == 0)
            return 0;

        var activeCases = await _db.TestCases
            .AsNoTracking()
            .Include(tc => tc.Plan)
            .Where(tc => affectedCaseIds.Contains(tc.Id) && tc.Status == TestCaseStatuses.Active)
            .ToListAsync(ct);

        if (activeCases.Count == 0)
            return 0;

        var activeCaseIds = activeCases.Select(c => c.Id).ToList();
        var testedPairs = await _db.TestResults
            .AsNoTracking()
            .Where(r => activeCaseIds.Contains(r.TestCaseId) && r.Result != TestResultValues.Untest)
            .Select(r => new { r.TestCaseId, r.EnvId })
            .Distinct()
            .ToListAsync(ct);

        var envsByCase = testedPairs
            .GroupBy(p => p.TestCaseId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.EnvId).ToList());

        var existingFlags = await _db.TestRetestFlags
            .Where(f => activeCaseIds.Contains(f.TestCaseId))
            .ToListAsync(ct);
        var flagMap = existingFlags.ToDictionary(f => (f.TestCaseId, f.EnvId));

        var displayName = string.IsNullOrWhiteSpace(fileName) ? fileKey : fileName;
        var flaggedCount = 0;

        foreach (var tc in activeCases)
        {
            if (envsByCase.TryGetValue(tc.Id, out var envIds) && envIds.Count > 0)
            {
                foreach (var envId in envIds)
                {
                    if (flagMap.TryGetValue((tc.Id, envId), out var existingFlag))
                    {
                        existingFlag.Reason = TestRetestReasons.DesignChanged;
                        existingFlag.ReqKey = displayName;
                        existingFlag.BugIssueId = null;
                        existingFlag.Build = null;
                        existingFlag.At = now;
                    }
                    else
                    {
                        var newFlag = new TestRetestFlag
                        {
                            TestCaseId = tc.Id,
                            EnvId = envId,
                            Reason = TestRetestReasons.DesignChanged,
                            ReqKey = displayName,
                            BugIssueId = null,
                            Build = null,
                            At = now
                        };
                        _db.TestRetestFlags.Add(newFlag);
                        flagMap[(tc.Id, envId)] = newFlag;
                    }
                }
                flaggedCount++;
            }

            _db.TestActivities.Add(new TestActivity
            {
                PlanId = tc.PlanId,
                Type = TestActivityTypes.Req,
                At = now,
                ActorId = null,
                ModuleId = tc.ModuleId,
                TestCaseId = tc.Id,
                Text = $"Thiết kế Figma \"{displayName}\" đã thay đổi — cần kiểm tra lại {tc.Code}"
            });
        }

        var moduleIds = activeCases.Select(c => c.ModuleId).Distinct().ToList();
        var projectIds = activeCases.Select(c => c.Plan.ProjectId).Distinct().ToList();
        var assignedUserIds = await _db.TestAssignments
            .AsNoTracking()
            .Where(a => moduleIds.Contains(a.ModuleId))
            .Select(a => a.AssigneeId)
            .Distinct()
            .ToListAsync(ct);

        if (assignedUserIds.Count == 0 && projectIds.Count > 0)
        {
            assignedUserIds = await _db.ProjectMembers
                .AsNoTracking()
                .Where(pm => projectIds.Contains(pm.ProjectId) && pm.IsActive)
                .Select(pm => pm.UserId)
                .Distinct()
                .ToListAsync(ct);
        }

        var sampleCodes = string.Join(", ", activeCases.Select(c => c.Code).Distinct().Take(3));
        var firstProjectId = projectIds.FirstOrDefault();
        var link = firstProjectId > 0 ? $"/projects/{firstProjectId}/tests" : null;
        var msg = $"{activeCases.Count} test case bị ảnh hưởng ({sampleCodes}) do thiết kế Figma \"{displayName}\" thay đổi.";
        foreach (var uid in assignedUserIds)
        {
            await _notifications.NotifyAsync(
                uid,
                "TESTCASE_DESIGN_CHANGED",
                $"Cảnh báo UI/UX Figma ảnh hưởng Test Case",
                msg,
                link,
                ct);
        }

        return flaggedCount;
    }

    private async Task<FigmaFileMeta?> FetchFigmaFileMetaAsync(string fileKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("Figma");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 60));

            using var msg = new HttpRequestMessage(HttpMethod.Get, $"https://api.figma.com/v1/files/{Uri.EscapeDataString(fileKey)}?depth=1");
            msg.Headers.Add("X-Figma-Token", _options.AccessToken);

            using var resp = await client.SendAsync(msg, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Figma file meta fetch failed for {FileKey} with status {Status}", fileKey, resp.StatusCode);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var name = root.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String
                ? nEl.GetString()
                : null;
            var version = root.TryGetProperty("version", out var vEl) && vEl.ValueKind == JsonValueKind.String
                ? vEl.GetString()
                : null;
            DateTime? lastModified = null;
            if (root.TryGetProperty("lastModified", out var lmEl) && lmEl.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(lmEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                lastModified = parsed;
            }

            return new FigmaFileMeta(name, version, lastModified);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Error polling Figma file {FileKey}", fileKey);
            return null;
        }
    }

    public static bool TryParseFigmaTarget(string input, out string fileKey, out string? nodeId)
    {
        fileKey = string.Empty;
        nodeId = null;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Host.Equals("figma.com", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("www.figma.com", StringComparison.OrdinalIgnoreCase)))
        {
            var m = FigmaPathRegex.Match(uri.AbsolutePath);
            if (!m.Success)
                return false;

            fileKey = !string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[2].Value : m.Groups[1].Value;
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            if (query.TryGetValue("node-id", out var rawNodeId) && !string.IsNullOrWhiteSpace(rawNodeId.ToString()))
                nodeId = rawNodeId.ToString().Trim();
            return !string.IsNullOrWhiteSpace(fileKey);
        }

        if (Regex.IsMatch(trimmed, @"^[A-Za-z0-9_-]{8,}$"))
        {
            fileKey = trimmed;
            return true;
        }

        return false;
    }

    private sealed record FigmaFileMeta(string? Name, string? Version, DateTime? LastModified);
}
