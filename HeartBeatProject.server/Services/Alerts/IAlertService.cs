namespace HeartBeatProject.server.Services.Alerts;

public interface IAlertService
{
    Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default);
}

// Future implementations to register in place of (or alongside) SmtpAlertService:
//   SyslogAlertService  : IAlertService  — send RFC 5424 syslog messages
//   SnmpAlertService    : IAlertService  — send SNMP traps
