using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;
/****************************************************************************
   Purpose      : 인앱 와치독 서비스 — 하트비트+종료신호+런처+상태조회 오케스트레이션.
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// <see cref="IConnectionWatchdog"/> 구현. 앱 부팅 시 하트비트 기록(<see cref="HeartbeatWriter"/>),
/// 정상종료 신호 준비(<see cref="ShutdownSignal"/>), 와치독 실행파일 기동(<see cref="WatchdogLauncher"/>),
/// 상태 조회(<see cref="WatchdogStatusClient"/>)를 조립한다.
/// <para>스레드: <see cref="Start"/>/<see cref="ExecuteAsync"/>는 부팅 시퀀스(UI 스레드 기대)에서 호출.
/// 하트비트는 내부적으로 UI Dispatcher에 마샬링된다.</para>
/// <para>RISK-02: <see cref="Start"/>는 전체 예외를 격리하여 와치독 실패가 앱 부팅을 저해하지 않는다.</para>
/// </summary>
public sealed class ConnectionWatchdog : IConnectionWatchdog
{
    #region - Ctors -
    public ConnectionWatchdog(WatchdogClientOptions options, IClock clock, ILogService? log = null)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log;
    }
    #endregion
    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default)
    {
        Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        Dispose();
        return Task.CompletedTask;
    }
    #endregion
    #region - IConnectionWatchdog -
    public void Start()
    {
        if (_started || _disposed) return;
        try // RISK-02 — 부팅 저해 방지 격리
        {
            if (!_opt.Enabled)
            {
                _log?.Info("[Watchdog] 비활성(Enabled=false) — 감시 시작 생략.");
                return;
            }

            _shutdown = new ShutdownSignal(_log);
            _shutdown.Start();

            _heartbeat = new HeartbeatWriter(_clock, _log, _opt.HeartbeatIntervalMs);
            _heartbeat.Start();

            _launcher = new WatchdogLauncher(_log, _opt);
            _launcher.EnsureRunning();
            // 상호감시 — 와치독이 죽으면 앱이 재기동(뮤텍스 확인, 30초 주기 백그라운드 점검)
            _relaunchTimer = new Timer(_ => SafeEnsureRunning(), null, RELAUNCH_CHECK_MS, RELAUNCH_CHECK_MS);

            _statusClient = new WatchdogStatusClient(_log, _clock);

            _started = true;
            _log?.Info("[Watchdog] 인앱 감시 시작 완료.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[Watchdog] Start 실패(격리됨, 앱 부팅 계속): {ex}");
        }
    }

    public WatchdogStatus? QueryStatus()
    {
        return _started ? _statusClient?.Query() : null;
    }

    /// <summary>
    /// 사용자 의도 종료 확정 시 호출 — 와치독이 재시작하지 않도록 graceful 신호를 즉시 발화한다
    /// (종료 이벤트 Set + app.pid sentinel 삭제). 이후 정상 종료 경로의 <see cref="Dispose"/>(StopAsync)와
    /// 중복 호출돼도 무해하다. 비활성/미시작 시 no-op.
    /// </summary>
    public void NotifyIntentionalShutdown()
    {
        try { _shutdown?.SignalGraceful(); } catch { }
        _log?.Info("[Watchdog] 의도적 종료 신호 발화 — 와치독 재시작 방지.");
    }

    /// <summary>
    /// 런타임 감시 중지 — 실행 중 와치독을 graceful 종료 유도하고 앱측 재기동/하트비트를 중단한다.
    /// 옵션 Enabled=false로 재기동 타이머·다음 부팅도 비활성. 이후 <see cref="Start"/>로 재활성 가능.
    /// </summary>
    public void StopWatching()
    {
        _opt.Enabled = false;                                    // 재기동 타이머·다음 부팅 차단
        try { _relaunchTimer?.Dispose(); _relaunchTimer = null; } catch { }
        try { _shutdown?.SignalGraceful(); } catch { }           // 실행 중 와치독 → graceful 종료
        try { _shutdown?.Dispose(); _shutdown = null; } catch { }
        try { _heartbeat?.Dispose(); _heartbeat = null; } catch { }
        try { _statusClient = null; } catch { }
        _started = false;                                        // 재활성(Start) 가능하게
        _log?.Info("[Watchdog] 런타임 감시 중지(사용자 비활성) — 와치독 종료 유도 + 재기동 차단.");
    }

    private void SafeEnsureRunning()
    {
        try
        {
            if (!_disposed && _opt.Enabled) _launcher?.EnsureRunning();
        }
        catch (Exception ex)
        {
            _log?.Warning($"[Watchdog] 상호감시 재점검 예외: {ex.Message}");
        }
    }
    #endregion
    #region - IDisposable -
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _relaunchTimer?.Dispose(); } catch { }
        try { _shutdown?.Dispose(); } catch { }   // 정상종료 신호 Set
        try { _heartbeat?.Dispose(); } catch { }  // UI 타이머 정지 + MMF 해제
        _log?.Info("[Watchdog] 인앱 감시 종료(정상).");
    }
    #endregion
    #region - Attributes -
    private readonly WatchdogClientOptions _opt;
    private readonly IClock _clock;
    private readonly ILogService? _log;

    private HeartbeatWriter? _heartbeat;
    private ShutdownSignal? _shutdown;
    private WatchdogLauncher? _launcher;
    private WatchdogStatusClient? _statusClient;
    private Timer? _relaunchTimer;
    private volatile bool _started;
    private volatile bool _disposed;

    private const int RELAUNCH_CHECK_MS = 30000;
    #endregion
}
