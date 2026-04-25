namespace HeartBeatProject.Rx.Services;

public sealed class RxOperationalState
{
    private readonly object _lock = new();
    private string    _status      = "STARTING";
    private string    _details     = "Service is starting up.";
    private DateTime? _lastHealthy;

    public void RecordHealthy(string fileName, double ageSeconds)
    {
        lock (_lock)
        {
            _lastHealthy = DateTime.UtcNow;
            _status      = "HEALTHY";
            _details     = $"'{Path.GetFileName(fileName)}' is {ageSeconds:F0}s old — within threshold.";
        }
    }

    public void RecordDown(string reason)
    {
        lock (_lock)
        {
            _status  = "DOWN";
            _details = reason;
        }
    }

    public (string Status, string Details, DateTime? LastHealthy) Get()
    {
        lock (_lock) return (_status, _details, _lastHealthy);
    }
}
