using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SSAM_Server.Common;
using SSAM_Server.Common.Options;
using SSAM_Server.DTOs.Designs;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class FigmaService : IFigmaService
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private static readonly Regex FigmaPathRegex = new(
        @"^/(?:design|file|proto|board)/([A-Za-z0-9]+)(?:/|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;
    private readonly FigmaOptions _options;
    private readonly ILogger<FigmaService> _logger;

    public FigmaService(
        IHttpClientFactory httpClientFactory,
        IPermissionService permissionService,
        ICurrentUser currentUser,
        IOptions<FigmaOptions> options,
        ILogger<FigmaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<ResolveDesignResponse>> ResolveAsync(ResolveDesignRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<ResolveDesignResponse>.Unauthorized("Your session is invalid or has expired.");
        }

        if (!await _permissionService.HasAnyProjectPermissionAsync(userId.Value, PermissionKeys.DocumentView, ct))
        {
            return ServiceResult<ResolveDesignResponse>.ForbiddenPermission(PermissionKeys.DocumentView);
        }

        if (!TryParseFigmaUrl(request.Url, out var fileKey, out var nodeId))
        {
            return ServiceResult<ResolveDesignResponse>.Validation(new Dictionary<string, string[]>
            {
                ["url"] = new[] { "Link Figma không đúng định dạng (cần https://figma.com/design|file|proto|board/...?node-id=...)." }
            });
        }

        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            return ServiceResult<ResolveDesignResponse>.Failure(
                StatusCodes.Status501NotImplemented,
                "FIGMA_NOT_CONFIGURED",
                "Chưa cấu hình kết nối Figma.");
        }

        var (nodeResult, name) = await FetchNodeNameAsync(fileKey, nodeId, ct);
        if (nodeResult != null)
        {
            return nodeResult;
        }

        var thumbnailUrl = await FetchNodeImageUrlAsync(fileKey, nodeId, ct);
        return ServiceResult<ResolveDesignResponse>.Ok(new ResolveDesignResponse
        {
            Name = name!,
            ThumbnailUrl = thumbnailUrl
        });
    }

    public async Task<ResolvedFigmaImage?> FetchNodeImageForSuggestAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
            return null;

        if (!TryParseFigmaUrl(url, out var fileKey, out var nodeId))
            return null;

        try
        {
            var (nodeErr, name) = await FetchNodeNameAsync(fileKey, nodeId, ct);
            if (nodeErr != null || string.IsNullOrWhiteSpace(name))
                return null;

            var imageUrl = await FetchNodeImageUrlAsync(fileKey, nodeId, ct);
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imgUri) || imgUri.Scheme != Uri.UriSchemeHttps)
                return null;

            var client = _httpClientFactory.CreateClient("Figma");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 30));

            using var resp = await client.GetAsync(imgUri, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            if (resp.Content.Headers.ContentLength > MaxImageBytes)
                return null;

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0 || bytes.Length > MaxImageBytes)
                return null;

            var mime = DetectImageMimeType(bytes);
            if (mime == null)
                return null;

            return new ResolvedFigmaImage(name, imageUrl, bytes, mime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Figma image for suggest from URL.");
            return null;
        }
    }

    internal static bool TryParseFigmaUrl(string? rawUrl, out string fileKey, out string nodeId)
    {
        fileKey = string.Empty;
        nodeId = string.Empty;

        if (string.IsNullOrWhiteSpace(rawUrl))
            return false;

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host != "figma.com" && host != "www.figma.com")
            return false;

        var match = FigmaPathRegex.Match(uri.AbsolutePath);
        if (!match.Success)
            return false;

        fileKey = match.Groups[1].Value;

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("node-id", out var nodeVal) || string.IsNullOrWhiteSpace(nodeVal))
            return false;

        nodeId = nodeVal.ToString().Trim().Replace('-', ':');
        return !string.IsNullOrWhiteSpace(nodeId);
    }

    internal static string? DetectImageMimeType(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        return null;
    }

    private async Task<(ServiceResult<ResolveDesignResponse>? Error, string? Name)> FetchNodeNameAsync(
        string fileKey,
        string nodeId,
        CancellationToken ct)
    {
        var client = CreateApiClient();
        var endpoint = $"https://api.figma.com/v1/files/{Uri.EscapeDataString(fileKey)}/nodes?ids={Uri.EscapeDataString(nodeId)}";

        HttpResponseMessage resp;
        try
        {
            resp = await client.GetAsync(endpoint, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Figma API node request failed.");
            return (ServiceResult<ResolveDesignResponse>.Failure(
                StatusCodes.Status502BadGateway,
                "FIGMA_UNAVAILABLE",
                "Không kiểm tra được lúc này."), null);
        }

        using (resp)
        {
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                return (ServiceResult<ResolveDesignResponse>.Forbidden(
                    "FIGMA_NO_ACCESS",
                    "Không có quyền truy cập file; mời tài khoản dịch vụ hoặc tải ảnh."), null);
            }

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return (ServiceResult<ResolveDesignResponse>.NotFound(
                    "FIGMA_NODE_NOT_FOUND",
                    "Không tìm thấy khung."), null);
            }

            if ((int)resp.StatusCode == 429)
            {
                return (ServiceResult<ResolveDesignResponse>.Failure(
                    StatusCodes.Status429TooManyRequests,
                    "FIGMA_RATE_LIMITED",
                    "Figma giới hạn lượt gọi."), null);
            }

            if (!resp.IsSuccessStatusCode)
            {
                return (ServiceResult<ResolveDesignResponse>.Failure(
                    StatusCodes.Status502BadGateway,
                    "FIGMA_ERROR",
                    "Không kiểm tra được lúc này."), null);
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("nodes", out var nodesEl) ||
                !nodesEl.TryGetProperty(nodeId, out var nodeEl) ||
                nodeEl.ValueKind == JsonValueKind.Null ||
                !nodeEl.TryGetProperty("document", out var docEl) ||
                docEl.ValueKind == JsonValueKind.Null)
            {
                return (ServiceResult<ResolveDesignResponse>.NotFound(
                    "FIGMA_NODE_NOT_FOUND",
                    "Không tìm thấy khung."), null);
            }

            var fileName = doc.RootElement.TryGetProperty("name", out var fnEl) ? fnEl.GetString() : null;
            var nodeName = docEl.TryGetProperty("name", out var nnEl) ? nnEl.GetString() : null;
            var displayName = !string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(nodeName)
                ? $"{fileName} / {nodeName}"
                : nodeName ?? fileName ?? nodeId;

            return (null, displayName);
        }
    }

    private async Task<string?> FetchNodeImageUrlAsync(string fileKey, string nodeId, CancellationToken ct)
    {
        try
        {
            var client = CreateApiClient();
            var endpoint = $"https://api.figma.com/v1/images/{Uri.EscapeDataString(fileKey)}?ids={Uri.EscapeDataString(nodeId)}&format=png&scale=1";
            using var resp = await client.GetAsync(endpoint, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("images", out var imgsEl) &&
                imgsEl.TryGetProperty(nodeId, out var urlEl) &&
                urlEl.ValueKind == JsonValueKind.String)
            {
                return urlEl.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Figma thumbnail URL.");
        }

        return null;
    }

    private HttpClient CreateApiClient()
    {
        var client = _httpClientFactory.CreateClient("Figma");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 30));
        client.DefaultRequestHeaders.Remove("X-Figma-Token");
        client.DefaultRequestHeaders.Add("X-Figma-Token", _options.AccessToken);
        return client;
    }
}
