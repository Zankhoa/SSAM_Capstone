namespace SSAM_Server.Interfaces.Services;

public interface IAiService
{
    Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamTextAsync(string prompt, CancellationToken cancellationToken = default);
}