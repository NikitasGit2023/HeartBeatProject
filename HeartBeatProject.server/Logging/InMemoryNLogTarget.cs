using HeartBeatProject.Shared.Dtos;
using NLog;
using NLog.Targets;

namespace HeartBeatProject.Server.Logging;

public sealed class InMemoryNLogTarget : TargetWithLayout
{
    private readonly ILogStore _store;

    public InMemoryNLogTarget(ILogStore store)
    {
        _store = store;
        Name   = "dashboard";
    }

    protected override void Write(LogEventInfo logEvent)
    {
        _store.Add(new LogEntryDto
        {
            Timestamp = logEvent.TimeStamp.ToUniversalTime(),
            Level     = ToMicrosoftLevel(logEvent.Level),
            Category  = MapCategory(logEvent.LoggerName ?? string.Empty),
            Message   = logEvent.Exception is null
                            ? logEvent.FormattedMessage
                            : $"{logEvent.FormattedMessage} | {logEvent.Exception.Message}"
        });
    }

    private static string ToMicrosoftLevel(NLog.LogLevel level)
    {
        if (level == NLog.LogLevel.Info)  return "Information";
        if (level == NLog.LogLevel.Warn)  return "Warning";
        if (level == NLog.LogLevel.Fatal) return "Critical";
        return level.Name; // Debug, Trace, Error — identical
    }

    private static string MapCategory(string loggerName) => loggerName switch
    {
        var c when c.EndsWith("TxService")    => "TX",
        var c when c.EndsWith("RxService")    => "RX",
        var c when c.EndsWith("AlertService") => "Email",
        _                                      => "System"
    };
}
