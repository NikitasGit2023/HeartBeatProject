using System.Diagnostics;
using HeartBeatProject.Server.Configuration;
using HeartBeatProject.Server.Logging;
using HeartBeatProject.Server.Services;
using HeartBeatProject.Server.Services.Alerts;
using HeartBeatProject.Tx.Services;
using NLog;
using NLog.Web;
using NLog.Targets.Syslog;
using NLog.Targets.Syslog.Settings;

// ── Phase 1: NLog + SyslogTarget before the DI container builds ──────────────
// earlyConfig reads appsettings.json directly so NLog can be fully wired up
// before WebApplication.CreateBuilder is called. Without this, startup errors
// and the SyslogTarget port/facility patch would happen after the first log
// events are emitted, causing them to be lost or sent to the wrong destination.
var earlyConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var syslogHost     = earlyConfig[$"{AlertOptions.Section}:SyslogHost"]     ?? string.Empty;
var syslogPortRaw  = earlyConfig[$"{AlertOptions.Section}:SyslogPort"]     ?? "514";
var syslogFacility = earlyConfig[$"{AlertOptions.Section}:SyslogFacility"] ?? "Local0";

// SyslogTarget.Server is the Layout "${gdc:item=SyslogHost}" so the host must be
// written to GDC before the first log event. SyslogSender also updates this key
// at alert time so runtime changes via POST /api/settings take effect immediately.
GlobalDiagnosticsContext.Set("SyslogHost", syslogHost);

var logStore       = new InMemoryLogStore();
var nlogConfigPath = Path.Combine(AppContext.BaseDirectory, "nlog.config");
LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath);

// SyslogTarget.Port and .Facility cannot be set in nlog.config XML because they
// come from appsettings.json; patch the already-loaded target object directly.
var syslogTarget = LogManager.Configuration?.FindTargetByName<SyslogTarget>("syslog");
if (syslogTarget is not null)
{
    if (int.TryParse(syslogPortRaw, out var syslogPort) && syslogPort > 0)
        syslogTarget.MessageSend.Udp.Port = syslogPort;

    if (Enum.TryParse<Facility>(syslogFacility, ignoreCase: true, out var facility))
        syslogTarget.MessageCreation.Facility = facility;
}

// InMemoryNLogTarget wraps a runtime object (logStore) so it must be registered
// in code — it cannot be expressed in nlog.config XML.
var dashboardTarget = new InMemoryNLogTarget(logStore);
LogManager.Configuration!.AddTarget(dashboardTarget);
LogManager.Configuration.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, dashboardTarget, "*");
LogManager.ReconfigExistingLoggers();

// ── Phase 2: DI container + ASP.NET pipeline ─────────────────────────────────
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args            = args,
    // Must point to the executable directory. When running as a Windows Service the
    // default working directory is System32, which prevents UseBlazorFrameworkFiles()
    // from locating the wwwroot/_framework assets.
    ContentRootPath = AppContext.BaseDirectory
});

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection(HeartbeatOptions.Section));
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection(AlertOptions.Section));

builder.Services.AddSingleton<ILogStore>(logStore);
builder.Services.AddSingleton<RuntimeSettingsStore>();
builder.Services.AddSingleton<TxOperationalState>();
builder.Services.AddSingleton<IHeartbeatStatusService, TxHeartbeatStatusService>();
builder.Services.AddSingleton<IHeartbeatFileGenerator, HeartbeatFileGenerator>();
builder.Services.AddSingleton<SmtpAlertService>();
builder.Services.AddSingleton<ISnmpTrapSender, SnmpTrapSender>();
builder.Services.AddSingleton<ISyslogSender, SyslogSender>();
builder.Services.AddSingleton<IAlertService, CompositeAlertService>();
builder.Services.AddHostedService<HeartbeatTxService>();

builder.Services.AddControllers()
    .AddApplicationPart(typeof(HeartBeatProject.Server.Controllers.HeartbeatController).Assembly);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
