namespace HeartBeatProject.server.Services;

public sealed class HeartbeatTxService : BackgroundService
{
    private readonly ILogger<HeartbeatTxService> _logger;
    private readonly string _folderPath;
    private readonly TimeSpan _interval;
    private readonly bool _enabled;

    public HeartbeatTxService(IConfiguration config, ILogger<HeartbeatTxService> logger)
    {
        _logger     = logger;
        _enabled    = config["Heartbeat:Mode"] == "TX";
        _folderPath = config["Heartbeat:FolderPath"] ?? "C:\\Heartbeat";
        _interval   = TimeSpan.FromSeconds(config.GetValue<int>("Heartbeat:IntervalSeconds", 30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("HeartbeatTxService is disabled (Mode != TX).");
            return;
        }

        _logger.LogInformation("HeartbeatTxService started. Folder: {Folder}, Interval: {Interval}s",
            _folderPath, _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Directory.CreateDireory(_folderPath);

                var fileName = $"heartbeat_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(_folderPath, fileName);

                await File.WriteAllTextAsync(filePath, "alive", stoppingToken);

                _logger.LogInformation("Heartbeat written: {File}", fileName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to write heartbeat file.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
