namespace HeartBeatProject.server.Services.Alerts;

public interface ISyslogSender
{
    Task SendAsync(string subject, string message, CancellationToken cancellationToken = default);
}
