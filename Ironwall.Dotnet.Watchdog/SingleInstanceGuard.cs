using System.Diagnostics;
using Ironwall.Dotnet.Libraries.Base.Services.Startup;

namespace Ironwall.Dotnet.Watchdog;
/****************************************************************************
   Purpose      : 세션당 와치독 단일 인스턴스 보장(뮤텍스).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 세션 로컬 뮤텍스로 와치독이 세션당 1개만 실행되도록 한다.
/// 앱의 런처는 이 뮤텍스 존재로 와치독 실행 여부를 판별한다(중복 기동 방지).
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly int _sessionId;
    private Mutex? _mutex;
    private bool _owned;

    public SingleInstanceGuard()
    {
        _sessionId = Process.GetCurrentProcess().SessionId;
    }

    /// <returns>true=획득 성공(이 인스턴스가 소유), false=이미 실행 중.</returns>
    public bool TryAcquire()
    {
        _mutex = new Mutex(true, WatchdogNaming.SingleInstanceMutex(_sessionId), out bool createdNew);
        _owned = createdNew;
        return createdNew;
    }

    public void Dispose()
    {
        try { if (_owned) _mutex?.ReleaseMutex(); } catch { }
        try { _mutex?.Dispose(); } catch { }
    }
}
