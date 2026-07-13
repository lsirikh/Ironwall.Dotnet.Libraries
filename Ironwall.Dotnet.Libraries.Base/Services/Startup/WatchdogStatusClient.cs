using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 와치독 상태 Named Pipe 조회(Setup 패널 폴링) + DEGRADED fallback.
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 와치독의 상태 파이프에 접속하여 <see cref="WatchdogStatus"/>를 조회한다.
/// 파이프 미연결 시(와치독 미실행/DEGRADED로 파이프 중단) DEGRADED 플래그 파일을 fallback으로 확인한다.
/// </summary>
internal sealed class WatchdogStatusClient
{
    #region - Ctors -
    public WatchdogStatusClient(ILogService? log, IClock clock)
    {
        _log = log;
        _clock = clock;
        _sessionId = Process.GetCurrentProcess().SessionId;
    }
    #endregion
    #region - Processes -
    public WatchdogStatus? Query()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", WatchdogNaming.StatusPipe(_sessionId), PipeDirection.In);
            pipe.Connect(500); // 500ms 타임아웃
            using var reader = new StreamReader(pipe, Encoding.UTF8);
            var json = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json))
                return DegradedFallback();
            return JsonSerializer.Deserialize<WatchdogStatus>(json) ?? DegradedFallback();
        }
        catch (TimeoutException)
        {
            return DegradedFallback();
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Watchdog] 상태 조회 실패: {ex.Message}");
            return DegradedFallback();
        }
    }

    private WatchdogStatus? DegradedFallback()
    {
        try
        {
            if (File.Exists(WatchdogNaming.DegradedFlag(_sessionId)))
                return new WatchdogStatus(WatchdogState.Degraded, false, null, 0, _clock.UtcNow, "degraded flag(파이프 미연결)");
        }
        catch { }
        return null;
    }
    #endregion
    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IClock _clock;
    private readonly int _sessionId;
    #endregion
}
