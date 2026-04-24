namespace HeartBeatProject.Server.Services.Alerts;

public interface ISnmpTrapSender
{
    Task SendTrapAsync(string subject, string message, CancellationToken cancellationToken = default);
}
