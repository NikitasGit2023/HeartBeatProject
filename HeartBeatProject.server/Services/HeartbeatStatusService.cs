using HeartBeatProject.server.Configuration;
using HeartBeatProject.Shared.Dtos;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services;

public sealed class HeartbeatStatusService : IHeartbeatStatusService
{
    private readonly ILogger<HeartbeatStatusService> _logger;
    private readonly IOptions<HeartbeatOptions> _heartbeatOptions;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public HeartbeatStatusService(
        IOptions<HeartbeatOptions> heartbeatOptions,
        RuntimeSettingsStore settingsStore,
        ILogger<HeartbeatStatusService> logger)
    {
        _heartbeatOptions = heartbeatOptions;
        _settingsStore    = settingsStore;
        _logger           = logger;
    }

    public StatusDto GetStatus()
    {
        var settings = _settingsStore.Get();
        DateTime? lastHeartbeat = null;
        string status;

        try
        {
            var latestFile = Directory.Exists(settings.FolderPath)
                ? Directory.EnumerateFiles(settings.FolderPath, "*.txt")
                           .Select(f => new FileInfo(f))
                           .OrderByDescending(f => f.LastWriteTimeUtc)
                           .FirstOrDefault()
                : null;

            if (latestFile is null)
            {
                status = "DOWN";
            }
            else
            {
                lastHeartbeat = latestFile.LastWriteTimeUtc;
                status = (DateTime.UtcNow - latestFile.LastWriteTimeUtc).TotalSeconds <= settings.ThresholdSeconds
                    ? "HEALTHY"
                    : "DOWN";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read heartbeat status from folder {Folder}.", settings.FolderPath);
            status = "DOWN";
        }

        return new StatusDto
        {
            Mode            = _heartbeatOptions.Value.Mode,
            Status          = status,
            LastHeartbeat   = lastHeartbeat,
            Uptime          = DateTime.UtcNow - _startTime,
            IntervalSeconds = _heartbeatOptions.Value.Mode.Equals("RX", StringComparison.OrdinalIgnoreCase)
                ? settings.CheckIntervalSeconds
                : settings.IntervalSeconds
        };
    }
}
