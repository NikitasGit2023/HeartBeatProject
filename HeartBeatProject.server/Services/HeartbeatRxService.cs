using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services;
/// <summary>
/// Background service that monitors a specified folder for heartbeat files and triggers alerts if heartbeat signals are
/// missing or delayed beyond a configured threshold.
/// </summary>
/// <remarks>This service periodically checks for the presence and freshness of heartbeat files in a configured
/// directory. If no recent heartbeat is detected, it transitions to a 'down' state and sends an alert using the
/// provided alert service. The service is intended for use in environments where external systems signal their health
/// by writing files to a shared location. Thread safety is managed internally, and the service is designed to run as a
/// singleton within the application's background processing infrastructure.</remarks>
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


    //Starting the background service
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
        //Checikg whether heartbeat forlder is created
        if (!Directory.Exists(_options.FolderPath))
        {
            _logger.LogWarning("[{Time}] Heartbeat folder not found: {Folder}.", DateTime.Now, _options.FolderPath);
            await TransitionToDownAsync(
                "Heartbeat RX — Folder Missing",
                $"Folder '{_options.FolderPath}' does not exist at {DateTime.Now}.",
                cancellationToken);
            return;
        }

        //looking for the lastest file
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

        //Checking when last file is generated.
        var age = DateTime.Now - latestFile.LastWriteTime;

        _logger.LogInformation("[{Time}] Latest heartbeat: {File} ({Age:F1}s ago)",
            DateTime.Now, latestFile.Name, age.TotalSeconds);

        //// Check if the latest heartbeat file is older than the allowed threshold
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
