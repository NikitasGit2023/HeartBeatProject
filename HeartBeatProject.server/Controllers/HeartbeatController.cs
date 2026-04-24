using HeartBeatProject.server.Logging;
using HeartBeatProject.server.Services;
using HeartBeatProject.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HeartBeatProject.server.Controllers;

[ApiController]
[Route("api")]
public sealed class HeartbeatController : ControllerBase
{
    private const string PasswordMask = "********";

    private readonly IHeartbeatStatusService _statusService;
    private readonly RuntimeSettingsStore _settingsStore;
    private readonly ILogStore _logStore;
    private readonly ILogger<HeartbeatController> _logger;

    public HeartbeatController(
        IHeartbeatStatusService statusService,
        RuntimeSettingsStore settingsStore,
        ILogStore logStore,
        ILogger<HeartbeatController> logger)
    {
        _statusService = statusService;
        _settingsStore = settingsStore;
        _logStore      = logStore;
        _logger        = logger;
    }

    [HttpGet("status")]
    public ActionResult<StatusDto> GetStatus() => Ok(_statusService.GetStatus());

    [HttpGet("settings")]
    public ActionResult<SettingsDto> GetSettings()
    {
        var dto = _settingsStore.Get();

        // Never return the real password over the API.
        if (!string.IsNullOrEmpty(dto.Password))
            dto.Password = PasswordMask;

        return Ok(dto);
    }

    [HttpPost("settings")]
    public IActionResult UpdateSettings([FromBody] SettingsDto dto)
    {
        // If the client echoed back the mask placeholder, keep the existing stored password
        // so the user doesn't need to re-enter it on every settings save.
        if (dto.Password == PasswordMask)
            dto.Password = _settingsStore.Get().Password;

        try
        {
            _settingsStore.Update(dto);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Settings update rejected: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("logs")]
    public ActionResult<IReadOnlyList<LogEntryDto>> GetLogs() => Ok(_logStore.GetRecent());
}
