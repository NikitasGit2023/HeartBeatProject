using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.server.Logging;

public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ILogStore _store;
    public InMemoryLoggerProvider(ILogStore store) => _store = store;

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_store);
    public void Dispose() { }
}

internal sealed class InMemoryLogger : ILogger
{
    private readonly ILogStore _store;
    public InMemoryLogger(ILogStore store) => _store = store;

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
            Message   = formatter(state, exception)
        });
    }
}
