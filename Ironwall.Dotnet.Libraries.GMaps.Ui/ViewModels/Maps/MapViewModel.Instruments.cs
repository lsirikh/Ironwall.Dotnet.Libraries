using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 지도 계기 인디케이터(강풍) + 탐지·장애 신호등(상단바 T1) + 보기(View) 가시성 배선
                  (GMap_Map_Instruments FR-03/12/14~17 · map-topbar-trafficlight FR-A).
                  신호등 최종 배치 = 상단바 시스템 도넛 캡슐 좌측(사용자 확정, 이벤트 카드 리스트
                  헤더 배치는 철회·원복) — 같은 뷰라 보기 토글이 직접 가시성 제어(EA 브리지 불요).
                  구 pill 드래그 계기(GMapDetectionFaultControl)는 D2 제거 유지.
   Note         : 복원 중 저장 억제(_suppressInstrumentSave) — 회전 영속 자가오염(44bc6fc) 교훈.
                  강풍 초기 모드는 IGMapSetupModel에 없어 wind0 기본, 첫 ChangeModeWindyMessage로 갱신.
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel : IHandle<ChangeModeWindyMessageModel>
{
    private bool _suppressInstrumentSave;

    #region - 강풍 인디케이터 -
    private EnumWindyMode _windyMode = EnumWindyMode.wind0;
    /// <summary>현재 WINDY 모드(강풍 인디케이터 바인딩). NATS/WindyPanel의 ChangeModeWindyMessage로 갱신.</summary>
    public EnumWindyMode WindyMode { get => _windyMode; set { _windyMode = value; NotifyOfPropertyChange(nameof(WindyMode)); } }

    private double _windyIndicatorX = double.NaN;
    public double WindyIndicatorX { get => _windyIndicatorX; set { _windyIndicatorX = value; NotifyOfPropertyChange(nameof(WindyIndicatorX)); } }
    private double _windyIndicatorY = double.NaN;
    public double WindyIndicatorY { get => _windyIndicatorY; set { _windyIndicatorY = value; NotifyOfPropertyChange(nameof(WindyIndicatorY)); } }

    private WindyDisplayStyle _windyDisplayStyle = WindyDisplayStyle.IconLabel;
    public WindyDisplayStyle WindyDisplayStyle { get => _windyDisplayStyle; set { _windyDisplayStyle = value; NotifyOfPropertyChange(nameof(WindyDisplayStyle)); } }
    private bool _windyHideOnNormal;
    public bool WindyHideOnNormal { get => _windyHideOnNormal; set { _windyHideOnNormal = value; NotifyOfPropertyChange(nameof(WindyHideOnNormal)); } }

    private Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand? _saveMapWindyCommand;
    public Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand SaveMapWindyCommand
        => _saveMapWindyCommand ??= new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand(_ => _ = SaveMapWindyState());

    private Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand? _openWindyPanelCommand;
    /// <summary>강풍 배지 클릭 → WINDY 패널 오픈(D-10).</summary>
    public Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand OpenWindyPanelCommand
        => _openWindyPanelCommand ??= new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand(
            _ => _ = _eventAggregator?.PublishOnCurrentThreadAsync(new OpenWindyPanelMessageModel()));

    /// <summary>ChangeModeWindyMessage 수신 → 강풍 모드 갱신(FR-03). Value=int(0~3).</summary>
    public Task HandleAsync(ChangeModeWindyMessageModel message, CancellationToken cancellationToken)
    {
        int v = message?.Value ?? 0;
        WindyMode = v switch { 1 => EnumWindyMode.wind1, 2 => EnumWindyMode.wind2, 3 => EnumWindyMode.wind3, _ => EnumWindyMode.wind0 };
        return Task.CompletedTask;
    }
    #endregion

    #region - 탐지·장애 신호등 (map-topbar-trafficlight FR-A — 상단바 T1, 도넛 캡슐 좌측) -
    // EQM 활성(미조치) 집계 → 램프 3(빨=장애 펄스·노=탐지·초=정상) + 건수 병기(점등 시만).
    // 점등 규칙 SSOT = TrafficLampLogic(green 불변식·미초기화 게이트). 클릭=이벤트 패널 오픈(D-10 승계).
    // 구 pill 위치/방향/HideOnZero 영속(MapDetectionFaultModel)은 소비처 없음(파일 잔존 무해).

    private int _trafficDetectionCount;
    /// <summary>미조치 탐지 건수(EQM). 램프 점등 시에만 숫자 표시.</summary>
    public int TrafficDetectionCount { get => _trafficDetectionCount; private set { _trafficDetectionCount = value; NotifyOfPropertyChange(nameof(TrafficDetectionCount)); } }

    private int _trafficFaultCount;
    /// <summary>미조치 장애 건수(EQM).</summary>
    public int TrafficFaultCount { get => _trafficFaultCount; private set { _trafficFaultCount = value; NotifyOfPropertyChange(nameof(TrafficFaultCount)); } }

    private bool _isTrafficCountsReady;
    /// <summary>EQM 첫 집계 수신 여부 — false면 '데이터 없음'(전체 소등+"—"), 초록 점등 금지.</summary>
    public bool IsTrafficCountsReady { get => _isTrafficCountsReady; private set { _isTrafficCountsReady = value; NotifyOfPropertyChange(nameof(IsTrafficCountsReady)); } }

    public bool IsTrafficFaultOn => TrafficLampLogic.FaultOn(_isTrafficCountsReady, _trafficFaultCount);
    public bool IsTrafficDetectionOn => TrafficLampLogic.DetectionOn(_isTrafficCountsReady, _trafficDetectionCount);
    public bool IsTrafficGreenOn => TrafficLampLogic.GreenOn(_isTrafficCountsReady, _trafficDetectionCount, _trafficFaultCount);

    public string TrafficTooltip => _isTrafficCountsReady
        ? $"미조치 장애 {_trafficFaultCount}건 · 탐지 {_trafficDetectionCount}건 — 클릭: 이벤트 패널 열기"
        : "집계 대기 중 — 로그인/초기화 전";

    /// <summary>EQM 콜백은 NATS 스레드일 수 있어 UI 스레드로 정렬(FR-07 승계).</summary>
    private void OnTrafficActiveCountChanged(int detection, int fault)
        => OnUIThread(() =>
        {
            TrafficDetectionCount = detection;
            TrafficFaultCount = fault;
            IsTrafficCountsReady = true;
            NotifyOfPropertyChange(nameof(IsTrafficFaultOn));
            NotifyOfPropertyChange(nameof(IsTrafficDetectionOn));
            NotifyOfPropertyChange(nameof(IsTrafficGreenOn));
            NotifyOfPropertyChange(nameof(TrafficTooltip));
        });

    private Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand? _openEventPanelCommand;
    /// <summary>신호등 pill 클릭 → 이벤트 패널 오픈(D-10 승계).</summary>
    public Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand OpenEventPanelCommand
        => _openEventPanelCommand ??= new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand(
            _ => _ = _eventAggregator?.PublishOnCurrentThreadAsync(new OpenEventPanelMessageModel()));
    #endregion

    #region - 보기(View) 가시성 (D-12/FR-14~17) -
    private bool _isCompassVisible = true;
    public bool IsCompassVisible { get => _isCompassVisible; set { _isCompassVisible = value; NotifyOfPropertyChange(nameof(IsCompassVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); } }
    private bool _isWindyIndicatorVisible = true;
    public bool IsWindyIndicatorVisible { get => _isWindyIndicatorVisible; set { _isWindyIndicatorVisible = value; NotifyOfPropertyChange(nameof(IsWindyIndicatorVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); } }
    private bool _isDetFaultVisible = true;
    /// <summary>보기&gt;탐지·장애 신호등(Ctrl+Shift+F) — 상단바 신호등 pill 가시성 직결(같은 뷰, EA 브리지 불요).</summary>
    public bool IsDetFaultVisible { get => _isDetFaultVisible; set { _isDetFaultVisible = value; NotifyOfPropertyChange(nameof(IsDetFaultVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); } }

    // 키보드 단축키용 토글 커맨드(메뉴는 IsChecked로 마우스 토글). 세터가 저장까지 처리.
    private Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand? _toggleCompassVisibleCommand;
    public Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand ToggleCompassVisibleCommand
        => _toggleCompassVisibleCommand ??= new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand(_ => IsCompassVisible = !IsCompassVisible);
    private Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand? _toggleWindyIndicatorVisibleCommand;
    public Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand ToggleWindyIndicatorVisibleCommand
        => _toggleWindyIndicatorVisibleCommand ??= new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand(_ => IsWindyIndicatorVisible = !IsWindyIndicatorVisible);
    private Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand? _toggleDetFaultVisibleCommand;
    public Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand ToggleDetFaultVisibleCommand
        => _toggleDetFaultVisibleCommand ??= new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand(_ => IsDetFaultVisible = !IsDetFaultVisible);
    #endregion

    /// <summary>초기 배선 — CCMS(부팅)에서 1회 호출. EQM 구독(신호등) + 초기 카운트 + 설정 복원(억제).</summary>
    private void InitializeInstruments()
    {
        // EQM 구독(FR-A) — VM은 싱글턴(앱 수명)이라 누수 없음. 중복 방지 위해 먼저 해제.
        _eventQueueManager.OnActiveCountChanged -= OnTrafficActiveCountChanged;
        _eventQueueManager.OnActiveCountChanged += OnTrafficActiveCountChanged;
        var (det, flt) = _eventQueueManager.GetActiveCounts();
        OnTrafficActiveCountChanged(det, flt);

        LoadMapInstrumentsFromSettings();
    }

    /// <summary>부팅 복원 — 위치/설정/가시성. 복원 중 저장 억제(D-07).</summary>
    private void LoadMapInstrumentsFromSettings()
    {
        _suppressInstrumentSave = true;
        try
        {
            var w = _setupModel?.MapWindyIndicator;
            if (w != null)
            {
                WindyIndicatorX = w.X; WindyIndicatorY = w.Y;
                WindyDisplayStyle = InstrumentMath.ParseWindyStyle(w.DisplayStyle);
                WindyHideOnNormal = w.HideOnNormal;
            }
            var vis = _setupModel?.MapInstrumentVisibility;
            if (vis != null)
            {
                IsCompassVisible = vis.Compass;
                IsWindyIndicatorVisible = vis.Windy;
                IsDetFaultVisible = vis.DetectionFault;
                IsCenterCrosshairVisible = vis.Crosshair;
            }
            _log?.Info($"[Instruments] 복원: windy={_setupModel?.MapWindyIndicator}, vis={_setupModel?.MapInstrumentVisibility}");
        }
        finally { _suppressInstrumentSave = false; }
    }

    private Task SaveMapWindyState()
    {
        if (_suppressInstrumentSave) return Task.CompletedTask;
        try
        {
            var m = new MapWindyIndicatorModel { X = WindyIndicatorX, Y = WindyIndicatorY, DisplayStyle = WindyDisplayStyle.ToString(), HideOnNormal = WindyHideOnNormal };
            if (_setupModel != null) _setupModel.MapWindyIndicator = m;
            return MapSettingsHelper.SaveMapWindyIndicatorAsync(m, _log);
        }
        catch (Exception ex) { _log?.Error($"[Instruments] 강풍 저장 실패: {ex.Message}"); return Task.CompletedTask; }
    }

    private Task SaveInstrumentVisibility()
    {
        try
        {
            var m = new MapInstrumentVisibilityModel { Compass = IsCompassVisible, Windy = IsWindyIndicatorVisible, DetectionFault = IsDetFaultVisible, Crosshair = IsCenterCrosshairVisible };
            if (_setupModel != null) _setupModel.MapInstrumentVisibility = m;
            return MapSettingsHelper.SaveMapInstrumentVisibilityAsync(m, _log);
        }
        catch (Exception ex) { _log?.Error($"[Instruments] 가시성 저장 실패: {ex.Message}"); return Task.CompletedTask; }
    }
}
