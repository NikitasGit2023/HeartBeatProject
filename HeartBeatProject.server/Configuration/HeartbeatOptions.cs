namespace HeartBeatProject.server.Configuration;

public sealed class HeartbeatOptions
{
    public const string Section = "Heartbeat";

    public string Mode              { get; init; } = string.Empty;
    public string FolderPath        { get; init; } = "C:\\Heartbeat";
    public string FileNamePrefix    { get; init; } = "heartbeat";
    public int    IntervalSeconds   { get; init; } = 30;
    public bool   OverwriteExisting { get; init; } = true;
    public string LogFolderPath     { get; init; } = "C:\\Heartbeat\\Logs";
    public int    CheckIntervalSeconds { get; init; } = 30;
    public int    ThresholdSeconds     { get; init; } = 60;
}
