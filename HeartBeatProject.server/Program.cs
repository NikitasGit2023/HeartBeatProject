using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Logging;
using HeartBeatProject.server.Services;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.AspNetCore.Components.WebAssembly.Server;

var builder = WebApplication.CreateBuilder(args);

//// Options
builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection(HeartbeatOptions.Section));
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection(AlertOptions.Section));

//// In-memory log store — created early so the provider can be wired before DI build
var logStore = new InMemoryLogStore();
builder.Services.AddSingleton<ILogStore>(logStore);
builder.Logging.AddProvider(new InMemoryLoggerProvider(logStore));

//// Services
builder.Services.AddSingleton<RuntimeSettingsStore>();
builder.Services.AddSingleton<IHeartbeatFileGenerator, HeartbeatFileGenerator>();
builder.Services.AddSingleton<IAlertService, SmtpAlertService>();
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

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// THIS makes Server open Client UI
app.MapFallbackToFile("index.html");

app.Run();
