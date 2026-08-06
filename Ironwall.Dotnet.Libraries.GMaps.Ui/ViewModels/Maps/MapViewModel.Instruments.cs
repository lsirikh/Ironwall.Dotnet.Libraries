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
   Purpose      : 지도 계기 인디케이터(강풍) + 보기(View) 가시성 배선
                  (GMap_Map_Instruments FR-03/12/14~17). MapViewModel partial 분리.
                  탐지·장애 pill 계기는 map-topbar-trafficlight D2로 제거 —
                  집계 표시는 이벤트 카드 리스트 헤더 신호등(Events.Ui, EQM 직구독)이 승계,
                  여기는 보기 토글(IsDetFaultVisible)의 EA 브리지(FR-A5)만 남는다.
   Note         : 복원 중 저장 억제(_suppressInstrumentSave) — 회전 영속 자가오염(44bc6fc) 교훈.
                  강풍 초기 모드는 IGMapSetupModel에 없어 wind0 기본, 첫 ChangeModeWindyMessage로 갱신.
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel : IHandle<ChangeModeWindyMessageModel>,
                                    IHandle<TrafficLightVisibilityRequestMessage>
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

    #region - 탐지·장애 신호등 연동 (map-topbar-trafficlight FR-A5/A6) -
    // pill 계기(GMapDetectionFaultControl)·EQM 구독·건수 프로퍼티는 D2로 제거(ee51761 승계 커밋) —
    // 위치/방향/HideOnZero 영속(MapDetectionFaultModel)은 소비처가 없어져 읽지 않는다(파일 잔존 무해).

    /// <summary>신호등 측(Events.Ui) 초기 상태 질의 수신 → 현재 토글 값 재발행(양측 활성화 순서 역전 대비).</summary>
    public Task HandleAsync(TrafficLightVisibilityRequestMessage message, CancellationToken cancellationToken)
        => _eventAggregator?.PublishOnCurrentThreadAsync(
               new TrafficLightVisibilityChangedMessage(IsDetFaultVisible), cancellationToken) ?? Task.CompletedTask;
    #endregion

    #region - 보기(View) 가시성 (D-12/FR-14~17) -
    private bool _isCompassVisible = true;
    public bool IsCompassVisible { get => _isCompassVisible; set { _isCompassVisible = value; NotifyOfPropertyChange(nameof(IsCompassVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); } }
    private bool _isWindyIndicatorVisible = true;
    public bool IsWindyIndicatorVisible { get => _isWindyIndicatorVisible; set { _isWindyIndicatorVisible = value; NotifyOfPropertyChange(nameof(IsWindyIndicatorVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); } }
    private bool _isDetFaultVisible = true;
    /// <summary>보기&gt;탐지·장애 신호등(Ctrl+Shift+F). 세터가 EA 발행(FR-A5) — 부팅 복원(_suppressInstrumentSave) 중에도
    /// 발행해 Events.Ui 신호등에 초기 상태를 전파한다(저장만 억제).</summary>
    public bool IsDetFaultVisible { get => _isDetFaultVisible; set { _isDetFaultVisible = value; NotifyOfPropertyChange(nameof(IsDetFaultVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); _ = _eventAggregator?.PublishOnCurrentThreadAsync(new TrafficLightVisibilityChangedMessage(value)); } }

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

    /// <summary>초기 배선 — CCMS(부팅)에서 1회 호출. 설정 복원(억제).
    /// EQM 구독은 신호등 이관(FR-A6)으로 제거 — EventCardListPanelViewModel이 직구독한다(이중 집계 방지).</summary>
    private void InitializeInstruments()
    {
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
