using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.server.Logging;

public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ILogStore _store;
    public InMemoryLoggerProvider(ILogStore store) => _store = store;

    public ILogger CreateLogger(string categoryName) =>
        new InMemoryLogger(_store, MapCategory(categoryName));

    public void Dispose() { }

    private static string MapCategory(string categoryName) => categoryName switch
    {
        var c when c.EndsWith("TxService")    => "TX",
        var c when c.EndsWith("RxService")    => "RX",
        var c when c.EndsWith("AlertService") => "Email",
        _                                      => "System"
    };
}

internal sealed class InMemoryLogger : ILogger
{
    private readonly ILogStore _store;
    private readonly string    _category;

    public InMemoryLogger(ILogStore store, string category)
    {
        _store    = store;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        _store.Add(new LogEntryDto
        {
            Timestamp = DateTime.Now,
            Level     = logLevel.ToString(),
            Category  = _category,
            Message   = formatter(state, exception)
        });
    }
}
