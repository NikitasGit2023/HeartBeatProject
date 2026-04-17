namespace HeartBeatProject.server.Services;

public interface IHeartbeatFileGenerator
{
    Task GenerateAsync(CancellationToken cancellationToken = default);
}

public sealed class HeartbeatFileGenerator : IHeartbeatFileGenerator
{
    private readonly ILogger<HeartbeatFileGenerator> _logger;
    private readonly string _folderPath;
    private readonly string _prefix;
    private readonly bool _overwrite;

    public HeartbeatFileGenerator(IConfiguration config, ILogger<HeartbeatFileGenerator> logger)
    {
        _logger     = logger;
        _folderPath = config["Heartbeat:FolderPath"] ?? "C:\\Heartbeat";
        _prefix     = config["Heartbeat:FileNamePrefix"] ?? "heartbeat";
        _overwrite  = config.GetValue<bool>("Heartbeat:OverwriteExisting", true);
    }

    public async Task GenerateAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_folderPath);

        var fileName = $"{_prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(_folderPath, fileName);

        if (!_overwrite && File.Exists(filePath))
        {
            _logger.LogInformation("[{Time}] Heartbeat skipped — file already exists: {File}",
                DateTime.Now, fileName);
            return;
        }

        await File.WriteAllTextAsync(filePath, "alive", cancellationToken);

        _logger.LogInformation("[{Time}] Heartbeat written: {File}", DateTime.Now, fileName);
    }
}
