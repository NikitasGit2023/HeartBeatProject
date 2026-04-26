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

    // Task.WhenAll is safe here because each provider catches all its own exceptions
    // internally and never lets a fault propagate — a failure in one channel does
    // not cancel or suppress the others.
    public Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            _smtp.SendAlertAsync(subject, message, cancellationToken),
            _snmp.SendTrapAsync(subject, message, cancellationToken),
            _syslog.SendAsync(subject, message, cancellationToken));
}
