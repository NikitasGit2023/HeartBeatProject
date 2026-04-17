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

    //sending alerts using email.
    public async Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableEmail) return; //if emable sending is disabled

        try
        {
            using var client = new SmtpClient(_options.SmtpServer, _options.Port);
            using var mail   = new MailMessage(_options.From, _options.To, subject, message);

            await client.SendMailAsync(mail, cancellationToken);

            _logger.LogInformation("[{Time}] Alert email sent: {Subject}", DateTime.Now, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] Failed to send alert email: {Subject}", DateTime.Now, subject);
        }
    }
}
