namespace HeartBeatProject.server.Configuration;

public sealed class AlertOptions
{
    public const string Section = "Alerts";

    // Email (SMTP)
    public bool   EnableEmail { get; init; }
    public string SmtpServer  { get; init; } = string.Empty;
    public int    Port        { get; init; } = 587;
    public string From        { get; init; } = string.Empty;
    public string To          { get; init; } = string.Empty;
    public string Username    { get; init; } = string.Empty;
    public string Password    { get; init; } = string.Empty;
    public bool   EnableSsl   { get; init; } = true;

    // SNMP Trap
    public bool   EnableSnmp  { get; init; }
    public string SnmpHost    { get; init; } = string.Empty;
    public int    SnmpPort    { get; init; } = 162;
    public string Community   { get; init; } = "public";
}
