using NLog;

namespace HeartBeatProject.server.Services.Alerts;

/// <summary>
/// Sends alerts to a Syslog server via the NLog.Targets.Syslog pipeline.
/// The "SyslogAlert" NLog logger is routed exclusively to the Syslog target
/// in nlog.config (final="true"), keeping alerts separate from file/console logs.
/// SyslogHost is updated in GDC before each call so runtime host changes
/// (via RuntimeSettingsStore / POST /api/settings) take effect immediately.
/// SyslogPort and Facility require an application restart to change (typed
/// properties on the NLog target, not Layout-rendered).
/// </summary>
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

        if (!settings.EnableSyslog || string.IsNullOrWhiteSpace(settings.SyslogHost))
        {
            _logger.LogWarning("Syslog alert skipped: incomplete Syslog configuration.");
            return Task.CompletedTask;
        }

        // Update GDC so the Syslog target uses the current host for this call.
        // The ${gdc:item=SyslogHost} Layout in nlog.config is re-evaluated per log event.
        GlobalDiagnosticsContext.Set("SyslogHost", settings.SyslogHost);

        NLogSyslog.Warn("{0}: {1}", subject, message);

        _logger.LogInformation("Syslog alert sent to {Host}:{Port}. Subject: {Subject}",
            settings.SyslogHost, settings.SyslogPort, subject);

        return Task.CompletedTask;
    }
}
