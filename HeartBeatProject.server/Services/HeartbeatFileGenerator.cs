using HeartBeatProject.server.Configuration;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services;

public interface IHeartbeatFileGenerator
{
    Task GenerateAsync(CancellationToken cancellationToken = default);
}

public sealed class HeartbeatFileGenerator : IHeartbeatFileGenerator
{
    private readonly ILogger<HeartbeatFileGenerator> _logger;
    private readonly HeartbeatOptions _options;

    public HeartbeatFileGenerator(IOptions<HeartbeatOptions> options, ILogger<HeartbeatFileGenerator> logger)
    {
        _logger  = logger;
        _options = options.Value;
    }

    public async Task GenerateAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.FolderPath);

        var fileName = $"{_options.FileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(_options.FolderPath, fileName);

        if (!_options.OverwriteExisting && File.Exists(filePath))
        {
            _logger.LogInformation("[{Time}] Heartbeat skipped — file already exists: {File}",
                DateTime.Now, fileName);
            return;
        }

        await File.WriteAllTextAsync(filePath, "alive", cancellationToken);

        _logger.LogInformation("[{Time}] Heartbeat written: {File}", DateTime.Now, fileName);
    }
}
