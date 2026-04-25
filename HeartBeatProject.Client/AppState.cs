namespace HeartBeatProject;

public sealed class AppState
{
    public string Mode     { get; private set; } = string.Empty;
    public bool   IsLoaded { get; private set; }

    public event Action? OnChange;

    public void SetMode(string mode)
    {
        if (IsLoaded && Mode == mode) return;
        Mode     = mode;
        IsLoaded = true;
        OnChange?.Invoke();
    }

    public bool IsTx => Mode.Equals("TX", StringComparison.OrdinalIgnoreCase);
    public bool IsRx => Mode.Equals("RX", StringComparison.OrdinalIgnoreCase);
}
