using HeartBeatProject.Server.Configuration;
using HeartBeatProject.Server.Services;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.Tx.Services;

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
        var settings   = _settingsStore.Get();
        var folderPath = settings.FolderPath;

        if (string.IsNullOrWhiteSpace(folderPath))
            throw new DirectoryNotFoundException("FolderPath is not configured.");

        Directory.CreateDirectory(folderPath);

        // OverwriteExisting=true  → single fixed file (RX always sees the freshest write time)
        // OverwriteExisting=false → timestamped file per cycle (accumulates; RX picks latest by LastWriteTimeUtc)
        var fileName = settings.OverwriteExisting
            ? $"{_staticOptions.FileNamePrefix}_latest.txt"
            : $"{_staticOptions.FileNamePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";

        var filePath = Path.Combine(folderPath, fileName);

        await File.WriteAllTextAsync(filePath, "alive", cancellationToken);

        _logger.LogInformation("Heartbeat written: {File}", fileName);
    }
}
