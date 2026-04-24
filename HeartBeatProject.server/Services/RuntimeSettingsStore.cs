using System.Net.Mail;
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
            // Heartbeat
            FolderPath           = h.FolderPath,
            IntervalSeconds      = h.IntervalSeconds,
            OverwriteExisting    = h.OverwriteExisting,
            CheckIntervalSeconds = h.CheckIntervalSeconds,
            ThresholdSeconds     = h.ThresholdSeconds,

            // Email
            EnableEmail = a.EnableEmail,
            SmtpServer  = a.SmtpServer,
            Port        = a.Port,
            From        = a.From,
            To          = a.To,
            Username    = a.Username,
            Password    = a.Password,
            EnableSsl   = a.EnableSsl,

            // SNMP
            EnableSnmp = a.EnableSnmp,
            SnmpHost   = a.SnmpHost,
            SnmpPort   = a.SnmpPort,
            Community  = a.Community,

            // Syslog
            EnableSyslog   = a.EnableSyslog,
            SyslogHost     = a.SyslogHost,
            SyslogPort     = a.SyslogPort,
            SyslogFacility = a.SyslogFacility,
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

        // System.Net.Mail.SmtpClient uses STARTTLS and does not support implicit SSL on port 465.
        if (dto.EnableEmail && dto.EnableSsl && dto.Port == 465)
            throw new ArgumentOutOfRangeException(nameof(dto),
                "Port 465 (implicit SSL) is not supported by System.Net.Mail. Use port 587 with STARTTLS.");

        // Validate email addresses when they are supplied.
        if (!string.IsNullOrWhiteSpace(dto.From))
            ValidateEmailAddress(dto.From, "From");

        if (!string.IsNullOrWhiteSpace(dto.To))
        {
            foreach (var addr in dto.To.Split(new[] { ';', ',' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                ValidateEmailAddress(addr, "To");
            }
        }

        if (dto.SnmpPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(dto), "SnmpPort must be between 1 and 65535.");

        if (dto.SyslogPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(dto), "SyslogPort must be between 1 and 65535.");
    }

    private static void ValidateEmailAddress(string address, string fieldName)
    {
        try   { _ = new MailAddress(address); }
        catch { throw new ArgumentException($"{fieldName} contains an invalid email address: '{address}'.", "dto"); }
    }

    private static SettingsDto Copy(SettingsDto src) => new()
    {
        // Heartbeat
        FolderPath           = src.FolderPath,
        IntervalSeconds      = src.IntervalSeconds,
        OverwriteExisting    = src.OverwriteExisting,
        CheckIntervalSeconds = src.CheckIntervalSeconds,
        ThresholdSeconds     = src.ThresholdSeconds,

        // Email
        EnableEmail = src.EnableEmail,
        SmtpServer  = src.SmtpServer,
        Port        = src.Port,
        From        = src.From,
        To          = src.To,
        Username    = src.Username,
        Password    = src.Password,
        EnableSsl   = src.EnableSsl,

        // SNMP
        EnableSnmp = src.EnableSnmp,
        SnmpHost   = src.SnmpHost,
        SnmpPort   = src.SnmpPort,
        Community  = src.Community,

        // Syslog
        EnableSyslog   = src.EnableSyslog,
        SyslogHost     = src.SyslogHost,
        SyslogPort     = src.SyslogPort,
        SyslogFacility = src.SyslogFacility,
    };
}
