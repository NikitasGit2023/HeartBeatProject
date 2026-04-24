using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.server.Logging;

public interface ILogStore
{
    void Add(LogEntryDto entry);
    IReadOnlyList<LogEntryDto> GetRecent(int count = 200);
}

public sealed class InMemoryLogStore : ILogStore
{
    private readonly Queue<LogEntryDto> _queue = [];
    private readonly object _lock = new();
    private const int MaxEntries = 500;

    public void Add(LogEntryDto entry)
    {
        lock (_lock)
        {
            if (_queue.Count >= MaxEntries) _queue.Dequeue();
            _queue.Enqueue(entry);
        }
    }

    public IReadOnlyList<LogEntryDto> GetRecent(int count = 200)
    {
        lock (_lock)
            return _queue.TakeLast(count).Reverse().ToList();
    }
}
