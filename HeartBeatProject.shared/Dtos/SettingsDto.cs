namespace HeartBeatProject.Shared.Dtos;

public sealed class SettingsDto
{
    // Heartbeat
    public string FolderPath           { get; set; } = string.Empty;
    public int    IntervalSeconds      { get; set; }
    public bool   OverwriteExisting    { get; set; }
    public int    CheckIntervalSeconds { get; set; }
    public int    ThresholdSeconds     { get; set; }

    // Email (SMTP)
    public bool   EnableEmail  { get; set; }
    public string SmtpServer   { get; set; } = string.Empty;
    public int    Port         { get; set; }
    public string From         { get; set; } = string.Empty;
    public string To           { get; set; } = string.Empty;
    public string Username     { get; set; } = string.Empty;
    public string Password     { get; set; } = string.Empty;
    public bool   EnableSsl    { get; set; } = true;

    // SNMP Trap
    public bool   EnableSnmp  { get; set; }
    public string SnmpHost    { get; set; } = string.Empty;
    public int    SnmpPort    { get; set; } = 162;
    public string Community   { get; set; } = "public";

    // Syslog (RFC 5424, UDP)
    public bool   EnableSyslog   { get; set; }
    public string SyslogHost     { get; set; } = string.Empty;
    public int    SyslogPort     { get; set; } = 514;
    public string SyslogFacility { get; set; } = "Local0";
}
