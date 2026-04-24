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

    public SettingsDto Get()
    {
        lock (_lock) return Copy(_current);
    }

    public void Update(SettingsDto dto)
    {
        Validate(dto);
        lock (_lock) _current = Copy(dto);
    }

    private static void Validate(SettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FolderPath))
            throw new ArgumentException("FolderPath must not be empty.", nameof(dto));

        if (dto.IntervalSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto), "IntervalSeconds must be greater than 0.");

        if (dto.CheckIntervalSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto), "CheckIntervalSeconds must be greater than 0.");

        if (dto.ThresholdSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto), "ThresholdSeconds must be greater than 0.");

        if (dto.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(dto), "Port must be between 1 and 65535.");
    }

    private static SettingsDto Copy(SettingsDto src) => new()
    {
        FolderPath           = src.FolderPath,
        IntervalSeconds      = src.IntervalSeconds,
        CheckIntervalSeconds = src.CheckIntervalSeconds,
        ThresholdSeconds     = src.ThresholdSeconds,
        EnableEmail          = src.EnableEmail,
        SmtpServer           = src.SmtpServer,
        Port                 = src.Port,
        From                 = src.From,
        To                   = src.To
    };
}
