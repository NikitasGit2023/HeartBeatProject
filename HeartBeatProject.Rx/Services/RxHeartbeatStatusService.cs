using HeartBeatProject.Server.Services;
using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.Rx.Services;

public sealed class RxHeartbeatStatusService : IHeartbeatStatusService
{
    private readonly ILogger<RxHeartbeatStatusService> _logger;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public RxHeartbeatStatusService(RuntimeSettingsStore settingsStore, ILogger<RxHeartbeatStatusService> logger)
    {
        _settingsStore = settingsStore;
        _logger        = logger;
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
            Mode            = "RX",
            Status          = status,
            LastHeartbeat   = lastHeartbeat,
            Uptime          = DateTime.UtcNow - _startTime,
            IntervalSeconds = settings.CheckIntervalSeconds
        };
    }
}
