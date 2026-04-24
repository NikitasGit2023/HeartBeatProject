namespace HeartBeatProject.server.Services.Alerts;

public interface ISnmpTrapSender
{
    Task SendTrapAsync(string subject, string message, CancellationToken cancellationToken = default);
}
