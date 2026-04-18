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
    private readonly HeartbeatOptions _staticOptions;
    private readonly RuntimeSettingsStore _settingsStore;

    public HeartbeatFileGenerator(IOptions<HeartbeatOptions> options, ILogger<HeartbeatFileGenerator> logger, RuntimeSettingsStore settingsStore)
    {
        _logger        = logger;
        _staticOptions = options.Value;
        _settingsStore = settingsStore;
    }

    public async Task GenerateAsync(CancellationToken cancellationToken = default)
    {
        var folderPath = _settingsStore.Get().FolderPath;

        //creating directory if not existed inside user system
        Directory.CreateDirectory(folderPath);

        var fileName = $"{_staticOptions.FileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(folderPath, fileName);

        // used for disable generating the same file twice
        if (!_staticOptions.OverwriteExisting && File.Exists(filePath))
        {
            _logger.LogInformation("[{Time}] Heartbeat skipped — file already exists: {File}",
                DateTime.Now, fileName);
            return;
        }

        // writing to file
        await File.WriteAllTextAsync(filePath, "alive", cancellationToken);

        _logger.LogInformation("[{Time}] Heartbeat written: {File}", DateTime.Now, fileName);
    }
}
