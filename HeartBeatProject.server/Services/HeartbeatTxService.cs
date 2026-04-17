using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.Mode != "TX")
        {
            _logger.LogInformation("HeartbeatTxService is disabled (Mode != TX).");
            return;
        }

        _logger.LogInformation("HeartbeatTxService started. Interval: {Interval}s", _options.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _fileGenerator.GenerateAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[{Time}] Heartbeat file generation failed.", DateTime.Now);

                await _alertService.SendAlertAsync(
                    subject: "Heartbeat TX Failure",
                    message: $"Failed to write heartbeat file at {DateTime.Now}.\n\nError: {ex.Message}",
                    cancellationToken: stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
        }
    }
}
