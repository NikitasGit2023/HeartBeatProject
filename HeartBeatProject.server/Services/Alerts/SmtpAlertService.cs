using System.Net.Mail;
using HeartBeatProject.server.Configuration;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services.Alerts;

public sealed class SmtpAlertService : IAlertService
{
    private readonly ILogger<SmtpAlertService> _logger;
    private readonly AlertOptions _options;

    public SmtpAlertService(IOptions<AlertOptions> options, ILogger<SmtpAlertService> logger)
    {
        _logger  = logger;
        _options = options.Value;
    }

    private bool HasFullCredentials =>
        _options.EnableEmail &&
        !string.IsNullOrWhiteSpace(_options.SmtpServer) &&
        _options.Port > 0 &&
        !string.IsNullOrWhiteSpace(_options.From) &&
        !string.IsNullOrWhiteSpace(_options.To);

    public async Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        if (!HasFullCredentials)
        {
            _logger.LogWarning("Email alert skipped: incomplete SMTP configuration.");
            return;
        }

        try
        {
            using var client = new SmtpClient(_options.SmtpServer, _options.Port);
            client.EnableSsl = _options.EnableSsl;

            if (!string.IsNullOrEmpty(_options.Username))
                client.Credentials = new System.Net.NetworkCredential(_options.Username, _options.Password);

            using var mail = new MailMessage(_options.From, _options.To, subject, message);

            await client.SendMailAsync(mail, cancellationToken);

            _logger.LogInformation("[{Time}] Alert email sent: {Subject}", DateTime.Now, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] Failed to send alert email: {Subject}", DateTime.Now, subject);
        }
    }
}
