using System.Diagnostics;
using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Logging;
using HeartBeatProject.server.Services;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using NLog;
using NLog.Web;

//// Logging — configure NLog before the host is built
var logStore = new InMemoryLogStore();
var nlogConfigPath = Path.Combine(AppContext.BaseDirectory, "nlog.config");
LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath);

var dashboardTarget = new InMemoryNLogTarget(logStore);
LogManager.Configuration.AddTarget(dashboardTarget);
LogManager.Configuration.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, dashboardTarget, "*");
LogManager.ReconfigExistingLoggers();

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();

//// Options
builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection(HeartbeatOptions.Section));
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection(AlertOptions.Section));

builder.Services.AddSingleton<ILogStore>(logStore);

//// Services
builder.Services.AddSingleton<RuntimeSettingsStore>();
builder.Services.AddSingleton<IHeartbeatStatusService, HeartbeatStatusService>();
builder.Services.AddSingleton<IHeartbeatFileGenerator, HeartbeatFileGenerator>();
builder.Services.AddSingleton<SmtpAlertService>();
builder.Services.AddSingleton<ISnmpTrapSender, SnmpTrapSender>();
builder.Services.AddSingleton<IAlertService, CompositeAlertService>();
builder.Services.AddHostedService<HeartbeatTxService>();
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

// Open browser once server is ready
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

// THIS makes Server open Client UI
app.MapFallbackToFile("index.html");

app.Run();
