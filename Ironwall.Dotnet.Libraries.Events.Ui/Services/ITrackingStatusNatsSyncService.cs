using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : TrackingStatusNatsSyncService 인터페이스
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// TRACKING_STATUS 수신 서비스 — 현재 로그-only stub. 오버레이(targets[]) 처리는 FR-15 후속.
/// </summary>
public interface ITrackingStatusNatsSyncService : IService
{
    Task StartService(CancellationToken token = default);
}
