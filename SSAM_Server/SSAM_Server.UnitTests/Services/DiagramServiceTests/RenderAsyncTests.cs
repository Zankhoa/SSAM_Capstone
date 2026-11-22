using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SSAM_Server.DTOs.Diagrams;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.DiagramServiceTests;

public class RenderAsyncTests : DiagramServiceTestBase
{
    private const string ValidCode = "@startuml\nA -> B : hello\n@enduml";

    private void SetupAuthorizedUser(long userId = 1)
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(userId);
        PermissionServiceMock
            .Setup(p => p.HasAnyProjectPermissionAsync(userId, PermissionKeys.DocumentView, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupSvgResponse(string svg, HttpStatusCode status = HttpStatusCode.OK)
    {
        Handler.Responder = _ => new HttpResponseMessage(status) { Content = new StringContent(svg, Encoding.UTF8) };
    }

    private static string CacheKeyOf(string code)
        => $"plantuml:svg:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)))}";

    private static RenderDiagramRequest Req(string code = ValidCode, string kind = "plantuml")
        => new() { Kind = kind, Code = code };

    // =========================================================================
    // 1. NORMAL CASES
    // =========================================================================

    // UTC001: Render valid PlantUML code successfully -> 200 with SVG, POSTs code to {ServerUrl}/svg
    [Fact]
    public async Task RenderAsync_WithValidPlantUmlCode_ReturnsOkWithSvg()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>", result.Data.Svg);
        Assert.Null(result.ErrorCode);

        Assert.Equal(1, Handler.CallCount);
        Assert.Equal(HttpMethod.Post, Handler.LastMethod);
        Assert.Equal("http://plantuml.local:8080/svg", Handler.LastUri!.ToString());
        Assert.Equal(ValidCode, Handler.LastBody);
        Assert.Equal("text/plain", Handler.LastContentType);
        HttpClientFactoryMock.Verify(f => f.CreateClient("PlantUml"), Times.Once);
        PermissionServiceMock.Verify(p => p.HasAnyProjectPermissionAsync(1, PermissionKeys.DocumentView, It.IsAny<CancellationToken>()), Times.Once);
    }

    // UTC002: Kind is matched case-insensitively and trimmed (" PlantUML ") -> 200
    [Fact]
    public async Task RenderAsync_WithKindDifferentCaseAndWhitespace_ReturnsOk()
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req(kind: "  PlantUML  "), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("<svg/>", result.Data!.Svg);
        Assert.Equal(1, Handler.CallCount);
    }

    // UTC003: Trailing slashes of ServerUrl are trimmed before appending /svg
    [Fact]
    public async Task RenderAsync_WithServerUrlTrailingSlash_PostsToNormalizedEndpoint()
    {
        SetupAuthorizedUser();
        Options.ServerUrl = "http://plantuml.local:8080///";

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("http://plantuml.local:8080/svg", Handler.LastUri!.ToString());
    }

    // UTC004: Second render of identical code is served from cache -> HTTP called only once
    [Fact]
    public async Task RenderAsync_WhenSameCodeRenderedTwice_SecondCallServedFromCache()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("<svg>first</svg>");

        var first = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);
        SetupSvgResponse("<svg>second</svg>"); // would be returned if cache were not used
        var second = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("<svg>first</svg>", first.Data!.Svg);
        Assert.Equal("<svg>first</svg>", second.Data!.Svg);
        Assert.Equal(1, Handler.CallCount);
        HttpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Once);
    }

    // UTC005: Successful render stores the sanitized SVG in cache under plantuml:svg:<sha256>
    [Fact]
    public async Task RenderAsync_OnSuccess_StoresSvgInCacheWithHashKey()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("<svg>cached</svg>");

        await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(Cache.TryGetValue<string>(CacheKeyOf(ValidCode), out var cached));
        Assert.Equal("<svg>cached</svg>", cached);
    }

    // UTC006: Pre-seeded cache entry is returned without any HTTP call or HttpClient creation
    [Fact]
    public async Task RenderAsync_WhenCacheContainsEntry_ReturnsCachedSvgWithoutHttpCall()
    {
        SetupAuthorizedUser();
        Cache.Set(CacheKeyOf(ValidCode), "<svg>from-cache</svg>");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("<svg>from-cache</svg>", result.Data!.Svg);
        Assert.Equal(0, Handler.CallCount);
        HttpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    // UTC007: Different code produces a different cache entry -> two HTTP calls
    [Fact]
    public async Task RenderAsync_WithDifferentCode_DoesNotReuseCache()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("<svg>one</svg>");
        await DiagramService.RenderAsync(Req("@startuml\nA -> B\n@enduml"), TestContext.Current.CancellationToken);
        SetupSvgResponse("<svg>two</svg>");

        var second = await DiagramService.RenderAsync(Req("@startuml\nA -> C\n@enduml"), TestContext.Current.CancellationToken);

        Assert.Equal("<svg>two</svg>", second.Data!.Svg);
        Assert.Equal(2, Handler.CallCount);
        Assert.True(Cache.TryGetValue<string>(CacheKeyOf("@startuml\nA -> B\n@enduml"), out _));
        Assert.True(Cache.TryGetValue<string>(CacheKeyOf("@startuml\nA -> C\n@enduml"), out _));
    }

    // UTC008: Empty-string cache entry is treated as a miss -> HTTP call is made
    [Fact]
    public async Task RenderAsync_WhenCachedValueIsEmpty_FallsThroughToHttp()
    {
        SetupAuthorizedUser();
        Cache.Set(CacheKeyOf(ValidCode), string.Empty);
        SetupSvgResponse("<svg>fresh</svg>");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("<svg>fresh</svg>", result.Data!.Svg);
        Assert.Equal(1, Handler.CallCount);
    }

    // UTC009: <script> blocks are stripped from the returned SVG (output sanitization)
    [Fact]
    public async Task RenderAsync_WhenSvgContainsScriptTag_StripsScript()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("<svg><script>alert(1)</script><rect/></svg>");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("<svg><rect/></svg>", result.Data!.Svg);
    }

    // UTC010: Multi-line, upper-case <SCRIPT ...> blocks are stripped as well
    [Fact]
    public async Task RenderAsync_WhenSvgContainsMultilineUppercaseScript_StripsScript()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("<svg><SCRIPT type=\"text/javascript\">\nvar a = 1;\n</SCRIPT><circle/></svg>");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal("<svg><circle/></svg>", result.Data!.Svg);
    }

    // UTC011: Inline event handler attributes (double, single, unquoted) are removed
    [Fact]
    public async Task RenderAsync_WhenSvgContainsEventAttributes_RemovesThem()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("<svg><rect onclick=\"x()\" onload='y()' onmouseover=z /></svg>");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("onclick", result.Data!.Svg);
        Assert.DoesNotContain("onload", result.Data.Svg);
        Assert.DoesNotContain("onmouseover", result.Data.Svg);
        Assert.StartsWith("<svg><rect", result.Data.Svg);
    }

    // UTC012: javascript: URIs are removed from the returned SVG
    [Fact]
    public async Task RenderAsync_WhenSvgContainsJavascriptUri_RemovesScheme()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("<svg><a href=\"JavaScript : alert(1)\"><text>x</text></a></svg>");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("javascript", result.Data!.Svg, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("<svg><a href=\" alert(1)\"><text>x</text></a></svg>", result.Data.Svg);
    }

    // UTC013: A clean SVG passes through the sanitizer unchanged
    [Fact]
    public async Task RenderAsync_WhenSvgIsClean_ReturnsSvgUnchanged()
    {
        SetupAuthorizedUser();
        const string svg = "<svg width=\"10\" height=\"10\"><g id=\"one\"><text>online</text></g></svg>";
        SetupSvgResponse(svg);

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(svg, result.Data!.Svg);
    }

    // UTC014: Timeout is taken from options (5s)
    [Fact]
    public async Task RenderAsync_WithTimeoutSecondsInRange_AppliesConfiguredTimeout()
    {
        SetupAuthorizedUser();
        Options.TimeoutSeconds = 5;

        await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.NotNull(LastClient);
        Assert.Equal(TimeSpan.FromSeconds(5), LastClient.Timeout);
    }

    // =========================================================================
    // 2. ABNORMAL CASES
    // =========================================================================

    // UTC015: Unauthenticated user -> 401 UNAUTHORIZED, nothing else is touched
    [Fact]
    public async Task RenderAsync_WhenUserIsNotAuthenticated_ReturnsUnauthorized()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns((long?)null);

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Equal("Your session is invalid or has expired.", result.ErrorMessage);
        Assert.Null(result.Data);
        PermissionServiceMock.Verify(p => p.HasAnyProjectPermissionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(0, Handler.CallCount);
    }

    // UTC016: User lacks document.view on every project -> 403 FORBIDDEN with required permission
    [Fact]
    public async Task RenderAsync_WhenUserLacksDocumentView_ReturnsForbidden()
    {
        CurrentUserMock.Setup(u => u.UserIdOrNull).Returns(1L);
        PermissionServiceMock
            .Setup(p => p.HasAnyProjectPermissionAsync(1, PermissionKeys.DocumentView, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(PermissionKeys.DocumentView, result.RequiredPermission);
        Assert.Equal(0, Handler.CallCount);
        HttpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    // UTC017: Unsupported kind ("mermaid") -> 400 VALIDATION_ERROR on "kind"
    [Fact]
    public async Task RenderAsync_WithUnsupportedKind_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req(kind: "mermaid"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.NotNull(result.Errors);
        Assert.True(result.Errors.ContainsKey("kind"));
        Assert.Single(result.Errors["kind"]);
        Assert.Equal(0, Handler.CallCount);
    }

    // UTC018: Empty kind -> 400 validation on "kind"
    [Fact]
    public async Task RenderAsync_WithEmptyKind_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req(kind: ""), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("kind"));
    }

    // UTC019: Null kind -> 400 validation on "kind"
    [Fact]
    public async Task RenderAsync_WithNullKind_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        var request = new RenderDiagramRequest { Kind = null!, Code = ValidCode };

        var result = await DiagramService.RenderAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("kind"));
    }

    // UTC020: Empty code -> 400 validation on "code"
    [Fact]
    public async Task RenderAsync_WithEmptyCode_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req(code: ""), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.True(result.Errors!.ContainsKey("code"));
        Assert.Equal(0, Handler.CallCount);
    }

    // UTC021: Whitespace-only code -> 400 validation on "code"
    [Fact]
    public async Task RenderAsync_WithWhitespaceCode_ReturnsValidationError()
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req(code: " \t\r\n "), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("code"));
    }

    // UTC022: Null code -> 400 validation on "code"
    [Fact]
    public async Task RenderAsync_WithNullCode_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        var request = new RenderDiagramRequest { Kind = "plantuml", Code = null! };

        var result = await DiagramService.RenderAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(result.Errors!.ContainsKey("code"));
    }

    // UTC023: Code longer than MaxCodeLength -> 400 validation mentioning the limit
    [Fact]
    public async Task RenderAsync_WithCodeExceedingMaxLength_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        Options.MaxCodeLength = 100;

        var result = await DiagramService.RenderAsync(Req(new string('a', 101)), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Contains("100", result.Errors!["code"][0]);
        Assert.Equal(0, Handler.CallCount);
    }

    // UTC024–UTC031: Each forbidden directive variant returns 400 FORBIDDEN_DIRECTIVE.
    [Theory]
    [InlineData("@startuml\n!include http://evil/x.puml\n@enduml")]            // UTC024: !include
    [InlineData("@startuml\n!includeurl http://evil/x.puml\n@enduml")]          // UTC025: !includeurl
    [InlineData("@startuml\n!includesub file.puml!SUB\n@enduml")]               // UTC026: !includesub
    [InlineData("@startuml\n!import lib.zip\n@enduml")]                         // UTC027: !import
    [InlineData("@startuml\n!theme sketchy from http://evil\n@enduml")]         // UTC028: !theme ... from
    [InlineData("@startuml\nAlice -> Bob : %getenv(\"HOME\")\n@enduml")]        // UTC029: %getenv(
    [InlineData("@startuml\n%load_json(\"x.json\")\n@enduml")]                  // UTC030: %load_json(
    [InlineData("@startuml\n!INCLUDE x.puml\n@enduml")]                         // UTC031: upper-case directive
    public async Task RenderAsync_WithForbiddenDirective_ReturnsBadRequest(string code)
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req(code), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("FORBIDDEN_DIRECTIVE", result.ErrorCode);
        Assert.Equal(0, Handler.CallCount);
        Assert.False(Cache.TryGetValue<string>(CacheKeyOf(code), out _));
    }

    // UTC032: ServerUrl not configured (null) -> 501 PLANTUML_NOT_CONFIGURED
    [Fact]
    public async Task RenderAsync_WhenServerUrlIsNull_ReturnsNotImplemented()
    {
        SetupAuthorizedUser();
        Options.ServerUrl = null;

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status501NotImplemented, result.StatusCode);
        Assert.Equal("PLANTUML_NOT_CONFIGURED", result.ErrorCode);
        Assert.Equal(0, Handler.CallCount);
        HttpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    // UTC033: ServerUrl whitespace -> 501 PLANTUML_NOT_CONFIGURED
    [Fact]
    public async Task RenderAsync_WhenServerUrlIsWhitespace_ReturnsNotImplemented()
    {
        SetupAuthorizedUser();
        Options.ServerUrl = "   ";

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status501NotImplemented, result.StatusCode);
        Assert.Equal("PLANTUML_NOT_CONFIGURED", result.ErrorCode);
    }

    // UTC034: Not-configured check precedes cache lookup -> cached entry is not served
    [Fact]
    public async Task RenderAsync_WhenNotConfiguredAndCacheHasEntry_StillReturnsNotImplemented()
    {
        SetupAuthorizedUser();
        Options.ServerUrl = "";
        Cache.Set(CacheKeyOf(ValidCode), "<svg/>");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status501NotImplemented, result.StatusCode);
    }

    // UTC035: PlantUML server returns 400 with X-PlantUML-Diagram-Error header -> 400 validation using header text
    [Fact]
    public async Task RenderAsync_WhenServerReturnsBadRequestWithErrorHeader_ReturnsValidationErrorWithDetail()
    {
        SetupAuthorizedUser();
        Handler.Responder = _ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad") };
            resp.Headers.Add("X-PlantUML-Diagram-Error", "Syntax Error?");
            resp.Headers.Add("X-PlantUML-Diagram-Error", "line 3");
            return resp;
        };

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Syntax Error? line 3", result.ErrorMessage);
        Assert.Equal(new[] { "Syntax Error? line 3" }, result.Errors!["code"]);
        Assert.False(Cache.TryGetValue<string>(CacheKeyOf(ValidCode), out _));
    }

    // UTC036: PlantUML server returns 400 without header -> default syntax message
    [Fact]
    public async Task RenderAsync_WhenServerReturnsBadRequestWithoutHeader_ReturnsDefaultSyntaxMessage()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("bad", HttpStatusCode.BadRequest);

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Cú pháp PlantUML không hợp lệ.", result.ErrorMessage);
        Assert.Equal(new[] { "Cú pháp PlantUML không hợp lệ." }, result.Errors!["code"]);
    }

    // UTC037: Server returns 500 -> 502 PLANTUML_ERROR, not cached
    [Fact]
    public async Task RenderAsync_WhenServerReturnsInternalError_ReturnsBadGateway()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("boom", HttpStatusCode.InternalServerError);

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal("PLANTUML_ERROR", result.ErrorCode);
        Assert.False(Cache.TryGetValue<string>(CacheKeyOf(ValidCode), out _));
    }

    // UTC038: Server returns 404 (non-400 error) -> 502 PLANTUML_ERROR
    [Fact]
    public async Task RenderAsync_WhenServerReturnsNotFound_ReturnsBadGateway()
    {
        SetupAuthorizedUser();
        SetupSvgResponse("nope", HttpStatusCode.NotFound);

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal("PLANTUML_ERROR", result.ErrorCode);
    }

    // UTC039: HttpRequestException (server down) -> 502 PLANTUML_UNAVAILABLE
    [Fact]
    public async Task RenderAsync_WhenHttpRequestExceptionThrown_ReturnsBadGatewayUnavailable()
    {
        SetupAuthorizedUser();
        Handler.Responder = _ => throw new HttpRequestException("connection refused");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal("PLANTUML_UNAVAILABLE", result.ErrorCode);
        Assert.False(Cache.TryGetValue<string>(CacheKeyOf(ValidCode), out _));
    }

    // UTC040: Timeout (TaskCanceledException) -> 502 PLANTUML_UNAVAILABLE
    [Fact]
    public async Task RenderAsync_WhenRequestTimesOut_ReturnsBadGatewayUnavailable()
    {
        SetupAuthorizedUser();
        Handler.Responder = _ => throw new TaskCanceledException("timeout");

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal("PLANTUML_UNAVAILABLE", result.ErrorCode);
    }

    // UTC041: Unexpected exception type is not swallowed -> propagates to caller
    [Fact]
    public async Task RenderAsync_WhenUnexpectedExceptionThrown_Propagates()
    {
        SetupAuthorizedUser();
        Handler.Responder = _ => throw new InvalidOperationException("unexpected");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken));
    }

    // UTC042: Content-Length header larger than MaxSvgBytes -> 413 SVG_TOO_LARGE, not cached
    [Fact]
    public async Task RenderAsync_WhenContentLengthExceedsMaxSvgBytes_ReturnsPayloadTooLarge()
    {
        SetupAuthorizedUser();
        Options.MaxSvgBytes = 10;
        SetupSvgResponse(new string('x', 11));

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, result.StatusCode);
        Assert.Equal("SVG_TOO_LARGE", result.ErrorCode);
        Assert.False(Cache.TryGetValue<string>(CacheKeyOf(ValidCode), out _));
    }

    // UTC043: Unknown Content-Length but body larger than MaxSvgBytes -> 413 SVG_TOO_LARGE
    [Fact]
    public async Task RenderAsync_WhenStreamedBodyExceedsMaxSvgBytes_ReturnsPayloadTooLarge()
    {
        SetupAuthorizedUser();
        Options.MaxSvgBytes = 10;
        Handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(Encoding.UTF8.GetBytes(new string('x', 11)))
        };

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, result.StatusCode);
        Assert.Equal("SVG_TOO_LARGE", result.ErrorCode);
    }

    // =========================================================================
    // 3. BOUNDARY CASES
    // =========================================================================

    // UTC044: Code length == MaxCodeLength -> accepted
    [Fact]
    public async Task RenderAsync_WithCodeLengthEqualToMax_ReturnsOk()
    {
        SetupAuthorizedUser();
        Options.MaxCodeLength = 100;

        var result = await DiagramService.RenderAsync(Req(new string('a', 100)), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Handler.CallCount);
    }

    // UTC045: Code length == MaxCodeLength + 1 -> rejected
    [Fact]
    public async Task RenderAsync_WithCodeLengthOneOverMax_ReturnsValidationError()
    {
        SetupAuthorizedUser();
        Options.MaxCodeLength = 100;

        var result = await DiagramService.RenderAsync(Req(new string('a', 101)), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Errors!.ContainsKey("code"));
        Assert.Equal(0, Handler.CallCount);
    }

    // UTC046: Code of a single character (min non-blank) -> accepted
    [Fact]
    public async Task RenderAsync_WithSingleCharacterCode_ReturnsOk()
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req("a"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("a", Handler.LastBody);
    }

    // UTC047: SVG size == MaxSvgBytes -> accepted
    [Fact]
    public async Task RenderAsync_WithSvgSizeEqualToMax_ReturnsOk()
    {
        SetupAuthorizedUser();
        Options.MaxSvgBytes = 10;
        SetupSvgResponse(new string('x', 10));

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('x', 10), result.Data!.Svg);
    }

    // UTC048: SVG size == MaxSvgBytes + 1 -> 413
    [Fact]
    public async Task RenderAsync_WithSvgSizeOneOverMax_ReturnsPayloadTooLarge()
    {
        SetupAuthorizedUser();
        Options.MaxSvgBytes = 10;
        SetupSvgResponse(new string('x', 11));

        var result = await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, result.StatusCode);
    }

    // UTC049: TimeoutSeconds below minimum (0) is clamped to 1 second
    [Fact]
    public async Task RenderAsync_WithTimeoutBelowMinimum_ClampsToOneSecond()
    {
        SetupAuthorizedUser();
        Options.TimeoutSeconds = 0;

        await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromSeconds(1), LastClient!.Timeout);
    }

    // UTC050: Negative TimeoutSeconds is clamped to 1 second
    [Fact]
    public async Task RenderAsync_WithNegativeTimeout_ClampsToOneSecond()
    {
        SetupAuthorizedUser();
        Options.TimeoutSeconds = -5;

        await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromSeconds(1), LastClient!.Timeout);
    }

    // UTC051: TimeoutSeconds == 30 (max) is kept
    [Fact]
    public async Task RenderAsync_WithTimeoutAtMaximum_KeepsThirtySeconds()
    {
        SetupAuthorizedUser();
        Options.TimeoutSeconds = 30;

        await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromSeconds(30), LastClient!.Timeout);
    }

    // UTC052: TimeoutSeconds above maximum (31) is clamped to 30 seconds
    [Fact]
    public async Task RenderAsync_WithTimeoutAboveMaximum_ClampsToThirtySeconds()
    {
        SetupAuthorizedUser();
        Options.TimeoutSeconds = 31;

        await DiagramService.RenderAsync(Req(), TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromSeconds(30), LastClient!.Timeout);
    }

    // UTC053: Directive-like plain text ("include and import" without "!") is allowed
    [Fact]
    public async Task RenderAsync_WithDirectiveLikeButAllowedText_ReturnsOk()
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req("@startuml\nA -> B : include and import\n@enduml"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Handler.CallCount);
    }

    // UTC054: !theme without "from" is allowed
    [Fact]
    public async Task RenderAsync_WithThemeDirectiveWithoutFrom_ReturnsOk()
    {
        SetupAuthorizedUser();

        var result = await DiagramService.RenderAsync(Req("@startuml\n!theme sketchy\nA -> B\n@enduml"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Handler.CallCount);
    }
}
