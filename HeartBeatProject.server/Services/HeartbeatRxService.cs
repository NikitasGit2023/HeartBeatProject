using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_staticOptions.Mode.Equals("RX", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("HeartbeatRxService skipped — configured mode is {Mode}.", _staticOptions.Mode);
            return;
        }

        var initial = _settingsStore.Get();
        _logger.LogInformation(
            "HeartbeatRxService started. Folder: {Folder}, Threshold: {Threshold}s, Check interval: {Interval}s",
            initial.FolderPath, initial.ThresholdSeconds, initial.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("RX: Monitoring cycle started");
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error in HeartbeatRxService.");
                await TransitionToDownAsync(
                    "Heartbeat RX \u2014 Unexpected Error",
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
            _logger.LogWarning("Heartbeat folder not found: {Folder}.", settings.FolderPath);
            await TransitionToDownAsync(
                "Heartbeat RX \u2014 Folder Missing",
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
            _logger.LogWarning("No heartbeat files found in {Folder}.", settings.FolderPath);
            await TransitionToDownAsync(
                "Heartbeat RX \u2014 No Files",
                $"Mode: RX\nFolder: {settings.FolderPath}\nTimestamp: {DateTime.UtcNow:O}\n\nNo heartbeat files found in the monitored folder.",
                cancellationToken);
            return;
        }

        var age = DateTime.UtcNow - latestFile.LastWriteTimeUtc;

        _logger.LogInformation("RX: Latest heartbeat: {File} ({Age:F1}s ago)", latestFile.Name, age.TotalSeconds);

        if (age.TotalSeconds > settings.ThresholdSeconds)
        {
            _logger.LogWarning("RX: Threshold exceeded. Last file '{File}' is {Age:F0}s old (threshold: {Threshold}s).",
                latestFile.Name, age.TotalSeconds, settings.ThresholdSeconds);
            await TransitionToDownAsync(
                "Heartbeat RX \u2014 Threshold Exceeded",
                $"Mode: RX\nFolder: {settings.FolderPath}\nLast file: {latestFile.Name}\nLast written: {latestFile.LastWriteTimeUtc:O}\nAge: {age.TotalSeconds:F0}s (threshold: {settings.ThresholdSeconds}s)\nTimestamp: {DateTime.UtcNow:O}",
                cancellationToken);
        }
        else
        {
            _logger.LogInformation("RX: Heartbeat OK. Last file at {Time}", latestFile.LastWriteTimeUtc);
            await TransitionToHealthyAsync(cancellationToken);
        }
    }

    private async Task TransitionToDownAsync(string subject, string message, CancellationToken cancellationToken)
    {
        if (_isHealthy)
        {
            _logger.LogWarning("RX: Status: HEALTHY \u2192 DOWN");
            _isHealthy = false;
        }
        else
        {
            _logger.LogWarning("RX: Still DOWN.");
        }

        var now = DateTime.UtcNow;
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

        _logger.LogInformation("RX: Status: DOWN \u2192 HEALTHY");
        _isHealthy = true;
        _lastAlertTime = DateTime.MinValue;

        await _alertService.SendAlertAsync(
            "Heartbeat RX \u2014 Recovery",
            $"Mode: RX\nTimestamp: {DateTime.UtcNow:O}\n\nHeartbeat monitoring has recovered to HEALTHY.",
            cancellationToken);
    }
}
