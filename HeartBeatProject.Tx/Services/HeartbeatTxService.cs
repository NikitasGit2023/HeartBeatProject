using HeartBeatProject.Server.Services;
using HeartBeatProject.Server.Services.Alerts;

namespace HeartBeatProject.Tx.Services;

public sealed class HeartbeatTxService : BackgroundService
{
    private readonly ILogger<HeartbeatTxService> _logger;
    private readonly IHeartbeatFileGenerator _fileGenerator;
    private readonly IAlertService _alertService;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly TxOperationalState _state;
    private readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(5);
    private bool _lastWasSuccess = true;
    private DateTime _lastAlertTime = DateTime.MinValue;

    public HeartbeatTxService(
        ILogger<HeartbeatTxService> logger,
        IHeartbeatFileGenerator fileGenerator,
        IAlertService alertService,
        RuntimeSettingsStore settingsStore,
        TxOperationalState state)
    {
        _logger        = logger;
        _fileGenerator = fileGenerator;
        _alertService  = alertService;
        _settingsStore = settingsStore;
        _state         = state;
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
        if (_lastWasSuccess)
        {
            _state.RecordSuccess();
            return;
        }

        // Capture the previous status before overwriting it with RecordSuccess.
        var (previousStatus, _, _) = _state.Get();
        _state.RecordSuccess();

        _logger.LogInformation("TX: Status: {Previous} → RUNNING — file generation recovered.", previousStatus);
        _lastWasSuccess = true;
        _lastAlertTime  = DateTime.MinValue;

        await _alertService.SendAlertAsync(
            "Heartbeat TX — Recovery",
            $"Mode: TX\nTimestamp: {DateTime.UtcNow:O}\n\nHeartbeat file generation has recovered.",
            cancellationToken);
    }

    private async Task OnFailureAsync(Exception ex, CancellationToken cancellationToken)
    {
        var isPathIssue = ex is UnauthorizedAccessException
                              or PathTooLongException
                              or DirectoryNotFoundException
                              or IOException
                              or NotSupportedException;
        _state.RecordFailure(isPathIssue, ex.Message);
        var newStatus = isPathIssue ? "DEGRADED" : "ERROR";

        if (_lastWasSuccess)
        {
            _logger.LogWarning("TX: Status: RUNNING → {Status} — {Reason}", newStatus, ex.Message);
            _lastWasSuccess = false;
        }
        else
        {
            _logger.LogWarning("TX: Still {Status} — {Reason}", newStatus, ex.Message);
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
            "Heartbeat TX — File Generation Failed",
            $"Mode: TX\nFolder: {folder}\nTimestamp: {DateTime.UtcNow:O}\n\nError: {ex.Message}",
            cancellationToken);
    }
}
