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
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly HeartbeatOptions _staticOptions;
    private readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(5);
    private volatile bool _isHealthy = true;
    private DateTime _lastAlertTime = DateTime.MinValue;

    public HeartbeatRxService(
        IOptions<HeartbeatOptions> options,
        ILogger<HeartbeatRxService> logger,
        IAlertService alertService,
        RuntimeSettingsStore settingsStore)
    {
        _logger        = logger;
        _alertService  = alertService;
        _settingsStore = settingsStore;
        _staticOptions = options.Value;
    }


    //Starting the background service
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initial = _settingsStore.Get();
        _logger.LogInformation("HeartbeatRxService started. Mode: {Mode}, Folder: {Folder}, Threshold: {Threshold}s, Check interval: {Interval}s",
            _staticOptions.Mode, initial.FolderPath, initial.ThresholdSeconds, initial.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("RX: Monitoring cycle started");
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[{Time}] Unexpected error in HeartbeatRxService.", DateTime.Now);
                await TransitionToDownAsync(
                    "Heartbeat RX \u2014 Unexpected Error",
                    $"Mode: RX\nTimestamp: {DateTime.Now}\n\nUnexpected error: {ex.Message}",
                    stoppingToken);
            }

            var checkInterval = _settingsStore.Get().CheckIntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(checkInterval), stoppingToken);
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsStore.Get();

        //Checking whether heartbeat folder is created
        if (!Directory.Exists(settings.FolderPath))
        {
            _logger.LogWarning("[{Time}] Heartbeat folder not found: {Folder}.", DateTime.Now, settings.FolderPath);
            await TransitionToDownAsync(
                "Heartbeat RX \u2014 Folder Missing",
                $"Mode: RX\nFolder: {settings.FolderPath}\nTimestamp: {DateTime.Now}\n\nThe heartbeat folder does not exist.",
                cancellationToken);
            return;
        }

        //looking for the latest file
        var latestFile = Directory
            .EnumerateFiles(settings.FolderPath, "*.txt")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault();

        if (latestFile is null)
        {
            _logger.LogWarning("[{Time}] No heartbeat files found in {Folder}.", DateTime.Now, settings.FolderPath);
            await TransitionToDownAsync(
                "Heartbeat RX \u2014 No Files",
                $"Mode: RX\nFolder: {settings.FolderPath}\nTimestamp: {DateTime.Now}\n\nNo heartbeat files found in the monitored folder.",
                cancellationToken);
            return;
        }

        //Checking when last file is generated.
        var age = DateTime.Now - latestFile.LastWriteTime;

        _logger.LogInformation("[{Time}] Latest heartbeat: {File} ({Age:F1}s ago)",
            DateTime.Now, latestFile.Name, age.TotalSeconds);

        //// Check if the latest heartbeat file is older than the allowed threshold
        if (age.TotalSeconds > settings.ThresholdSeconds)
        {
            _logger.LogWarning("RX: Threshold exceeded. Last file '{File}' is {Age:F0}s old (threshold: {Threshold}s).",
                latestFile.Name, age.TotalSeconds, settings.ThresholdSeconds);
            await TransitionToDownAsync(
                "Heartbeat RX \u2014 Threshold Exceeded",
                $"Mode: RX\nFolder: {settings.FolderPath}\nLast file: {latestFile.Name}\nLast written: {latestFile.LastWriteTime}\nAge: {age.TotalSeconds:F0}s (threshold: {settings.ThresholdSeconds}s)\nTimestamp: {DateTime.Now}",
                cancellationToken);
        }
        else
        {
            _logger.LogInformation("RX: Heartbeat OK. Last file at {Time}", latestFile.LastWriteTime);
            TransitionToHealthy();
        }
    }

    private async Task TransitionToDownAsync(string subject, string message, CancellationToken cancellationToken)
    {
        if (!_isHealthy)
        {
            _logger.LogWarning("RX: Still DOWN. Waiting for recovery.");
            return;
        }

        _logger.LogWarning("RX: Status: HEALTHY \u2192 DOWN");
        _isHealthy = false;

        var now = DateTime.Now;
        var timeSinceLastAlert = now - _lastAlertTime;

        if (timeSinceLastAlert < _alertCooldown)
        {
            _logger.LogWarning("RX: Alert skipped — cooldown active. Next alert in {Remaining:F0}s.",
                (_alertCooldown - timeSinceLastAlert).TotalSeconds);
            return;
        }

        _lastAlertTime = now;
        _logger.LogInformation("RX: Sending alert email. Subject: {Subject}", subject);
        await _alertService.SendAlertAsync(subject, message, cancellationToken);
    }

    private void TransitionToHealthy()
    {
        if (!_isHealthy)
            _logger.LogInformation("[{Time}] Status: DOWN \u2192 HEALTHY", DateTime.Now);

        _isHealthy = true;
    }
}
