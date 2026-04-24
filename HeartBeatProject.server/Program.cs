using System.Diagnostics;
using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Logging;
using HeartBeatProject.server.Services;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Web;
using NLog.Targets.Syslog;
using NLog.Targets.Syslog.Settings;

//// Read configuration early — NLog and mode routing both need values before the host is built.
var earlyConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

//// Validate Mode immediately — fail fast before any further initialisation.
var mode = earlyConfig[$"{HeartbeatOptions.Section}:Mode"]?.Trim() ?? string.Empty;

if (!mode.Equals("TX", StringComparison.OrdinalIgnoreCase) &&
    !mode.Equals("RX", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Invalid or missing Heartbeat:Mode '{mode}' in appsettings.json. " +
        $"Expected 'TX' or 'RX'. Startup aborted.");
}

//// Syslog GDC — must be set before nlog.config is loaded.
var syslogHost     = earlyConfig[$"{AlertOptions.Section}:SyslogHost"]     ?? string.Empty;
var syslogPortRaw  = earlyConfig[$"{AlertOptions.Section}:SyslogPort"]     ?? "514";
var syslogFacility = earlyConfig[$"{AlertOptions.Section}:SyslogFacility"] ?? "Local0";

GlobalDiagnosticsContext.Set("SyslogHost", syslogHost);

//// Logging — configure NLog before the host is built.
var logStore      = new InMemoryLogStore();
var nlogConfigPath = Path.Combine(AppContext.BaseDirectory, "nlog.config");
LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath);

var syslogTarget = LogManager.Configuration?.FindTargetByName<SyslogTarget>("syslog");
if (syslogTarget is not null)
{
    if (int.TryParse(syslogPortRaw, out var syslogPort) && syslogPort > 0)
        syslogTarget.MessageSend.Udp.Port = syslogPort;

    if (Enum.TryParse<Facility>(syslogFacility, ignoreCase: true, out var facility))
        syslogTarget.MessageCreation.Facility = facility;
}

var dashboardTarget = new InMemoryNLogTarget(logStore);
LogManager.Configuration!.AddTarget(dashboardTarget);
LogManager.Configuration.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, dashboardTarget, "*");
LogManager.ReconfigExistingLoggers();

var builder = WebApplication.CreateBuilder(args);

// Ensure appsettings.json is always resolved relative to the executable,
// not the process working directory (critical when running as a Windows Service).
builder.Host.UseContentRoot(AppContext.BaseDirectory);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

//// Options
builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection(HeartbeatOptions.Section));
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection(AlertOptions.Section));

builder.Services.AddSingleton<ILogStore>(logStore);

//// Core services — registered regardless of mode.
builder.Services.AddSingleton<RuntimeSettingsStore>();
builder.Services.AddSingleton<IHeartbeatStatusService, HeartbeatStatusService>();
builder.Services.AddSingleton<IHeartbeatFileGenerator, HeartbeatFileGenerator>();
builder.Services.AddSingleton<SmtpAlertService>();
builder.Services.AddSingleton<ISnmpTrapSender, SnmpTrapSender>();
builder.Services.AddSingleton<ISyslogSender, SyslogSender>();
builder.Services.AddSingleton<IAlertService, CompositeAlertService>();

//// Background service — only the one that matches Mode is registered.
if (mode.Equals("TX", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHostedService<HeartbeatTxService>();
else
    builder.Services.AddHostedService<HeartbeatRxService>();

//// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = app.Configuration["Urls"] ?? "http://localhost:5000";
    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
});

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
