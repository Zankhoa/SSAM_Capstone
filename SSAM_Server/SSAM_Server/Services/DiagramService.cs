using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SSAM_Server.Common;
using SSAM_Server.Common.Options;
using SSAM_Server.DTOs.Diagrams;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Security;

namespace SSAM_Server.Services;

public class DiagramService : IDiagramService
{
    private static readonly Regex ForbiddenDirectiveRegex = new(
        @"(?:!(?:include|includeurl|includesub|import)\b|!theme\b[^\r\n]*\bfrom\b|%(?:getenv|load[a-z0-9_]*)\s*\()",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ScriptTagRegex = new(
        @"<script\b[^>]*>[\s\S]*?</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EventAttrRegex = new(
        @"\s+on[a-z]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JavascriptUriRegex = new(
        @"javascript\s*:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;
    private readonly PlantUmlOptions _options;
    private readonly ILogger<DiagramService> _logger;

    public DiagramService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IPermissionService permissionService,
        ICurrentUser currentUser,
        IOptions<PlantUmlOptions> options,
        ILogger<DiagramService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<RenderDiagramResponse>> RenderAsync(RenderDiagramRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserIdOrNull;
        if (userId == null)
        {
            return ServiceResult<RenderDiagramResponse>.Unauthorized("Your session is invalid or has expired.");
        }

        if (!await _permissionService.HasAnyProjectPermissionAsync(userId.Value, PermissionKeys.DocumentView, ct))
        {
            return ServiceResult<RenderDiagramResponse>.ForbiddenPermission(PermissionKeys.DocumentView);
        }

        var kind = request.Kind?.Trim() ?? string.Empty;
        if (!string.Equals(kind, "plantuml", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<RenderDiagramResponse>.Validation(new Dictionary<string, string[]>
            {
                ["kind"] = new[] { "Chỉ hỗ trợ kind = \"plantuml\"." }
            });
        }

        var code = request.Code ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            return ServiceResult<RenderDiagramResponse>.Validation(new Dictionary<string, string[]>
            {
                ["code"] = new[] { "Mã sơ đồ không được để trống." }
            });
        }

        if (code.Length > _options.MaxCodeLength)
        {
            return ServiceResult<RenderDiagramResponse>.Validation(new Dictionary<string, string[]>
            {
                ["code"] = new[] { $"Mã sơ đồ vượt quá giới hạn {_options.MaxCodeLength} ký tự." }
            });
        }

        if (ForbiddenDirectiveRegex.IsMatch(code))
        {
            return ServiceResult<RenderDiagramResponse>.BadRequest(
                "FORBIDDEN_DIRECTIVE",
                "Mã PlantUML chứa lệnh bị cấm (!include, !import, !theme ... from, %getenv, %load...).");
        }

        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
        {
            return ServiceResult<RenderDiagramResponse>.Failure(
                StatusCodes.Status501NotImplemented,
                "PLANTUML_NOT_CONFIGURED",
                "Chưa cấu hình máy chủ vẽ PlantUML.");
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        var cacheKey = $"plantuml:svg:{hash}";
        if (_cache.TryGetValue<string>(cacheKey, out var cachedSvg) && !string.IsNullOrEmpty(cachedSvg))
        {
            return ServiceResult<RenderDiagramResponse>.Ok(new RenderDiagramResponse { Svg = cachedSvg });
        }

        var client = _httpClientFactory.CreateClient("PlantUml");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 30));

        var endpoint = $"{_options.ServerUrl.TrimEnd('/')}/svg";
        using var content = new StringContent(code, Encoding.UTF8, "text/plain");
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(endpoint, content, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "PlantUML server request failed.");
            return ServiceResult<RenderDiagramResponse>.Failure(
                StatusCodes.Status502BadGateway,
                "PLANTUML_UNAVAILABLE",
                "Không kết nối được máy chủ vẽ PlantUML.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var detail = response.Headers.TryGetValues("X-PlantUML-Diagram-Error", out var hdr)
                    ? string.Join(" ", hdr)
                    : "Cú pháp PlantUML không hợp lệ.";
                return ServiceResult<RenderDiagramResponse>.Validation(
                    new Dictionary<string, string[]> { ["code"] = new[] { detail } },
                    detail);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ServiceResult<RenderDiagramResponse>.Failure(
                    StatusCodes.Status502BadGateway,
                    "PLANTUML_ERROR",
                    "Máy chủ PlantUML trả về lỗi.");
            }

            if (response.Content.Headers.ContentLength > _options.MaxSvgBytes)
            {
                return ServiceResult<RenderDiagramResponse>.Failure(
                    StatusCodes.Status413PayloadTooLarge,
                    "SVG_TOO_LARGE",
                    "Kích thước SVG vượt quá giới hạn cho phép.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length > _options.MaxSvgBytes)
            {
                return ServiceResult<RenderDiagramResponse>.Failure(
                    StatusCodes.Status413PayloadTooLarge,
                    "SVG_TOO_LARGE",
                    "Kích thước SVG vượt quá giới hạn cho phép.");
            }

            var svg = SanitizeSvg(Encoding.UTF8.GetString(bytes));
            _cache.Set(cacheKey, svg, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(1),
                Size = 1
            });

            return ServiceResult<RenderDiagramResponse>.Ok(new RenderDiagramResponse { Svg = svg });
        }
    }

    private static string SanitizeSvg(string svg)
    {
        var cleaned = ScriptTagRegex.Replace(svg, string.Empty);
        cleaned = EventAttrRegex.Replace(cleaned, string.Empty);
        cleaned = JavascriptUriRegex.Replace(cleaned, string.Empty);
        return cleaned;
    }
}
