using System.Net;
using System.Net.Mail;
using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.Server.Services.Alerts;

public sealed class SmtpAlertService : IAlertService
{
    private const int MaxAttempts  = 3;
    private const int RetryDelayMs = 2_000;
    private const int TimeoutMs    = 15_000;

    private readonly ILogger<SmtpAlertService> _logger;
    private readonly RuntimeSettingsStore _settingsStore;

    public SmtpAlertService(RuntimeSettingsStore settingsStore, ILogger<SmtpAlertService> logger)
    {
        _settingsStore = settingsStore;
        _logger        = logger;
    }

    public async Task SendAlertAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        var opts = _settingsStore.Get();

        if (!IsConfigured(opts))
        {
            LogSkipped(opts);
            return;
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var client = BuildClient(opts);
                using var mail   = BuildMessage(opts, subject, message);

                await client.SendMailAsync(mail, cancellationToken);

                _logger.LogInformation(
                    "Alert email sent (attempt {Attempt}/{Max}). Subject: {Subject}",
                    attempt, MaxAttempts, subject);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SmtpException ex) when (attempt == MaxAttempts)
            {
                _logger.LogError(ex,
                    "Failed to send alert email after {Attempts} attempts (SMTP status {Status}) to {Server}:{Port}. Subject: {Subject}",
                    MaxAttempts, ex.StatusCode, opts.SmtpServer, opts.Port, subject);
                return;
            }
            catch (Exception ex) when (attempt == MaxAttempts)
            {
                _logger.LogError(ex,
                    "Failed to send alert email after {Attempts} attempts to {Server}:{Port}. Subject: {Subject}",
                    MaxAttempts, opts.SmtpServer, opts.Port, subject);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Email send attempt {Attempt}/{Max} failed — retrying in {Delay}ms. Subject: {Subject}",
                    attempt, MaxAttempts, RetryDelayMs, subject);

                await Task.Delay(RetryDelayMs, cancellationToken);
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static bool IsConfigured(SettingsDto opts) =>
        opts.EnableEmail &&
        !string.IsNullOrWhiteSpace(opts.SmtpServer) &&
        opts.Port > 0 &&
        !string.IsNullOrWhiteSpace(opts.From) &&
        !string.IsNullOrWhiteSpace(opts.To);

    private void LogSkipped(SettingsDto opts)
    {
        if (!opts.EnableEmail)
            _logger.LogWarning("Email alert skipped — EnableEmail is false.");
        else if (string.IsNullOrWhiteSpace(opts.SmtpServer))
            _logger.LogWarning("Email alert skipped — SmtpServer is not configured.");
        else if (opts.Port <= 0)
            _logger.LogWarning("Email alert skipped — Port is invalid ({Port}).", opts.Port);
        else if (string.IsNullOrWhiteSpace(opts.From))
            _logger.LogWarning("Email alert skipped — From address is not configured.");
        else
            _logger.LogWarning("Email alert skipped — To address is not configured.");
    }

    private static SmtpClient BuildClient(SettingsDto opts)
    {
        var client = new SmtpClient(opts.SmtpServer, opts.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl      = opts.EnableSsl,
            Timeout        = TimeoutMs,
        };

        if (!string.IsNullOrEmpty(opts.Username))
            client.Credentials = new NetworkCredential(opts.Username, opts.Password);

        return client;
    }

    // Supports semicolon- or comma-separated To addresses.
    private static MailMessage BuildMessage(SettingsDto opts, string subject, string body)
    {
        var mail = new MailMessage
        {
            From    = new MailAddress(opts.From),
            Subject = subject,
            Body    = body,
        };

        foreach (var addr in opts.To.Split(new[] { ';', ',' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            mail.To.Add(new MailAddress(addr));
        }

        return mail;
    }
}
