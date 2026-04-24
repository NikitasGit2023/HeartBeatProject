namespace HeartBeatProject.Server.Services.Alerts;

public interface IAlertService
{
    Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default);
}

