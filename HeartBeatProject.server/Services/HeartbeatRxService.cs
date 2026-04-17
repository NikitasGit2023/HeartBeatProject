using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services;

public sealed class HeartbeatRxService : BackgroundService
{
    private readonly ILogger<HeartbeatRxService> _logger;
    private readonly IAlertService _alertService;
    private readonly HeartbeatOptions _options;
    private bool _isHealthy = true;

    public HeartbeatRxService(
        IOptions<HeartbeatOptions> options,
        ILogger<HeartbeatRxService> logger,
        IAlertService alertService)
    {
        _logger       = logger;
        _alertService = alertService;
        _options      = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatRxService started. Folder: {Folder}, Threshold: {Threshold}s",
            _options.FolderPath, _options.ThresholdSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[{Time}] Unexpected error in HeartbeatRxService.", DateTime.Now);
                await TransitionToDownAsync(
                    "Heartbeat RX — Unexpected Error",
                    $"Unexpected error at {DateTime.Now}.\n\nError: {ex.Message}",
                    stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.CheckIntervalSeconds), stoppingToken);
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        var latestFile = Directory
            .EnumerateFiles(_options.FolderPath, "*.txt")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault();

        if (latestFile is null)
        {
            _logger.LogWarning("[{Time}] No heartbeat files found in {Folder}.", DateTime.Now, _options.FolderPath);
            await TransitionToDownAsync(
                "Heartbeat RX — No Files",
                $"No heartbeat files found in '{_options.FolderPath}' at {DateTime.Now}.",
                cancellationToken);
            return;
        }

        var age = DateTime.Now - latestFile.LastWriteTime;

        _logger.LogInformation("[{Time}] Latest heartbeat: {File} ({Age:F1}s ago)",
            DateTime.Now, latestFile.Name, age.TotalSeconds);

        if (age.TotalSeconds > _options.ThresholdSeconds)
        {
            await TransitionToDownAsync(
                "Heartbeat RX — Threshold Exceeded",
                $"Last file '{latestFile.Name}' is {age.TotalSeconds:F0}s old (threshold: {_options.ThresholdSeconds}s).",
                cancellationToken);
        }
        else
        {
            TransitionToHealthy();
        }
    }

    private async Task TransitionToDownAsync(string subject, string message, CancellationToken cancellationToken)
    {
        if (!_isHealthy) return;

        _logger.LogWarning("[{Time}] Status: HEALTHY → DOWN", DateTime.Now);
        _isHealthy = false;
        await _alertService.SendAlertAsync(subject, message, cancellationToken);
    }

    private void TransitionToHealthy()
    {
        if (!_isHealthy)
            _logger.LogInformation("[{Time}] Status: DOWN → HEALTHY", DateTime.Now);

        _isHealthy = true;
    }
}
