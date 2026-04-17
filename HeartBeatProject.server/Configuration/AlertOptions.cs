namespace HeartBeatProject.server.Configuration;

public sealed class AlertOptions
{
    public const string Section = "Alerts";

    public bool   EnableEmail { get; init; }
    public string SmtpServer  { get; init; } = string.Empty;
    public int    Port        { get; init; } = 25;
    public string From        { get; init; } = string.Empty;
    public string To          { get; init; } = string.Empty;
}
