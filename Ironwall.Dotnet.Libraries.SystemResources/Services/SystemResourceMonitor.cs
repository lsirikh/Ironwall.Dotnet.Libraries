using System;
using System.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.SystemResources.Models;

namespace Ironwall.Dotnet.Libraries.SystemResources.Services;

/****************************************************************************
   Purpose      : 시스템 리소스 모니터 구현 (fail-safe · 히스테리시스 · clamp)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// <see cref="ISystemResourceMonitor"/> 구현. 소비자가 <see cref="Sample"/>을 주기적으로(예: 1초
/// DispatcherTimer) 호출하면 프로브에서 원시값을 취득해 clamp(Turbo로 100% 초과 방지)·히스테리시스
/// 레벨 매핑을 적용한 불변 스냅샷을 발행한다.
/// <para>Fail-safe(FR-06): 프로브 예외/collect 실패는 <b>절대 호출자(UI)로 전파하지 않고</b>
/// <see cref="SystemResourceSnapshot.Empty"/>를 발행하며, 로깅은 상태 전환 시 1회만(1Hz 스팸 방지).</para>
/// <para>재진입 가드: <see cref="Sample"/>는 동시 진입 시 즉시 <see cref="Current"/> 반환(UI 스레드
/// 단일 호출 전제이나 방어적).</para>
/// </summary>
public sealed class SystemResourceMonitor : ISystemResourceMonitor
{
    private readonly IResourceProbe _probe;
    private readonly IClock _clock;
    private readonly ILogService? _log;

    private readonly HysteresisLevelMapper _cpuLevel;
    private readonly HysteresisLevelMapper _gpuLevel;
    private readonly HysteresisLevelMapper _ramLevel;

    private volatile SystemResourceSnapshot _current = SystemResourceSnapshot.Empty;
    private int _sampling;        // 재진입 가드(0/1)
    private bool _started;
    private bool _disposed;
    private bool _faultLogged;    // 샘플링 실패 상태 전환 1회 로깅
    private bool _eventFaultLogged;   // 구독자 예외 1회 로깅

    public SystemResourceMonitor(IResourceProbe probe,
                                 IClock clock,
                                 ILogService? log = null,
                                 ResourceThresholds? thresholds = null)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log;
        var t = thresholds ?? ResourceThresholds.Default;
        _cpuLevel = new HysteresisLevelMapper(t);
        _gpuLevel = new HysteresisLevelMapper(t);
        _ramLevel = new HysteresisLevelMapper(t);
    }

    public SystemResourceSnapshot Current => _current;

    public event EventHandler<SystemResourceSnapshot>? SnapshotUpdated;

    public void Start()
    {
        if (_disposed || _started) return;
        try
        {
            _probe.Open();
            _started = true;
        }
        catch (Exception ex)
        {
            LogOnce($"[SysRes] 프로브 Open 실패 → 지표 미가용: {ex.Message}");
            _started = false;
        }
    }

    public void Stop()
    {
        if (!_started) return;
        try { _probe.Close(); } catch { /* 무시 */ }
        _started = false;
        // 재프라임을 위해 레벨 히스테리시스 초기화(재시작 시 stale 레벨 방지).
        _cpuLevel.Reset();
        _gpuLevel.Reset();
        _ramLevel.Reset();
    }

    public SystemResourceSnapshot Sample()
    {
        if (_disposed) return _current;

        // 재진입(동시 호출) 시 skip — 직전 값 반환.
        if (Interlocked.Exchange(ref _sampling, 1) == 1)
            return _current;

        try
        {
            if (!_started) Start();
            if (!_started)
                return Publish(SystemResourceSnapshot.Empty);   // Open 실패 → N/A

            ProbeReading r = _probe.Sample();
            _faultLogged = false;                                // 정상 복귀 시 로그 상태 리셋
            return Publish(Build(r));
        }
        catch (Exception ex)
        {
            // fail-safe: 어떤 예외도 UI로 전파하지 않는다.
            LogOnce($"[SysRes] 샘플링 실패 → N/A: {ex.Message}");
            return Publish(SystemResourceSnapshot.Empty);
        }
        finally
        {
            Interlocked.Exchange(ref _sampling, 0);
        }
    }

    private SystemResourceSnapshot Build(ProbeReading r)
    {
        double cpu = Clamp(r.Cpu);
        double gpu = Clamp(r.Gpu);
        double ram = Clamp(r.Ram);

        return new SystemResourceSnapshot
        {
            CpuPercent = cpu,
            CpuAvailable = r.CpuOk,
            CpuLevel = r.CpuOk ? _cpuLevel.Map(cpu) : ResourceLevel.Normal,

            GpuPercent = gpu,
            GpuAvailable = r.GpuOk,
            GpuLevel = r.GpuOk ? _gpuLevel.Map(gpu) : ResourceLevel.Normal,

            RamPercent = ram,
            RamAvailable = r.RamOk,
            RamLevel = r.RamOk ? _ramLevel.Map(ram) : ResourceLevel.Normal,

            RamTotalBytes = r.RamTotalBytes,
            RamUsedBytes = r.RamUsedBytes,
            TimestampUtc = _clock.UtcNow,
        };
    }

    private SystemResourceSnapshot Publish(SystemResourceSnapshot snapshot)
    {
        _current = snapshot;
        // 구독자 예외를 격리 — fail-safe 경로(catch 내 Publish 포함)에서도 Sample() 밖으로 전파 금지(FR-06).
        try
        {
            SnapshotUpdated?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            if (!_eventFaultLogged)
            {
                _log?.Error($"[SysRes] SnapshotUpdated 구독자 예외(격리): {ex.Message}");
                _eventFaultLogged = true;
            }
        }
        return snapshot;
    }

    private static double Clamp(double v)
        => double.IsNaN(v) || double.IsInfinity(v) ? 0 : Math.Clamp(v, 0, 100);

    private void LogOnce(string message)
    {
        if (_faultLogged) return;
        _log?.Error(message);
        _faultLogged = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _probe.Close(); } catch { /* 무시 */ }
        try { _probe.Dispose(); } catch { /* 무시 */ }
    }
}
