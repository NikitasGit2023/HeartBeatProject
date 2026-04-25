using System.Diagnostics;
using HeartBeatProject.Server.Configuration;
using HeartBeatProject.Server.Logging;
using HeartBeatProject.Server.Services;
using HeartBeatProject.Server.Services.Alerts;
using HeartBeatProject.Rx.Services;
using NLog;
using NLog.Web;
using NLog.Targets.Syslog;
using NLog.Targets.Syslog.Settings;

var earlyConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var syslogHost     = earlyConfig[$"{AlertOptions.Section}:SyslogHost"]     ?? string.Empty;
var syslogPortRaw  = earlyConfig[$"{AlertOptions.Section}:SyslogPort"]     ?? "514";
var syslogFacility = earlyConfig[$"{AlertOptions.Section}:SyslogFacility"] ?? "Local0";

GlobalDiagnosticsContext.Set("SyslogHost", syslogHost);

var logStore       = new InMemoryLogStore();
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

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args            = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection(HeartbeatOptions.Section));
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection(AlertOptions.Section));

builder.Services.AddSingleton<ILogStore>(logStore);
builder.Services.AddSingleton<RuntimeSettingsStore>();
builder.Services.AddSingleton<RxOperationalState>();
builder.Services.AddSingleton<IHeartbeatStatusService, RxHeartbeatStatusService>();
builder.Services.AddSingleton<SmtpAlertService>();
builder.Services.AddSingleton<ISnmpTrapSender, SnmpTrapSender>();
builder.Services.AddSingleton<ISyslogSender, SyslogSender>();
builder.Services.AddSingleton<IAlertService, CompositeAlertService>();
builder.Services.AddHostedService<HeartbeatRxService>();

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
    var url = app.Configuration["Urls"] ?? "http://localhost:5001";
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
