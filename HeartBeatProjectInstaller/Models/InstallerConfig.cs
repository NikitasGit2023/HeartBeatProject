namespace HeartBeatProjectInstaller.Models;

public class InstallerConfig
{
    public string Mode { get; set; } = "TX";
    public string FolderPath { get; set; } = "";
    public int IntervalSeconds { get; set; } = 30;
    public int CheckIntervalSeconds { get; set; } = 30;
    public int ThresholdSeconds { get; set; } = 90;
    public bool   EnableEmail { get; set; } = false;
    public string SmtpServer  { get; set; } = "";
    public int    SmtpPort    { get; set; } = 587;
    public string From        { get; set; } = "";
    public string To          { get; set; } = "";
}
