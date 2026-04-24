using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.Server.Services;

public interface IHeartbeatStatusService
{
    StatusDto GetStatus();
}
