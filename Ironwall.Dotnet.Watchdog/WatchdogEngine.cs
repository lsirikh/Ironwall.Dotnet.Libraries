using System.Diagnostics;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Base.Services.Startup;
using Microsoft.Extensions.Hosting;

namespace Ironwall.Dotnet.Watchdog;
/****************************************************************************
   Purpose      : 감시 폴 루프 — 종료/크래시/프리즈 판정 + 재시작 정책 적용.
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 주기적으로 대상 상태를 평가한다: 정상종료(재시작 안 함) / 크래시(백오프 재시작) /
/// 프리즈(kill 후 재시작) / 정상. 정상종료는 app.pid sentinel 부재로 확정한다
/// (프로세스 사망 후 EventWaitHandle 소멸로 인한 크래시 오판 방지).
/// </summary>
internal sealed class WatchdogEngine : IHostedService, IDisposable
{
    #region - Ctors -
    public WatchdogEngine(WatchdogOptions opt, IClock clock, IHostApplicationLifetime lifetime)
    {
        _opt = opt;
        _clock = clock;
        _lifetime = lifetime;
        _guard = new ProcessGuard(opt);
        _heartbeat = new HeartbeatReader();
        _shutdown = new ShutdownSignalReader();
        _policy = new RestartPolicy(opt, clock);
        _status = new StatusServer(Snapshot);
        _sessionId = Process.GetCurrentProcess().SessionId;
    }
    #endregion
    #region - IHostedService -
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _status.Start(_cts.Token);
        ScheduledTaskInstaller.EnsureRegistered(Environment.ProcessPath ?? string.Empty); // FR-07 상호감시
        _loop = Task.Run(() => RunLoopAsync(_cts.Token));
        WatchdogLog.Info($"와치독 감시 시작: targetPid={_opt.TargetPid}, target='{_opt.TargetExePath}', poll={_opt.PollIntervalMs}ms, freeze={_opt.FreezeThresholdMs}ms");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try { _cts?.Cancel(); } catch { }
        if (_loop != null) { try { await _loop.ConfigureAwait(false); } catch { } }
        _status.Dispose();
        _heartbeat.Dispose();
        WatchdogLog.Info("와치독 감시 종료.");
    }
    #endregion
    #region - Loop -
    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { EvaluateOnce(); }
            catch (Exception ex) { WatchdogLog.Error($"평가 루프 예외: {ex.Message}"); }

            try { await Task.Delay(_opt.PollIntervalMs, ct).ConfigureAwait(false); }
            catch (TaskCanceledException) { break; }
        }
    }

    private void EvaluateOnce()
    {
        // discovery — 대상 미지정(예약작업 기동) 시 app.pid로 탐색
        if (_opt.TargetPid <= 0)
        {
            if (!_guard.TryDiscoverTarget()) { _state = WatchdogState.Unknown; return; }
        }

        int pid = _opt.TargetPid;

        // 1) 종료 중(살아있으나 이벤트 신호) — 조기 감지
        if (_shutdown.IsGracefulRequested(pid))
        {
            SetGracefulAndStop("shutdown-event");
            return;
        }

        // 2) 프로세스 생존?
        if (!_guard.IsAlive())
        {
            // 정상종료 확정: sentinel 부재 = 사용자 quit(프로세스 사망 후에도 지속되는 신호)
            if (IsGracefulBySentinel())
            {
                SetGracefulAndStop("sentinel-absent");
                return;
            }
            _state = WatchdogState.Dead;
            TryRestart("crash");
            return;
        }

        // 3) 하트비트 신선도
        var epoch = _heartbeat.ReadEpochMs(pid);
        bool hbOk;
        if (epoch == null)
        {
            hbOk = true; // 공유메모리 아직 없음(초기 유예) — 살아있으면 정상 간주
        }
        else
        {
            long ageMs = TimeUtil.ToUnixMs(_clock.UtcNow) - epoch.Value;
            hbOk = ageMs <= _opt.FreezeThresholdMs;
        }

        if (!hbOk)
        {
            _state = WatchdogState.Frozen;
            WatchdogLog.Warn($"프리즈 감지(pid={pid}) — kill 후 재시작");
            if (_guard.KillIfMatches()) TryRestart("freeze");
            return;
        }

        // 정상
        _policy.NotifyHealthy();
        _state = _policy.IsDegraded ? WatchdogState.Degraded : WatchdogState.Running;
        ClearDegradedFlag();
    }

    private void TryRestart(string reason)
    {
        if (!_policy.CanRestartNow())
        {
            if (_policy.IsDegraded)
            {
                _state = WatchdogState.Degraded;
                WriteDegradedFlag(reason);
                WatchdogLog.Error($"DEGRADED — 재시작 중단({reason}), count={_policy.RestartCount}");
            }
            return;
        }

        if (_guard.StartTarget())
        {
            _policy.RecordRestart();
            _heartbeat.Dispose(); // 새 PID로 재오픈 유도
            WatchdogLog.Info($"재시작({reason}) 완료 — newPid={_opt.TargetPid}, count={_policy.RestartCount}");
        }
    }
    #endregion
    #region - Helpers -
    private bool IsGracefulBySentinel()
    {
        try { return !File.Exists(WatchdogNaming.AppPidFile(_sessionId)); }
        catch { return false; }
    }

    private void SetGracefulAndStop(string reason)
    {
        _state = WatchdogState.GracefulStop;
        WatchdogLog.Info($"정상 종료 감지({reason}) — 와치독 종료(재시작 안 함).");
        try { _lifetime.StopApplication(); } catch { }
    }

    private WatchdogStatus Snapshot() =>
        new WatchdogStatus(
            _state,
            _state == WatchdogState.Running,
            _policy.LastRestartUtc,
            _policy.RestartCount,
            _clock.UtcNow,
            _state == WatchdogState.Degraded ? "크래시루프 — 재시작 중단" : null);

    private void WriteDegradedFlag(string reason)
    {
        try
        {
            Directory.CreateDirectory(WatchdogNaming.DataDir);
            File.WriteAllText(WatchdogNaming.DegradedFlag(_sessionId),
                $"{DateTime.Now:o} {reason} count={_policy.RestartCount}");
        }
        catch { }
    }

    private void ClearDegradedFlag()
    {
        try
        {
            var f = WatchdogNaming.DegradedFlag(_sessionId);
            if (File.Exists(f)) File.Delete(f);
        }
        catch { }
    }
    #endregion
    #region - IDisposable -
    public void Dispose()
    {
        try { _cts?.Dispose(); } catch { }
        _heartbeat.Dispose();
        _status.Dispose();
    }
    #endregion
    #region - Attributes -
    private readonly WatchdogOptions _opt;
    private readonly IClock _clock;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ProcessGuard _guard;
    private readonly HeartbeatReader _heartbeat;
    private readonly ShutdownSignalReader _shutdown;
    private readonly RestartPolicy _policy;
    private readonly StatusServer _status;
    private readonly int _sessionId;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile WatchdogState _state = WatchdogState.Unknown;
    #endregion
}
