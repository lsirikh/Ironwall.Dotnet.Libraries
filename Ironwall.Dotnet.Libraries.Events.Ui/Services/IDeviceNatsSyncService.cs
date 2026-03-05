using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS SYNC_DEVICE 구독 서비스 인터페이스
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// NATS SYNC_DEVICE 메시지를 구독하여 Device Status 변경을 실시간으로 처리합니다.
/// </summary>
public interface IDeviceNatsSyncService : IService
{
}
