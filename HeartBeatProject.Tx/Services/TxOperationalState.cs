namespace HeartBeatProject.Tx.Services;

public sealed class TxOperationalState
{
    private readonly object _lock = new();
    private string    _status      = "STARTING";
    private string    _details     = "Service is starting up.";
    private DateTime? _lastSuccess;

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _lastSuccess = DateTime.UtcNow;
            _status      = "RUNNING";
            _details     = $"File written at {_lastSuccess.Value:HH:mm:ss} UTC.";
        }
    }

    public void RecordFailure(bool pathIssue, string errorMessage)
    {
        lock (_lock)
        {
            _status  = pathIssue ? "DEGRADED" : "ERROR";
            _details = pathIssue
                ? $"Output path unavailable: {errorMessage}"
                : $"File generation failed: {errorMessage}";
        }
    }

    public (string Status, string Details, DateTime? LastSuccess) Get()
    {
        lock (_lock) return (_status, _details, _lastSuccess);
    }
}
