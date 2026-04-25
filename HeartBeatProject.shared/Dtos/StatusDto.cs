namespace HeartBeatProject.Shared.Dtos;

public sealed class StatusDto
{
    public string    Mode            { get; set; } = string.Empty;
    public string    Status          { get; set; } = string.Empty;
    public string    Details         { get; set; } = string.Empty;
    public DateTime? LastHeartbeat   { get; set; }
    public TimeSpan  Uptime          { get; set; }
    public int       IntervalSeconds { get; set; }
}
