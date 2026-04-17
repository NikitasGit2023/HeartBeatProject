using System.Net.Mail;

namespace HeartBeatProject.server.Services.Alerts;

public sealed class SmtpAlertService : IAlertService
{
    private readonly ILogger<SmtpAlertService> _logger;
    private readonly bool _enabled;
    private readonly string _smtpServer;
    private readonly int _port;
    private readonly string _from;
    private readonly string _to;

    public SmtpAlertService(IConfiguration config, ILogger<SmtpAlertService> logger)
    {
        _logger     = logger;
        _enabled    = config.GetValue<bool>("Alerts:EnableEmail");
        _smtpServer = config["Alerts:SmtpServer"] ?? string.Empty;
        _port       = config.GetValue<int>("Alerts:Port", 25);
        _from       = config["Alerts:From"] ?? string.Empty;
        _to         = config["Alerts:To"] ?? string.Empty;
    }

    public async Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        if (!_enabled) return;

        try
        {
            using var client = new SmtpClient(_smtpServer, _port);
            using var mail   = new MailMessage(_from, _to, subject, message);

            await client.SendMailAsync(mail, cancellationToken);

            _logger.LogInformation("[{Time}] Alert email sent: {Subject}", DateTime.Now, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] Failed to send alert email: {Subject}", DateTime.Now, subject);
        }
    }
}
