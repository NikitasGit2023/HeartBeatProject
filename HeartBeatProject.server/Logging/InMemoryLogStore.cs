using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.server.Logging;

public interface ILogStore
{
    void Add(LogEntryDto entry);
    IReadOnlyList<LogEntryDto> GetRecent(int count = 200);
}

public sealed class InMemoryLogStore : ILogStore
{
    private readonly Queue<LogEntryDto> _entries = new();
    private readonly object _lock = new();
    private const int MaxEntries = 500;

    private static readonly string LogDir  = Path.Combine(AppContext.BaseDirectory, "Logs");
    private static string LogFile => Path.Combine(LogDir, $"heartbeat_{DateTime.Now:yyyyMMdd}.txt");

    public void Add(LogEntryDto entry)
    {
        lock (_lock)
        {
            if (_entries.Count >= MaxEntries) _entries.Dequeue();
            _entries.Enqueue(entry);

            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(LogFile,
                    $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level,-11}] {entry.Message}{Environment.NewLine}");
            }
            catch { /* never crash the service over a log write failure */ }
        }
    }

    public IReadOnlyList<LogEntryDto> GetRecent(int count = 200)
    {
        lock (_lock)
            return _entries.TakeLast(count).Reverse().ToList();
    }
}
