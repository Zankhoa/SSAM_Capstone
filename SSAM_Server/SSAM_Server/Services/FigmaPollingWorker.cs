using Microsoft.Extensions.Options;
using SSAM_Server.Common.Options;
using SSAM_Server.Interfaces.Services;

namespace SSAM_Server.Services;

public class FigmaPollingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<FigmaOptions> _optionsMonitor;
    private readonly ILogger<FigmaPollingWorker> _logger;

    public FigmaPollingWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<FigmaOptions> optionsMonitor,
        ILogger<FigmaPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _optionsMonitor.CurrentValue;
            var intervalSeconds = opts.PollingIntervalSeconds <= 0 ? 300 : Math.Max(30, opts.PollingIntervalSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(opts.AccessToken) || opts.PollingIntervalSeconds <= 0)
                continue;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var watchService = scope.ServiceProvider.GetRequiredService<IFigmaWatchService>();
                var result = await watchService.CheckAllWatchedFilesAsync(stoppingToken);
                if (result.ChangedFiles.Count > 0)
                {
                    _logger.LogInformation(
                        "Figma polling detected changes in {Count} file(s), flagged {Cases} test case(s).",
                        result.ChangedFiles.Count,
                        result.FlaggedTestCases);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Figma polling cycle.");
            }
        }
    }
}
