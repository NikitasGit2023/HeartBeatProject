using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services;

/// <summary>
/// Provides a background service that periodically generates a heartbeat file and sends alerts if file generation
/// fails.
/// </summary>
/// <remarks>This service runs continuously in the background, generating heartbeat files at intervals specified
/// by configuration. If an error occurs during file generation, an alert is sent using the configured alert service.
/// The service is intended for use in scenarios where regular heartbeat signaling and failure notification are
/// required, such as monitoring or high-availability systems.</remarks>
public sealed class HeartbeatTxService : BackgroundService
{
    private readonly ILogger<HeartbeatTxService> _logger;
    private readonly IHeartbeatFileGenerator _fileGenerator;
    private readonly IAlertService _alertService;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly HeartbeatOptions _staticOptions;
    private bool _lastWasSuccess = true;

    public HeartbeatTxService(
        IOptions<HeartbeatOptions> options,
        ILogger<HeartbeatTxService> logger,
        IHeartbeatFileGenerator fileGenerator,
        IAlertService alertService,
        RuntimeSettingsStore settingsStore)
    {
        _logger        = logger;
        _fileGenerator = fileGenerator;
        _alertService  = alertService;
        _settingsStore = settingsStore;
        _staticOptions = options.Value;
    }

    //Starting the background service
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatTxService started. Mode: {Mode}", _staticOptions.Mode);

        if (!_staticOptions.Mode.Equals("TX", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("HeartbeatTxService: Mode is '{Mode}' — TX execution skipped.", _staticOptions.Mode);
            return;
        }

        _logger.LogInformation("HeartbeatTxService: Running. Interval: {Interval}s", _staticOptions.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                //generating the file
                await _fileGenerator.GenerateAsync(stoppingToken);

                if (!_lastWasSuccess)
                {
                    _logger.LogInformation("[{Time}] Heartbeat TX recovered.", DateTime.Now);
                    _lastWasSuccess = true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[{Time}] Heartbeat file generation failed.", DateTime.Now);

                // Only alert on the first failure to avoid flooding
                if (_lastWasSuccess)
                {
                    _lastWasSuccess = false;
                    var folder = _settingsStore.Get().FolderPath;
                    await _alertService.SendAlertAsync(
                        subject: "Heartbeat TX — File Generation Failed",
                        message: $"Mode: TX\nFolder: {folder}\nTimestamp: {DateTime.Now}\n\nError: {ex.Message}",
                        cancellationToken: stoppingToken);
                }
            }

            var intervalSeconds = _settingsStore.Get().IntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
