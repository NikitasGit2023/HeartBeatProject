using HeartBeatProject.server.Configuration;
using HeartBeatProject.server.Logging;
using HeartBeatProject.server.Services;
using HeartBeatProject.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Controllers;

[ApiController]
[Route("api")]
public sealed class HeartbeatController : ControllerBase
{
    private static readonly DateTime StartTime = DateTime.Now;

    private readonly IOptions<HeartbeatOptions> _heartbeatOptions;
    private readonly RuntimeSettingsStore _settings;
    private readonly ILogStore _logStore;

    public HeartbeatController(
        IOptions<HeartbeatOptions> heartbeatOptions,
        RuntimeSettingsStore settings,
        ILogStore logStore)
    {
        _heartbeatOptions = heartbeatOptions;
        _settings         = settings;
        _logStore         = logStore;
    }

    [HttpGet("status")]
    public ActionResult<StatusDto> GetStatus()
    {
        var opts          = _settings.Get();
        DateTime? lastHb  = null;
        string status;

        try
        {
            var latestFile = Directory.Exists(opts.FolderPath)
                ? Directory.EnumerateFiles(opts.FolderPath, "*.txt")
                           .Select(f => new FileInfo(f))
                           .OrderByDescending(f => f.LastWriteTime)
                           .FirstOrDefault()
                : null;

            if (latestFile is null)
            {
                status = "DOWN";
            }
            else
            {
                lastHb = latestFile.LastWriteTime;
                status = (DateTime.Now - latestFile.LastWriteTime).TotalSeconds <= opts.ThresholdSeconds
                    ? "HEALTHY"
                    : "DOWN";
            }
        }
        catch
        {
            status = "DOWN";
        }

        return Ok(new StatusDto
        {
            Mode            = _heartbeatOptions.Value.Mode,
            Status          = status,
            LastHeartbeat   = lastHb,
            Uptime          = DateTime.Now - StartTime,
            IntervalSeconds = opts.IntervalSeconds
        });
    }

    [HttpGet("settings")]
    public ActionResult<SettingsDto> GetSettings() => Ok(_settings.Get());

    [HttpPost("settings")]
    public IActionResult UpdateSettings([FromBody] SettingsDto dto)
    {
        _settings.Update(dto);
        return NoContent();
    }

    [HttpGet("logs")]
    public ActionResult<IReadOnlyList<LogEntryDto>> GetLogs() => Ok(_logStore.GetRecent());
}
