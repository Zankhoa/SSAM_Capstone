using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SSAM_Server.Common.Options;
using SSAM_Server.Interfaces.Security;

namespace SSAM_Server.UnitTests.Services.DiagramServiceTests;

public abstract class DiagramServiceTestBase : IDisposable
{
    protected readonly Mock<IHttpClientFactory> HttpClientFactoryMock = new();
    protected readonly Mock<IPermissionService> PermissionServiceMock = new();
    protected readonly Mock<ICurrentUser> CurrentUserMock = new();
    protected readonly MemoryCache Cache = new(new MemoryCacheOptions());
    protected readonly FakeHttpMessageHandler Handler = new();
    protected readonly PlantUmlOptions Options = new()
    {
        ServerUrl = "http://plantuml.local:8080",
        TimeoutSeconds = 10,
        MaxCodeLength = 20_000,
        MaxSvgBytes = 2_000_000
    };

    /// <summary>The HttpClient last handed out by the mocked factory (to assert Timeout).</summary>
    protected HttpClient? LastClient;

    protected readonly SSAM_Server.Services.DiagramService DiagramService;

    protected DiagramServiceTestBase()
    {
        HttpClientFactoryMock
            .Setup(f => f.CreateClient("PlantUml"))
            .Returns(() =>
            {
                LastClient = new HttpClient(Handler, disposeHandler: false);
                return LastClient;
            });

        DiagramService = new SSAM_Server.Services.DiagramService(
            HttpClientFactoryMock.Object,
            Cache,
            PermissionServiceMock.Object,
            CurrentUserMock.Object,
            Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<SSAM_Server.Services.DiagramService>.Instance);
    }

    public void Dispose()
    {
        Cache.Dispose();
        Handler.Dispose();
    }
}

/// <summary>Fake HttpMessageHandler recording requests and returning a configurable response.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<svg/>", Encoding.UTF8) };

    public int CallCount { get; private set; }
    public HttpMethod? LastMethod { get; private set; }
    public Uri? LastUri { get; private set; }
    public string? LastBody { get; private set; }
    public string? LastContentType { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastMethod = request.Method;
        LastUri = request.RequestUri;
        LastContentType = request.Content?.Headers.ContentType?.MediaType;
        LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return Responder(request);
    }
}

/// <summary>HttpContent whose length is unknown (Content-Length header stays null).</summary>
public sealed class UnknownLengthContent : HttpContent
{
    private readonly byte[] _bytes;
    public UnknownLengthContent(byte[] bytes) => _bytes = bytes;

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => stream.WriteAsync(_bytes, 0, _bytes.Length);

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}
