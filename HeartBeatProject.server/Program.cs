using HeartBeatProject.server.Services;
using HeartBeatProject.server.Services.Alerts;
using Microsoft.AspNetCore.Components.WebAssembly.Server;

var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<IHeartbeatFileGenerator, HeartbeatFileGenerator>();
builder.Services.AddSingleton<IAlertService, SmtpAlertService>();
builder.Services.AddHostedService<HeartbeatTxService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


//// Configure the HTTP request pipeline.

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
