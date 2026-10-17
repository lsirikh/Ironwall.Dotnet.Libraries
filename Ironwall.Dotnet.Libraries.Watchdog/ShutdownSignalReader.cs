using Ironwall.Dotnet.Libraries.Base.Services.Startup;

namespace Ironwall.Dotnet.Libraries.Watchdog;
/****************************************************************************
   Purpose      : 정상 종료 신호(EventWaitHandle) 판독.
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 대상 앱이 정상 종료 시 Set 하는 이벤트(<see cref="WatchdogNaming.ShutdownEvent"/>)를 확인한다.
/// 신호 상태면 사용자 의도 종료 → 재시작하지 않는다.
/// </summary>
internal sealed class ShutdownSignalReader
{
    public bool IsGracefulRequested(int targetPid)
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(WatchdogNaming.ShutdownEvent(targetPid), out var handle))
            {
                using (handle)
                    return handle.WaitOne(0);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
