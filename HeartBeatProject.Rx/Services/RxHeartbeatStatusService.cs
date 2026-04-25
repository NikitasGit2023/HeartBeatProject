using HeartBeatProject.Server.Services;
using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.Rx.Services;

public sealed class RxHeartbeatStatusService : IHeartbeatStatusService
{
    private readonly RxOperationalState _state;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public RxHeartbeatStatusService(RxOperationalState state, RuntimeSettingsStore settingsStore)
    {
        _state         = state;
        _settingsStore = settingsStore;
    }

    public StatusDto GetStatus()
    {
        var settings                       = _settingsStore.Get();
        var (status, details, lastHealthy) = _state.Get();

        return new StatusDto
        {
            Mode            = "RX",
            Status          = status,
            Details         = details,
            LastHeartbeat   = lastHealthy,
            Uptime          = DateTime.UtcNow - _startTime,
            IntervalSeconds = settings.CheckIntervalSeconds,
        };
    }
}
