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
   Purpose      : 지도 계기 인디케이터(강풍·탐지장애) + 보기(View) 가시성 배선
                  (GMap_Map_Instruments FR-03/07/12/14~17). MapViewModel partial 분리.
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

    #region - 탐지·장애 인디케이터 -
    private int _detectionCount;
    public int DetectionCount { get => _detectionCount; set { _detectionCount = value; NotifyOfPropertyChange(nameof(DetectionCount)); } }
    private int _faultCount;
    public int FaultCount { get => _faultCount; set { _faultCount = value; NotifyOfPropertyChange(nameof(FaultCount)); } }

    private double _detFaultX = double.NaN;
    public double DetFaultX { get => _detFaultX; set { _detFaultX = value; NotifyOfPropertyChange(nameof(DetFaultX)); } }
    private double _detFaultY = double.NaN;
    public double DetFaultY { get => _detFaultY; set { _detFaultY = value; NotifyOfPropertyChange(nameof(DetFaultY)); } }

    private InstrumentOrientation _detFaultOrientation = InstrumentOrientation.Vertical;
    public InstrumentOrientation DetFaultOrientation { get => _detFaultOrientation; set { _detFaultOrientation = value; NotifyOfPropertyChange(nameof(DetFaultOrientation)); } }
    private bool _detFaultHideOnZero;
    public bool DetFaultHideOnZero { get => _detFaultHideOnZero; set { _detFaultHideOnZero = value; NotifyOfPropertyChange(nameof(DetFaultHideOnZero)); } }

    private Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand? _saveMapDetFaultCommand;
    public Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand SaveMapDetectionFaultCommand
        => _saveMapDetFaultCommand ??= new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand(_ => _ = SaveMapDetectionFaultState());

    private Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand? _openEventPanelCommand;
    /// <summary>탐지·장애 pill 클릭 → 이벤트 패널 오픈(D-10).</summary>
    public Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand OpenEventPanelCommand
        => _openEventPanelCommand ??= new Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.RelayCommand(
            _ => _ = _eventAggregator?.PublishOnCurrentThreadAsync(new OpenEventPanelMessageModel()));

    /// <summary>EQM 활성 카운트 변경 핸들러(FR-07). EQM은 NATS 콜백 스레드일 수 있어 UI 스레드로 정렬.</summary>
    private void OnActiveCountChanged(int detection, int fault)
        => OnUIThread(() => { DetectionCount = detection; FaultCount = fault; });
    #endregion

    #region - 보기(View) 가시성 (D-12/FR-14~17) -
    private bool _isCompassVisible = true;
    public bool IsCompassVisible { get => _isCompassVisible; set { _isCompassVisible = value; NotifyOfPropertyChange(nameof(IsCompassVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); } }
    private bool _isWindyIndicatorVisible = true;
    public bool IsWindyIndicatorVisible { get => _isWindyIndicatorVisible; set { _isWindyIndicatorVisible = value; NotifyOfPropertyChange(nameof(IsWindyIndicatorVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); } }
    private bool _isDetFaultVisible = true;
    public bool IsDetFaultVisible { get => _isDetFaultVisible; set { _isDetFaultVisible = value; NotifyOfPropertyChange(nameof(IsDetFaultVisible)); if (!_suppressInstrumentSave) _ = SaveInstrumentVisibility(); } }
    #endregion

    /// <summary>초기 배선 — CCMS(부팅)에서 1회 호출. EQM 구독 + 초기 카운트 + 설정 복원(억제).</summary>
    private void InitializeInstruments()
    {
        // EQM 구독(FR-07) — VM은 싱글턴(앱 수명)이라 누수 없음. 중복 방지 위해 먼저 해제.
        _eventQueueManager.OnActiveCountChanged -= OnActiveCountChanged;
        _eventQueueManager.OnActiveCountChanged += OnActiveCountChanged;
        var (det, flt) = _eventQueueManager.GetActiveCounts();
        DetectionCount = det; FaultCount = flt;

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
            var d = _setupModel?.MapDetectionFault;
            if (d != null)
            {
                DetFaultX = d.X; DetFaultY = d.Y;
                DetFaultOrientation = InstrumentMath.ParseOrientation(d.Orientation);
                DetFaultHideOnZero = d.HideOnZero;
            }
            var vis = _setupModel?.MapInstrumentVisibility;
            if (vis != null)
            {
                IsCompassVisible = vis.Compass;
                IsWindyIndicatorVisible = vis.Windy;
                IsDetFaultVisible = vis.DetectionFault;
                IsCenterCrosshairVisible = vis.Crosshair;
            }
            _log?.Info($"[Instruments] 복원: windy={_setupModel?.MapWindyIndicator}, detFault={_setupModel?.MapDetectionFault}, vis={_setupModel?.MapInstrumentVisibility}");
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

    private Task SaveMapDetectionFaultState()
    {
        if (_suppressInstrumentSave) return Task.CompletedTask;
        try
        {
            var m = new MapDetectionFaultModel { X = DetFaultX, Y = DetFaultY, Orientation = DetFaultOrientation.ToString(), HideOnZero = DetFaultHideOnZero };
            if (_setupModel != null) _setupModel.MapDetectionFault = m;
            return MapSettingsHelper.SaveMapDetectionFaultAsync(m, _log);
        }
        catch (Exception ex) { _log?.Error($"[Instruments] 탐지장애 저장 실패: {ex.Message}"); return Task.CompletedTask; }
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
