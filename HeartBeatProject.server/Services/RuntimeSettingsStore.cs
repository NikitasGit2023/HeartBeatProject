using HeartBeatProject.server.Configuration;
using HeartBeatProject.Shared.Dtos;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services;

public sealed class RuntimeSettingsStore
{
    private SettingsDto _current;
    private readonly object _lock = new();

    public RuntimeSettingsStore(IOptions<HeartbeatOptions> heartbeat, IOptions<AlertOptions> alerts)
    {
        var h = heartbeat.Value;
        var a = alerts.Value;
        _current = new SettingsDto
        {
            FolderPath           = h.FolderPath,
            IntervalSeconds      = h.IntervalSeconds,
            CheckIntervalSeconds = h.CheckIntervalSeconds,
            ThresholdSeconds     = h.ThresholdSeconds,
            EnableEmail          = a.EnableEmail,
            SmtpServer           = a.SmtpServer,
            Port                 = a.Port,
            From                 = a.From,
            To                   = a.To
        };
    }

    //Write and read to file are thread safe, that why use lock is neccesary.
    public SettingsDto Get() { lock (_lock) return _current; }
    public void Update(SettingsDto dto) { lock (_lock) _current = dto; }
}
