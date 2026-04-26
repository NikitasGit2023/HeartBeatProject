using NLog;

namespace HeartBeatProject.Server.Services.Alerts;

public sealed class SyslogSender : ISyslogSender
{
    private static readonly Logger NLogSyslog = LogManager.GetLogger("SyslogAlert");

    private readonly ILogger<SyslogSender> _logger;
    private readonly RuntimeSettingsStore _settingsStore;

    public SyslogSender(RuntimeSettingsStore settingsStore, ILogger<SyslogSender> logger)
    {
        _settingsStore = settingsStore;
        _logger        = logger;
    }

    public Task SendAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        var settings = _settingsStore.Get();

        if (!settings.EnableSyslog)
        {
            _logger.LogDebug("Syslog alert skipped — Syslog is disabled.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(settings.SyslogHost))
        {
            _logger.LogWarning("Syslog alert skipped — SyslogHost is not configured.");
            return Task.CompletedTask;
        }

        try
        {
            GlobalDiagnosticsContext.Set("SyslogHost", settings.SyslogHost);
            NLogSyslog.Warn("{0}: {1}", subject, message);
            _logger.LogInformation("Syslog alert sent to {Host}:{Port}. Subject: {Subject}",
                settings.SyslogHost, settings.SyslogPort, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Syslog alert to {Host}:{Port}.",
                settings.SyslogHost, settings.SyslogPort);
        }

        return Task.CompletedTask;
    }
}
