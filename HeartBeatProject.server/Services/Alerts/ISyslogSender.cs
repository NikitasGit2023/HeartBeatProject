namespace HeartBeatProject.Server.Services.Alerts;

public interface ISyslogSender
{
    Task SendAsync(string subject, string message, CancellationToken cancellationToken = default);
}
