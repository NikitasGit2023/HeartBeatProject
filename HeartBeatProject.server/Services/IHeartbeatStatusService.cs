using HeartBeatProject.Shared.Dtos;

namespace HeartBeatProject.server.Services;

public interface IHeartbeatStatusService
{
    StatusDto GetStatus();
}
