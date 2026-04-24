namespace HeartBeatProject.server.Services.Alerts;

public sealed class CompositeAlertService : IAlertService
{
    private readonly SmtpAlertService _smtp;
    private readonly ISnmpTrapSender  _snmp;

    public CompositeAlertService(SmtpAlertService smtp, ISnmpTrapSender snmp)
    {
        _smtp = smtp;
        _snmp = snmp;
    }

    public Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            _smtp.SendAlertAsync(subject, message, cancellationToken),
            _snmp.SendTrapAsync(subject, message, cancellationToken));
}
