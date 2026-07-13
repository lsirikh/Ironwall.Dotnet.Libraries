using System;
using System.IO.MemoryMappedFiles;
using System.Windows;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 하트비트 공유메모리 기록(UI 스레드 DispatcherTimer — 프리즈 감지 핵심).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 대상 프로세스(자신)의 생존 하트비트를 공유메모리에 주기적으로 기록한다.
/// <para>
/// ⚠ <b>정확성 핵심(RISK-01)</b>: 하트비트는 반드시 <see cref="DispatcherTimer"/>(UI 스레드,
/// <see cref="DispatcherPriority.Background"/>)로 기록한다. <c>System.Timers.Timer</c> 등 스레드풀
/// 타이머를 쓰면 UI Dispatcher가 데드락(프리즈)이어도 계속 발화하여 "정상"으로 <b>오판</b>한다.
/// DispatcherTimer는 UI 메시지 루프에 큐잉되므로 UI가 멈추면 하트비트도 멈춰 정확히 감지된다.
/// </para>
/// UI Dispatcher가 없는(헤드리스/테스트) 환경에서는 타이머를 시작하지 않는다.
/// </summary>
internal sealed class HeartbeatWriter : IDisposable
{
    #region - Ctors -
    public HeartbeatWriter(IClock clock, ILogService? log, int intervalMs)
    {
        _clock = clock;
        _log = log;
        _intervalMs = Math.Max(500, intervalMs);
        _pid = Environment.ProcessId;
    }
    #endregion
    #region - Processes -
    public void Start()
    {
        _mmf = MemoryMappedFile.CreateOrOpen(WatchdogNaming.HeartbeatMap(_pid), WatchdogHeartbeat.Size);
        _accessor = _mmf.CreateViewAccessor(0, WatchdogHeartbeat.Size);
        WriteBeat(); // 초기 1회 즉시 기록

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _log?.Warning("[Watchdog] UI Dispatcher 없음 — 하트비트 타이머 미시작(프리즈 감지 비활성, 헤드리스/테스트로 간주).");
            return;
        }

        // 반드시 UI 스레드에서 타이머 생성/시작 (프리즈 감지 정확성).
        dispatcher.Invoke(() =>
        {
            _timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(_intervalMs),
                DispatcherPriority.Background,
                (_, _) => WriteBeat(),
                dispatcher);
            _timer.Start();
        });
    }

    private void WriteBeat()
    {
        var acc = _accessor;
        if (_disposed || acc == null) return;
        try
        {
            long epoch = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            acc.Write(WatchdogHeartbeat.OffsetEpochMs, epoch);   // 오프셋 0, 8B 정렬 → 원자적
            acc.Write(WatchdogHeartbeat.OffsetPid, _pid);
            acc.Write(WatchdogHeartbeat.OffsetState, WatchdogHeartbeat.StateHealthy);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Watchdog] 하트비트 기록 실패(일시적): {ex.Message}");
        }
    }
    #endregion
    #region - IDisposable -
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            var t = _timer;
            if (t != null)
                t.Dispatcher.Invoke(() => t.Stop());
        }
        catch { /* 종료 경합 무시 */ }
        try { _accessor?.Dispose(); } catch { }
        try { _mmf?.Dispose(); } catch { }
    }
    #endregion
    #region - Attributes -
    private readonly IClock _clock;
    private readonly ILogService? _log;
    private readonly int _intervalMs;
    private readonly int _pid;

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private DispatcherTimer? _timer;
    private volatile bool _disposed;
    #endregion
}
