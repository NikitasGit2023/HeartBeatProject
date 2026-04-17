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
    private readonly HeartbeatOptions _options;

    public HeartbeatTxService(
        IOptions<HeartbeatOptions> options,
        ILogger<HeartbeatTxService> logger,
        IHeartbeatFileGenerator fileGenerator,
        IAlertService alertService)
    {
        _logger        = logger;
        _fileGenerator = fileGenerator;
        _alertService  = alertService;
        _options       = options.Value;
    }

    //Starting the background service
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatTxService started. Interval: {Interval}s", _options.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                //generating the file
                await _fileGenerator.GenerateAsync(stoppingToken);
            }
            catch (Exception ex) /*when (ex is not OperationCanceledException)*/
            {
                _logger.LogError(ex, "[{Time}] Heartbeat file generation failed.", DateTime.Now);

                //sending alert
                await _alertService.SendAlertAsync(
                    subject: "Heartbeat TX Failure",
                    message: $"Failed to write heartbeat file at {DateTime.Now}.\n\nError: {ex.Message}",
                    cancellationToken: stoppingToken);
            }

            //Dealy for smooth CPU usage.
            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
        }
    }
}
