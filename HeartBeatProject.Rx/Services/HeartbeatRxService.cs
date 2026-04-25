using HeartBeatProject.Server.Services;
using HeartBeatProject.Server.Services.Alerts;

namespace HeartBeatProject.Rx.Services;

public sealed class HeartbeatRxService : BackgroundService
{
    private readonly ILogger<HeartbeatRxService> _logger;
    private readonly IAlertService _alertService;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly RxOperationalState _rxState;
    private readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(5);
    private volatile bool _isHealthy = true;
    private DateTime _lastAlertTime = DateTime.MinValue;

    public HeartbeatRxService(
        ILogger<HeartbeatRxService> logger,
        IAlertService alertService,
        RuntimeSettingsStore settingsStore,
        RxOperationalState rxState)
    {
        _logger        = logger;
        _alertService  = alertService;
        _settingsStore = settingsStore;
        _rxState       = rxState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initial = _settingsStore.Get();
        _logger.LogInformation(
            "HeartbeatRxService started. Folder: {Folder}, Threshold: {Threshold}s, Check interval: {Interval}s",
            initial.FolderPath, initial.ThresholdSeconds, initial.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("RX: Monitoring cycle started");
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error in HeartbeatRxService.");
                _rxState.RecordDown($"Unexpected error: {ex.Message}");
                await TransitionToDownAsync(
                    "Heartbeat RX — Unexpected Error",
                    $"Mode: RX\nTimestamp: {DateTime.UtcNow:O}\n\nUnexpected error: {ex.Message}",
                    stoppingToken);
            }

            var checkInterval = _settingsStore.Get().CheckIntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(checkInterval), stoppingToken);
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsStore.Get();

        if (!Directory.Exists(settings.FolderPath))
        {
            _rxState.RecordDown("Heartbeat folder not found.");
            _logger.LogWarning("Heartbeat folder not found: {Folder}.", settings.FolderPath);
            await TransitionToDownAsync(
                "Heartbeat RX — Folder Missing",
                $"Mode: RX\nFolder: {settings.FolderPath}\nTimestamp: {DateTime.UtcNow:O}\n\nThe heartbeat folder does not exist.",
                cancellationToken);
            return;
        }

        var latestFile = Directory
            .EnumerateFiles(settings.FolderPath, "*.txt")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        if (latestFile is null)
        {
            _rxState.RecordDown("No heartbeat files found in folder.");
            _logger.LogWarning("No heartbeat files found in {Folder}.", settings.FolderPath);
            await TransitionToDownAsync(
                "Heartbeat RX — No Files",
                $"Mode: RX\nFolder: {settings.FolderPath}\nTimestamp: {DateTime.UtcNow:O}\n\nNo heartbeat files found in the monitored folder.",
                cancellationToken);
            return;
        }

        var age = DateTime.UtcNow - latestFile.LastWriteTimeUtc;
        _logger.LogInformation("RX: Latest heartbeat: {File} ({Age:F1}s ago)", latestFile.Name, age.TotalSeconds);

        if (age.TotalSeconds > settings.ThresholdSeconds)
        {
            _rxState.RecordDown(
                $"Threshold exceeded: '{latestFile.Name}' is {age.TotalSeconds:F0}s old (threshold: {settings.ThresholdSeconds}s).");
            _logger.LogWarning("RX: Threshold exceeded. Last file '{File}' is {Age:F0}s old (threshold: {Threshold}s).",
                latestFile.Name, age.TotalSeconds, settings.ThresholdSeconds);
            await TransitionToDownAsync(
                "Heartbeat RX — Threshold Exceeded",
                $"Mode: RX\nFolder: {settings.FolderPath}\nLast file: {latestFile.Name}\nLast written: {latestFile.LastWriteTimeUtc:O}\nAge: {age.TotalSeconds:F0}s (threshold: {settings.ThresholdSeconds}s)\nTimestamp: {DateTime.UtcNow:O}",
                cancellationToken);
        }
        else
        {
            _rxState.RecordHealthy(latestFile.Name, age.TotalSeconds);
            _logger.LogInformation("RX: Heartbeat OK. Last file at {Time}", latestFile.LastWriteTimeUtc);
            await TransitionToHealthyAsync(cancellationToken);
        }
    }

    private async Task TransitionToDownAsync(string subject, string message, CancellationToken cancellationToken)
    {
        if (_isHealthy)
        {
            _logger.LogWarning("RX: Status: HEALTHY → DOWN");
            _isHealthy = false;
        }
        else
        {
            _logger.LogWarning("RX: Still DOWN.");
        }

        var now     = DateTime.UtcNow;
        var elapsed = now - _lastAlertTime;

        if (elapsed < _alertCooldown)
        {
            _logger.LogWarning("RX: Alert suppressed — cooldown active. Next alert in {Remaining:F0}s.",
                (_alertCooldown - elapsed).TotalSeconds);
            return;
        }

        _lastAlertTime = now;
        _logger.LogInformation("RX: Sending alert. Subject: {Subject}", subject);
        await _alertService.SendAlertAsync(subject, message, cancellationToken);
    }

    private async Task TransitionToHealthyAsync(CancellationToken cancellationToken)
    {
        if (_isHealthy) return;

        _logger.LogInformation("RX: Status: DOWN → HEALTHY");
        _isHealthy     = true;
        _lastAlertTime = DateTime.MinValue;

        await _alertService.SendAlertAsync(
            "Heartbeat RX — Recovery",
            $"Mode: RX\nTimestamp: {DateTime.UtcNow:O}\n\nHeartbeat monitoring has recovered to HEALTHY.",
            cancellationToken);
    }
}
