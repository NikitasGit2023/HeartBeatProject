using HeartBeatProject.Server.Services;
using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.Tx.Services;

public sealed class TxHeartbeatStatusService : IHeartbeatStatusService
{
    private readonly TxOperationalState _state;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public TxHeartbeatStatusService(TxOperationalState state, RuntimeSettingsStore settingsStore)
    {
        _state         = state;
        _settingsStore = settingsStore;
    }

    public StatusDto GetStatus()
    {
        var settings                       = _settingsStore.Get();
        var (status, details, lastSuccess) = _state.Get();

        return new StatusDto
        {
            Mode            = "TX",
            Status          = status,
            Details         = details,
            LastHeartbeat   = lastSuccess,
            Uptime          = DateTime.UtcNow - _startTime,
            IntervalSeconds = settings.IntervalSeconds,
        };
    }
}
