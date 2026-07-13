using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 정상 종료 신호(EventWaitHandle) + app.pid sentinel 기록.
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 사용자 의도 종료와 크래시를 구분하기 위한 신호. 앱 시작 시 비신호 이벤트를 생성하고,
/// <see cref="Dispose"/>(정상 종료 경로)에서 Set 한다. 와치독은 이 이벤트가 신호이면 재시작하지 않는다.
/// 크래시 시에는 Dispose가 호출되지 않아 이벤트가 비신호로 남고 → 와치독이 재시작한다.
/// </summary>
internal sealed class ShutdownSignal : IDisposable
{
    #region - Ctors -
    public ShutdownSignal(ILogService? log)
    {
        _log = log;
        _pid = Environment.ProcessId;
        _sessionId = Process.GetCurrentProcess().SessionId;
    }
    #endregion
    #region - Processes -
    public void Start()
    {
        _handle = new EventWaitHandle(false, EventResetMode.ManualReset, WatchdogNaming.ShutdownEvent(_pid));
        try
        {
            Directory.CreateDirectory(WatchdogNaming.DataDir);
            File.WriteAllText(WatchdogNaming.AppPidFile(_sessionId), _pid.ToString());
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Watchdog] app.pid sentinel 기록 실패: {ex.Message}");
        }
    }

    /// <summary>정상 종료 신호 Set + app.pid sentinel 제거.</summary>
    public void SignalGraceful()
    {
        try { _handle?.Set(); } catch { }
        try { File.Delete(WatchdogNaming.AppPidFile(_sessionId)); } catch { }
    }
    #endregion
    #region - IDisposable -
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SignalGraceful();
        try { _handle?.Dispose(); } catch { }
    }
    #endregion
    #region - Attributes -
    private readonly ILogService? _log;
    private readonly int _pid;
    private readonly int _sessionId;
    private EventWaitHandle? _handle;
    private volatile bool _disposed;
    #endregion
}
