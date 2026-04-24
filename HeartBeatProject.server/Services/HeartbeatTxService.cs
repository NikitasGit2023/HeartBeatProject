using HeartBeatProject.server.Services.Alerts;

namespace HeartBeatProject.server.Services;

public sealed class HeartbeatTxService : BackgroundService
{
    private readonly ILogger<HeartbeatTxService> _logger;
    private readonly IHeartbeatFileGenerator _fileGenerator;
    private readonly IAlertService _alertService;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(5);
    private bool _lastWasSuccess = true;
    private DateTime _lastAlertTime = DateTime.MinValue;

    public HeartbeatTxService(
        ILogger<HeartbeatTxService> logger,
        IHeartbeatFileGenerator fileGenerator,
        IAlertService alertService,
        RuntimeSettingsStore settingsStore)
    {
        _logger        = logger;
        _fileGenerator = fileGenerator;
        _alertService  = alertService;
        _settingsStore = settingsStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatTxService started. Interval: {Interval}s",
            _settingsStore.Get().IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _fileGenerator.GenerateAsync(stoppingToken);
                await OnSuccessAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Heartbeat file generation failed.");
                await OnFailureAsync(ex, stoppingToken);
            }

            var intervalSeconds = _settingsStore.Get().IntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task OnSuccessAsync(CancellationToken cancellationToken)
    {
        if (_lastWasSuccess) return;

        _logger.LogInformation("TX: Status: DOWN \u2192 HEALTHY — heartbeat file generation recovered.");
        _lastWasSuccess = true;
        _lastAlertTime  = DateTime.MinValue;

        await _alertService.SendAlertAsync(
            "Heartbeat TX \u2014 Recovery",
            $"Mode: TX\nTimestamp: {DateTime.UtcNow:O}\n\nHeartbeat file generation has recovered.",
            cancellationToken);
    }

    private async Task OnFailureAsync(Exception ex, CancellationToken cancellationToken)
    {
        if (_lastWasSuccess)
        {
            _logger.LogWarning("TX: Status: HEALTHY \u2192 DOWN");
            _lastWasSuccess = false;
        }
        else
        {
            _logger.LogWarning("TX: Still failing.");
        }

        var now     = DateTime.UtcNow;
        var elapsed = now - _lastAlertTime;

        if (elapsed < _alertCooldown)
        {
            _logger.LogWarning("TX: Alert suppressed — cooldown active. Next alert in {Remaining:F0}s.",
                (_alertCooldown - elapsed).TotalSeconds);
            return;
        }

        var folder     = _settingsStore.Get().FolderPath;
        _lastAlertTime = now;
        _logger.LogInformation("TX: Sending alert. Folder: {Folder}", folder);

        await _alertService.SendAlertAsync(
            "Heartbeat TX \u2014 File Generation Failed",
            $"Mode: TX\nFolder: {folder}\nTimestamp: {DateTime.UtcNow:O}\n\nError: {ex.Message}",
            cancellationToken);
    }
}
