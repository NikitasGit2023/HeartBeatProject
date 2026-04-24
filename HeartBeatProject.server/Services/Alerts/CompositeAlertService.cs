namespace HeartBeatProject.Server.Services.Alerts;

public sealed class CompositeAlertService : IAlertService
{
    private readonly SmtpAlertService _smtp;
    private readonly ISnmpTrapSender  _snmp;
    private readonly ISyslogSender    _syslog;

    public CompositeAlertService(SmtpAlertService smtp, ISnmpTrapSender snmp, ISyslogSender syslog)
    {
        _smtp   = smtp;
        _snmp   = snmp;
        _syslog = syslog;
    }

    public Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            _smtp.SendAlertAsync(subject, message, cancellationToken),
            _snmp.SendTrapAsync(subject, message, cancellationToken),
            _syslog.SendAsync(subject, message, cancellationToken));
}
