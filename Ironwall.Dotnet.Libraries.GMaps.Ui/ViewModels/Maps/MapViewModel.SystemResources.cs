using System;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.SystemResources.Models;
using Ironwall.Dotnet.Libraries.SystemResources.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : 툴바 우측 CPU/GPU/RAM 사용률 표시 (SystemResources 소비)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// MapViewModel의 시스템 리소스 표시 partial. <b>이 뷰가 소유한 UI-스레드 DispatcherTimer</b>가
/// 1초마다 <see cref="ISystemResourceMonitor.Sample"/>을 호출하고 결과를 바인딩 속성에 반영한다.
/// UI 스레드 단일 구동이라 크로스스레드 마샬/락/재진입이 없다. 모니터는 공유 싱글턴이므로
/// 탭 전환 시 타이머만 멈추고(<see cref="StopResourceMonitor"/>) 핸들은 유지(워밍업 보존),
/// Stop/Dispose는 하지 않는다(컨테이너가 앱 종료 시 Dispose).
/// </summary>
public partial class MapViewModel
{
    // 생성자(주 파일)에서 주입.
    private readonly ISystemResourceMonitor _resourceMonitor;
    private DispatcherTimer? _sysResTimer;

    #region - Lifecycle (OnActivate/OnDeactivate에서 호출) -

    /// <summary>활성화 시: 모니터 프라이밍 + UI 타이머 시작(멱등).</summary>
    private void StartResourceMonitor()
    {
        try
        {
            _resourceMonitor.Start();   // 멱등 — 최초 1회만 PDH open/prime, 이후 no-op

            if (_sysResTimer == null)
            {
                _sysResTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromSeconds(1),
                };
                _sysResTimer.Tick += OnResourceTimerTick;
            }
            _sysResTimer.Start();

            // 재활성 시 마지막 스냅샷 즉시 반영(워밍업 유지되어 값 있음).
            ApplyResourceSnapshot(_resourceMonitor.Current);
        }
        catch (Exception ex)
        {
            _log?.Error($"[SysRes] 모니터 시작 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 비활성화 시: <b>모든 경로(close 무관)</b>에서 타이머만 정지. 모니터 핸들은 유지(재활성 워밍업 보존),
    /// 공유 싱글턴이라 Stop/Dispose 하지 않는다.
    /// </summary>
    private void StopResourceMonitor()
    {
        try { _sysResTimer?.Stop(); }
        catch (Exception ex) { _log?.Error($"[SysRes] 타이머 정지 실패: {ex.Message}"); }
    }

    private void OnResourceTimerTick(object? sender, EventArgs e)
    {
        // UI 스레드. Sample()은 fail-safe(예외 미전파)이나 방어적으로 감싼다.
        try { ApplyResourceSnapshot(_resourceMonitor.Sample()); }
        catch (Exception ex) { _log?.Error($"[SysRes] 리소스 틱 실패: {ex.Message}"); }
    }

    private void ApplyResourceSnapshot(SystemResourceSnapshot s)
    {
        CpuText = s.CpuAvailable ? $"{s.CpuPercent:F0}%" : "--";
        CpuLevel = s.CpuLevel;
        CpuTooltip = s.CpuAvailable ? $"CPU 사용률 {s.CpuPercent:F0}%" : "CPU 측정 대기/불가";

        GpuText = s.GpuAvailable ? $"{s.GpuPercent:F0}%" : "--";
        GpuLevel = s.GpuLevel;
        GpuTooltip = s.GpuAvailable ? $"GPU 사용률 {s.GpuPercent:F0}%" : "GPU 미가용";

        RamText = s.RamAvailable ? $"{s.RamPercent:F0}%" : "--";
        RamLevel = s.RamLevel;
        RamTooltip = s.RamAvailable
            ? $"메모리 사용률 {s.RamPercent:F0}% ({BytesToGb(s.RamUsedBytes):F1}/{BytesToGb(s.RamTotalBytes):F1} GB)"
            : "메모리 측정 불가";

        IsGpuAvailable = s.GpuAvailable;
        IsResourceMonitorAvailable = s.RamAvailable;   // RAM = 모니터 전체 가용성 canary
    }

    private static double BytesToGb(ulong bytes) => bytes / 1024d / 1024d / 1024d;

    #endregion

    #region - Bindable Properties (툴바 칩) -

    private string _cpuText = "--";
    public string CpuText { get => _cpuText; set { if (_cpuText != value) { _cpuText = value; NotifyOfPropertyChange(); } } }

    private ResourceLevel _cpuLevel = ResourceLevel.Normal;
    public ResourceLevel CpuLevel { get => _cpuLevel; set { if (_cpuLevel != value) { _cpuLevel = value; NotifyOfPropertyChange(); } } }

    private string _cpuTooltip = "CPU 사용률";
    public string CpuTooltip { get => _cpuTooltip; set { if (_cpuTooltip != value) { _cpuTooltip = value; NotifyOfPropertyChange(); } } }

    private string _gpuText = "--";
    public string GpuText { get => _gpuText; set { if (_gpuText != value) { _gpuText = value; NotifyOfPropertyChange(); } } }

    private ResourceLevel _gpuLevel = ResourceLevel.Normal;
    public ResourceLevel GpuLevel { get => _gpuLevel; set { if (_gpuLevel != value) { _gpuLevel = value; NotifyOfPropertyChange(); } } }

    private string _gpuTooltip = "GPU 사용률";
    public string GpuTooltip { get => _gpuTooltip; set { if (_gpuTooltip != value) { _gpuTooltip = value; NotifyOfPropertyChange(); } } }

    private string _ramText = "--";
    public string RamText { get => _ramText; set { if (_ramText != value) { _ramText = value; NotifyOfPropertyChange(); } } }

    private ResourceLevel _ramLevel = ResourceLevel.Normal;
    public ResourceLevel RamLevel { get => _ramLevel; set { if (_ramLevel != value) { _ramLevel = value; NotifyOfPropertyChange(); } } }

    private string _ramTooltip = "메모리 사용률";
    public string RamTooltip { get => _ramTooltip; set { if (_ramTooltip != value) { _ramTooltip = value; NotifyOfPropertyChange(); } } }

    private bool _isGpuAvailable;
    /// <summary>GPU 카운터 가용 여부 — false면 GPU 칩 Hidden(공간 예약).</summary>
    public bool IsGpuAvailable { get => _isGpuAvailable; set { if (_isGpuAvailable != value) { _isGpuAvailable = value; NotifyOfPropertyChange(); } } }

    private bool _isResourceMonitorAvailable;
    /// <summary>모니터 전체 가용 여부(RAM canary).</summary>
    public bool IsResourceMonitorAvailable { get => _isResourceMonitorAvailable; set { if (_isResourceMonitorAvailable != value) { _isResourceMonitorAvailable = value; NotifyOfPropertyChange(); } } }

    #endregion
}
