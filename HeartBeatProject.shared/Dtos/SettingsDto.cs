namespace HeartBeatProject.Shared.Dtos;

public sealed class SettingsDto
{
    public string FolderPath           { get; set; } = string.Empty;
    public int    IntervalSeconds      { get; set; }
    public int    CheckIntervalSeconds { get; set; }
    public int    ThresholdSeconds     { get; set; }
    public bool   EnableEmail          { get; set; }
    public string SmtpServer           { get; set; } = string.Empty;
    public int    Port                 { get; set; }
    public string From                 { get; set; } = string.Empty;
    public string To                   { get; set; } = string.Empty;
}
