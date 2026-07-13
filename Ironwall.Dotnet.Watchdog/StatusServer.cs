using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Ironwall.Dotnet.Libraries.Base.Services.Startup;

namespace Ironwall.Dotnet.Watchdog;
/****************************************************************************
   Purpose      : 상태 리포팅 Named Pipe 서버(Setup 패널 폴링 대상).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 세션 파이프(<see cref="WatchdogNaming.StatusPipe"/>)로 현재 <see cref="WatchdogStatus"/>를
/// 요청 시마다 1회 발행한다. 앱은 주기적으로 접속해 상태를 폴링한다.
/// </summary>
internal sealed class StatusServer : IDisposable
{
    #region - Ctors -
    public StatusServer(Func<WatchdogStatus> snapshot)
    {
        _snapshot = snapshot;
        _sessionId = Process.GetCurrentProcess().SessionId;
    }
    #endregion
    #region - Processes -
    public void Start(CancellationToken token)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _task = Task.Run(() => ServeAsync(_cts.Token));
    }

    private async Task ServeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    WatchdogNaming.StatusPipe(_sessionId),
                    PipeDirection.Out, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                var json = JsonSerializer.Serialize(_snapshot());
                var bytes = Encoding.UTF8.GetBytes(json);
                await server.WriteAsync(bytes, ct).ConfigureAwait(false);
                await server.FlushAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                WatchdogLog.Warn($"상태 서버 예외: {ex.Message}");
                try { await Task.Delay(500, ct).ConfigureAwait(false); } catch { break; }
            }
        }
    }
    #endregion
    #region - IDisposable -
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _task?.Wait(1000); } catch { }
        try { _cts?.Dispose(); } catch { }
    }
    #endregion
    #region - Attributes -
    private readonly Func<WatchdogStatus> _snapshot;
    private readonly int _sessionId;
    private CancellationTokenSource? _cts;
    private Task? _task;
    #endregion
}
