using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.ViewModel;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/// <summary>
/// 맵 위 카메라 RTSP 팝업 1개의 런타임 상태(비영속). Hub(공유 디코더) 경로로 영상 표시.
/// <para>
/// Hub 배선: <see cref="CameraRowViewModel"/>(ISharedCameraStreamHub) + <see cref="CameraViewModel"/>(=player DataContext).
/// 플레이어가 OnLoaded에서 vm.IsHubManaged를 보고 ConnectViaHubAsync→Lease→공유 BitmapSource 표시.
/// </para>
/// </summary>
public class CameraStreamPopupViewModel : PropertyChangedBase, IAsyncDisposable
{
    public const double DefaultWidth = 384;
    public const double DefaultHeight = 300;
    public const double LargeWidth = 640;
    public const double LargeHeight = 380;

    private readonly CameraRowViewModel _row;
    private double _canvasLeft;
    private double _canvasTop;
    private double _popupWidth = DefaultWidth;
    private double _popupHeight = DefaultHeight;
    private double _cameraScreenX;
    private double _cameraScreenY;
    private double _lineX1, _lineY1, _lineX2, _lineY2;
    private bool _isLarge;
    private ICommand? _closeCommand;
    private ICommand? _toggleSizeCommand;

    /// <summary>닫기 요청 — MapViewModel이 컬렉션에서 제거 + DisposeAsync.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>드래그 종료 — MapViewModel이 CanvasLeft/Top→AnchorGeo 재계산 + DB 저장.</summary>
    public event EventHandler? DragCompleted;

    /// <summary>컨트롤이 드래그 종료 시 호출 → DragCompleted 발화.</summary>
    internal void RaiseDragCompleted() => DragCompleted?.Invoke(this, EventArgs.Empty);

    /// <summary>좌클릭 선택 요청 — MapViewModel이 SelectedCameraPopup 설정 + 맨앞 이동. (FR-SEL-01)</summary>
    public event EventHandler? SelectRequested;

    /// <summary>컨트롤 좌클릭 시 호출 → 선택 요청.</summary>
    internal void RaiseSelectRequested() => SelectRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>우버튼 드래그-PTZ 완료 — MapViewModel이 IPtzController.RelativeMoveByPixel 호출 + FOV 갱신. (FR-DRAG-03)</summary>
    public event EventHandler<PtzDragEventArgs>? PtzDragRequested;

    /// <summary>컨트롤이 영상 위 좌버튼 드래그 종료(8px 초과) 시 호출. 델타·영상 치수를 전달.</summary>
    internal void RaisePtzDrag(double dx, double dy, double imageW, double imageH)
        => PtzDragRequested?.Invoke(this, new PtzDragEventArgs(dx, dy, imageW, imageH));

    /// <summary>영상 위 휠 → PTZ 줌(+1=줌인 / -1=줌아웃). MapViewModel이 IPtzController.RelativeZoom 호출. (FR-PTZCTL-03)</summary>
    public event EventHandler<int>? PtzZoomRequested;

    /// <summary>컨트롤이 영상 위 휠 회전 시 호출(방향 ±1).</summary>
    internal void RaisePtzZoom(int direction) => PtzZoomRequested?.Invoke(this, direction);

    // 줌 +/- 버튼 — press-hold(누르면 연속 줌·뗌 정지). 컨트롤이 PreviewMouseDown/Up에서 Hold/Stop 발화.
    // 줌 release는 PtzStopRequested 재사용(StopAsync가 PanTilt+Zoom 정지). 휠은 별도 PtzZoomRequested 펄스 경로 유지.
    /// <summary>줌 버튼 누름 → 연속 줌 시작(+1=줌인/-1=줌아웃). 컨트롤 PreviewMouseDown. 뗌은 PtzStopRequested.</summary>
    public event EventHandler<int>? ZoomHoldRequested;
    internal void RaiseZoomHold(int direction) => ZoomHoldRequested?.Invoke(this, direction);

    /// <summary>포커스 버튼 누름 → 연속 포커스 시작(+1=far/-1=near). 컨트롤 PreviewMouseDown. IsImagingCapable일 때만.</summary>
    public event EventHandler<int>? FocusHoldRequested;
    internal void RaiseFocusHold(int direction) => FocusHoldRequested?.Invoke(this, direction);
    /// <summary>포커스 버튼 뗌/캡처분실/닫기 → 포커스 모터 정지(ImagingClient Stop — PTZ StopAsync와 별개 경로).</summary>
    public event EventHandler? FocusStopRequested;
    internal void RaiseFocusStop() => FocusStopRequested?.Invoke(this, EventArgs.Empty);

    public CameraStreamPopupViewModel(int cameraId, string? title, RtspConnectionInfo? connInfo,
        PointLatLng anchorGeo, ISharedCameraStreamHub hub)
    {
        CameraId = cameraId;
        Title = string.IsNullOrWhiteSpace(title) ? $"카메라 {cameraId}" : title!;
        _connectionInfo = connInfo;   // Onvif조회 모드(FR-05)는 null로 시작 — 조회 완료 후 세터 주입(late-bind)
        AnchorGeo = anchorGeo;

        _row = new CameraRowViewModel(cameraId.ToString(), Title, hub);
        var model = new CameraModel
        {
            Guid = cameraId.ToString(),
            Title = Title,
            StreamingOptions = StreamingOptions.CreateDefault(),
            AutoPlay = true,
            ShowControls = true,
        };
        if (connInfo != null) model.ConnectionInfo = connInfo;
        // 플레이어 DataContext가 될 스트리밍 VM (OwnerRow를 통해 Hub Lease 획득)
        StreamVm = new CameraViewModel(model, _row.RowId, _row);
    }

    /// <summary>카메라 장비 Id(= PidsSymbol.LinkedDeviceId). 팝업 식별/위치 키.</summary>
    public int CameraId { get; }
    public string Title { get; }

    private RtspConnectionInfo? _connectionInfo;
    /// <summary>재생 연결정보. Onvif조회 모드에선 오픈 시 null → 조회 완료 후 주입 — 세터가 StreamVm 모델을
    /// 동기하고 PropertyChanged를 발화해 팝업 템플릿 바인딩 → 플레이어 ConnectionInfo DP → late-bind 연결을
    /// 트리거한다. (CameraPopup_RtspSource_Priority FR-05)</summary>
    public RtspConnectionInfo? ConnectionInfo
    {
        get => _connectionInfo;
        set
        {
            if (ReferenceEquals(_connectionInfo, value)) return;   // 멱등(M-5) — 중복 주입 시 PropertyChanged 재발화→플레이어 재연결 트리거 방지
            _connectionInfo = value;
            if (value != null && StreamVm != null) StreamVm.ConnectionInfo = value;   // Row lease/모델 경로 일관
            NotifyOfPropertyChange(nameof(ConnectionInfo));
        }
    }

    private bool _isResolvingSource;
    /// <summary>Onvif조회 모드에서 스트림 URL 조회(ONVIF GetStreamUri) 진행 중 — "영상 주소 조회 중…" 배지. (FR-05)</summary>
    public bool IsResolvingSource { get => _isResolvingSource; set { if (_isResolvingSource == value) return; _isResolvingSource = value; NotifyOfPropertyChange(nameof(IsResolvingSource)); } }

    /// <summary>팝업 좌상단 코너의 위경도 앵커(드래그 완료 시 갱신 → DB 저장).</summary>
    public PointLatLng AnchorGeo { get; set; }

    /// <summary>ImprovedRtspPlayer의 DataContext(Hub 경로 분기에 사용).</summary>
    public CameraViewModel StreamVm { get; }

    public double CanvasLeft { get => _canvasLeft; set { _canvasLeft = value; NotifyOfPropertyChange(nameof(CanvasLeft)); RecomputeLine(); } }
    public double CanvasTop { get => _canvasTop; set { _canvasTop = value < MinCanvasTop ? MinCanvasTop : value; NotifyOfPropertyChange(nameof(CanvasTop)); RecomputeLine(); } }
    public double PopupWidth { get => _popupWidth; set { _popupWidth = value; NotifyOfPropertyChange(nameof(PopupWidth)); RecomputeLine(); } }
    public double PopupHeight { get => _popupHeight; set { _popupHeight = value; NotifyOfPropertyChange(nameof(PopupHeight)); NotifyOfPropertyChange(nameof(ControlHeight)); RecomputeLine(); } }

    /// <summary>컨트롤 패널(아코디언) 높이 — 펼치면 팝업이 이만큼 아래로 커져 영상이 가려지지 않음. (PTZ 탭 줌·포커스 +/- 버튼 2행 추가로 188→230)</summary>
    public const double PanelHeight = 230;
    /// <summary>팝업 상단 최소 Y(PropertyPanelCanvas 기준, 0=맵 영역 상단). 음수면 위로 넘쳐 상단 툴바/MahApps 윈도우 타이틀바(최대·최소·닫기)를 덮으므로 클램프. geo 추종은 AnchorGeo가 별도 보존하므로 표시 위치만 제한.</summary>
    public const double MinCanvasTop = 0;
    /// <summary>컨트롤 실제 높이 = 영상 높이(PopupHeight) + 패널(펼침 시). MapView Height에 바인딩. (FR-UI-01)</summary>
    public double ControlHeight => _popupHeight + (_isPanelExpanded ? PanelHeight : 0);

    private int _zIndex;
    /// <summary>팝업 z-order(선택/오픈 시 최상위). Panel.ZIndex 바인딩 — 컬렉션 Move 대신 사용해 RTSP 컨테이너 재생성(영상 끊김) 방지. (FR-SEL-02)</summary>
    public int ZIndex { get => _zIndex; set { if (_zIndex == value) return; _zIndex = value; NotifyOfPropertyChange(nameof(ZIndex)); } }

    // ── 연결선(Leader Line): 카메라 심볼 중점 → 팝업 경계 (빨간 점선) ──────────
    /// <summary>카메라 심볼 중점의 위경도(팬/줌 시 화면점 재계산용). MapViewModel이 설정.</summary>
    public PointLatLng CameraGeo { get; set; }

    /// <summary>카메라 심볼 중점의 화면(Canvas) 좌표 = 연결선 끝점1. MapViewModel이 팬/줌 시 갱신.</summary>
    public double CameraScreenX { get => _cameraScreenX; set { _cameraScreenX = value; RecomputeLine(); } }
    public double CameraScreenY { get => _cameraScreenY; set { _cameraScreenY = value; RecomputeLine(); } }

    public double LineX1 { get => _lineX1; private set { _lineX1 = value; NotifyOfPropertyChange(nameof(LineX1)); } }
    public double LineY1 { get => _lineY1; private set { _lineY1 = value; NotifyOfPropertyChange(nameof(LineY1)); } }
    public double LineX2 { get => _lineX2; private set { _lineX2 = value; NotifyOfPropertyChange(nameof(LineX2)); } }
    public double LineY2 { get => _lineY2; private set { _lineY2 = value; NotifyOfPropertyChange(nameof(LineY2)); } }

    /// <summary>끝점1=카메라 중점, 끝점2=카메라→팝업중심 선분이 팝업 사각형 경계와 만나는 점(좌/우/상/하 자동).</summary>
    private void RecomputeLine()
    {
        var cx = _canvasLeft + _popupWidth / 2;
        var cy = _canvasTop + _popupHeight / 2;
        var dx = _cameraScreenX - cx;
        var dy = _cameraScreenY - cy;

        LineX1 = _cameraScreenX;
        LineY1 = _cameraScreenY;

        if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) { LineX2 = cx; LineY2 = cy; return; }

        var scaleX = Math.Abs(dx) > 1e-6 ? (_popupWidth / 2) / Math.Abs(dx) : double.PositiveInfinity;
        var scaleY = Math.Abs(dy) > 1e-6 ? (_popupHeight / 2) / Math.Abs(dy) : double.PositiveInfinity;
        var scale = Math.Min(Math.Min(scaleX, scaleY), 1.0);   // 팝업 경계까지(카메라가 안쪽이면 중심)

        LineX2 = cx + dx * scale;
        LineY2 = cy + dy * scale;
    }

    /// <summary>"크게보기" 토글 상태(런타임만, 영속 안 함 — Q4).</summary>
    public bool IsLarge { get => _isLarge; private set { _isLarge = value; NotifyOfPropertyChange(nameof(IsLarge)); } }

    // ── PTZ 제어 상태(CameraPopup_PTZ_Control) ────────────────────────────────
    private bool _isSelected;
    private bool _isPtzCapable;
    private bool _isPtzLoading;
    private bool _isPanelExpanded;

    /// <summary>단일 선택 상태(MapViewModel.SelectedCameraPopup이 상호배타 설정). (FR-SEL-01)</summary>
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; NotifyOfPropertyChange(nameof(IsSelected)); } }

    /// <summary>PTZ 제어 가능 여부(MapViewModel이 IPtzController.EnsureReady 후 설정). false면 우버튼 입력 차단. (FR-GATE-01)</summary>
    public bool IsPtzCapable { get => _isPtzCapable; set { if (_isPtzCapable == value) return; _isPtzCapable = value; NotifyOfPropertyChange(nameof(IsPtzCapable)); } }

    /// <summary>ONVIF PTZ 준비(InitializeFull+GetNode, 수 초 소요) 진행 중 — "PTZ 준비 중…" 배지 표시용. 끝나면 IsPtzCapable로 결정.</summary>
    public bool IsPtzLoading { get => _isPtzLoading; set { if (_isPtzLoading == value) return; _isPtzLoading = value; NotifyOfPropertyChange(nameof(IsPtzLoading)); } }

    // ── PTZ 속도(ContinuousMove 속도, 사용자 조절) — PTZ 탭 슬라이더/텍스트박스. [0.1, 1.0]. ────────────
    private double _panTiltSpeed = 0.6;   // 팬/틸트 ContinuousMove 속도(패드/드래그)
    private double _zoomSpeed = 0.6;       // 줌 ContinuousMove 속도(휠)

    /// <summary>패드/드래그 PanTilt 속도(ContinuousMove 속도 크기 [0.1,1.0]). MapViewModel이 ContinuousMove에 전달. 슬라이더+직접입력.</summary>
    public double PanTiltSpeed
    {
        get => _panTiltSpeed;
        set { var v = Math.Round(Math.Clamp(value, 0.1, 1.0), 1, MidpointRounding.AwayFromZero); if (Math.Abs(_panTiltSpeed - v) < 1e-6) return; _panTiltSpeed = v; NotifyOfPropertyChange(nameof(PanTiltSpeed)); }
    }

    /// <summary>휠 줌 속도(ContinuousMove 줌 속도 크기 [0.1,1.0]). MapViewModel이 ContinuousMove(zoomVel)에 전달. 슬라이더+직접입력.</summary>
    public double ZoomSpeed
    {
        get => _zoomSpeed;
        set { var v = Math.Round(Math.Clamp(value, 0.1, 1.0), 1, MidpointRounding.AwayFromZero); if (Math.Abs(_zoomSpeed - v) < 1e-6) return; _zoomSpeed = v; NotifyOfPropertyChange(nameof(ZoomSpeed)); }
    }

    /// <summary>하단 [PTZ][프리셋][옵션] 탭 패널 펼침 여부(우버튼 짧은클릭 토글). (FR-UI-01)</summary>
    public bool IsPanelExpanded { get => _isPanelExpanded; set { if (_isPanelExpanded == value) return; _isPanelExpanded = value; NotifyOfPropertyChange(nameof(IsPanelExpanded)); NotifyOfPropertyChange(nameof(ControlHeight)); } }

    /// <summary>컨트롤 패널(아코디언) 토글.</summary>
    internal void TogglePanel() => IsPanelExpanded = !IsPanelExpanded;

    private ICommand? _togglePanelCommand;
    /// <summary>헤더 옵션 버튼 → 컨트롤 패널(PTZ/프리셋/옵션) 펼침/접기.</summary>
    public ICommand TogglePanelCommand => _togglePanelCommand ??= new RelayCommand(() => TogglePanel());

    private int _activeTab;   // 0=PTZ, 1=프리셋, 2=옵션
    /// <summary>활성 탭(0=PTZ / 1=프리셋 / 2=옵션). (FR-UI-02)</summary>
    public int ActiveTab { get => _activeTab; set { if (_activeTab == value) return; _activeTab = value; NotifyOfPropertyChange(nameof(ActiveTab)); } }

    private ICommand? _selectTabCommand;
    public ICommand SelectTabCommand => _selectTabCommand ??= new RelayCommand(p =>
    {
        if (!int.TryParse(p?.ToString(), out var i)) return;
        ActiveTab = i;
        IsPanelExpanded = true;
        if (i == 1) RaisePresetsReload();   // 프리셋 탭 진입 시 DB 재조회
        else if (i == 2) RaiseOptionsReload();   // 옵션 탭 진입 시 영상 옵션 조회
    });

    /// <summary>PTZ 탭 방향 버튼(8방향) → MapViewModel이 IPtzController로 상대 이동. (FR-UI-02)</summary>
    public event EventHandler<PtzNudgeEventArgs>? PtzNudgeRequested;
    /// <summary>PTZ 정지 버튼.</summary>
    public event EventHandler? PtzStopRequested;

    private ICommand? _nudgeCommand;
    public ICommand NudgeCommand => _nudgeCommand ??= new RelayCommand(p => RaiseNudge(p?.ToString()));

    private ICommand? _stopCommand;
    public ICommand StopCommand => _stopCommand ??= new RelayCommand(() => PtzStopRequested?.Invoke(this, EventArgs.Empty));

    /// <summary>방향 패드 버튼 누름 → 해당 방향 연속 이동 시작(컨트롤이 PreviewMouseDown에서 호출). dx/dy ∈ {-1,0,1}.</summary>
    internal void RaisePadPress(int dx, int dy) => PtzNudgeRequested?.Invoke(this, new PtzNudgeEventArgs(dx, dy));
    /// <summary>방향 패드 버튼 뗌/정지 → 이동 중지(컨트롤이 PreviewMouseUp에서 호출).</summary>
    internal void RaisePtzStop() => PtzStopRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseNudge(string? dir)
    {
        var (dx, dy) = dir switch
        {
            "UL" => (-1d, -1d), "U" => (0d, -1d), "UR" => (1d, -1d),
            "L" => (-1d, 0d), "R" => (1d, 0d),
            "DL" => (-1d, 1d), "D" => (0d, 1d), "DR" => (1d, 1d),
            _ => (0d, 0d)
        };
        if (dx != 0 || dy != 0) PtzNudgeRequested?.Invoke(this, new PtzNudgeEventArgs(dx, dy));
    }

    // ── 프리셋 탭(로컬 DB) ────────────────────────────────────────────────────
    private readonly BindableCollection<IPtzPresetModel> _presets = new();
    /// <summary>카메라 프리셋 목록(MapViewModel이 DB에서 로드해 주입). (FR-PRESET-01)</summary>
    public BindableCollection<IPtzPresetModel> Presets => _presets;

    /// <summary>Home 프리셋 존재 여부([Home 이동] 활성 판단). (FR-PRESET-07)</summary>
    public bool HasHomePreset => _presets.Any(p => p.IsHome);

    /// <summary>MapViewModel이 DB 로드 결과를 주입(UI 스레드).</summary>
    internal void SetPresets(IEnumerable<IPtzPresetModel> presets)
    {
        _presets.Clear();
        _presets.AddRange(presets);
        NotifyOfPropertyChange(nameof(HasHomePreset));
    }

    public event EventHandler? PresetsReloadRequested;       // 탭 진입/변경 후 재조회 요청
    public event EventHandler<IPtzPresetModel>? PresetGotoRequested;
    public event EventHandler<string>? PresetSaveRequested;  // 인라인 이름 확정
    public event EventHandler<IPtzPresetModel>? PresetDeleteRequested;
    public event EventHandler<IPtzPresetModel>? PresetHomeRequested;

    internal void RaisePresetsReload() => PresetsReloadRequested?.Invoke(this, EventArgs.Empty);

    private bool _isSavingPreset;
    /// <summary>프리셋 저장 인라인 이름 입력 표시 여부. (FR-PRESET-03)</summary>
    public bool IsSavingPreset { get => _isSavingPreset; set { if (_isSavingPreset == value) return; _isSavingPreset = value; NotifyOfPropertyChange(nameof(IsSavingPreset)); } }

    private string _newPresetName = string.Empty;
    public string NewPresetName { get => _newPresetName; set { _newPresetName = value; NotifyOfPropertyChange(nameof(NewPresetName)); } }

    private ICommand? _gotoPresetCommand, _deletePresetCommand, _setHomeCommand, _gotoHomeCommand;
    private ICommand? _savePresetCommand, _confirmSaveCommand, _cancelSaveCommand;

    public ICommand GotoPresetCommand => _gotoPresetCommand ??= new RelayCommand(p => { if (p is IPtzPresetModel m) PresetGotoRequested?.Invoke(this, m); });
    public ICommand DeletePresetCommand => _deletePresetCommand ??= new RelayCommand(p => { if (p is IPtzPresetModel m) PresetDeleteRequested?.Invoke(this, m); });
    public ICommand SetHomeCommand => _setHomeCommand ??= new RelayCommand(p => { if (p is IPtzPresetModel m) PresetHomeRequested?.Invoke(this, m); });
    public ICommand GotoHomeCommand => _gotoHomeCommand ??= new RelayCommand(() => { var h = _presets.FirstOrDefault(p => p.IsHome); if (h != null) PresetGotoRequested?.Invoke(this, h); });

    /// <summary>[현재위치 저장] → 인라인 이름 입력 시작.</summary>
    public ICommand SavePresetCommand => _savePresetCommand ??= new RelayCommand(() => { NewPresetName = $"Preset_{_presets.Count + 1}"; IsSavingPreset = true; });
    public ICommand ConfirmSavePresetCommand => _confirmSaveCommand ??= new RelayCommand(() =>
    {
        var name = NewPresetName?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        PresetSaveRequested?.Invoke(this, name);
        IsSavingPreset = false;
    });
    public ICommand CancelSavePresetCommand => _cancelSaveCommand ??= new RelayCommand(() => IsSavingPreset = false);

    // ── 옵션 탭(영상: 주야간/포커스) ──────────────────────────────────────────
    private bool _isImagingCapable;
    /// <summary>영상 옵션 지원 여부(미지원이면 안내). (FR-OPT-03)</summary>
    public bool IsImagingCapable { get => _isImagingCapable; set { if (_isImagingCapable == value) return; _isImagingCapable = value; NotifyOfPropertyChange(nameof(IsImagingCapable)); } }

    private string _irCutFilterMode = "AUTO";
    /// <summary>주야간(IrCutFilter): "ON"=주간 / "OFF"=야간 / "AUTO". (FR-OPT-01)</summary>
    public string IrCutFilterMode { get => _irCutFilterMode; set { if (_irCutFilterMode == value) return; _irCutFilterMode = value; NotifyOfPropertyChange(nameof(IrCutFilterMode)); } }

    private bool _isAutoFocus = true;
    /// <summary>오토포커스 여부(false=수동). (FR-OPT-02)</summary>
    public bool IsAutoFocus { get => _isAutoFocus; set { if (_isAutoFocus == value) return; _isAutoFocus = value; NotifyOfPropertyChange(nameof(IsAutoFocus)); } }

    /// <summary>MapViewModel이 ONVIF 조회 결과를 주입(UI 스레드).</summary>
    internal void SetImagingState(string irCutFilter, bool autoFocus)
    {
        IrCutFilterMode = string.IsNullOrEmpty(irCutFilter) ? "AUTO" : irCutFilter.ToUpperInvariant();
        IsAutoFocus = autoFocus;
    }

    public event EventHandler? OptionsReloadRequested;
    public event EventHandler<string>? IrCutFilterRequested;
    public event EventHandler<bool>? AutoFocusRequested;

    internal void RaiseOptionsReload() => OptionsReloadRequested?.Invoke(this, EventArgs.Empty);

    private ICommand? _setIrCutFilterCommand, _setAutoFocusCommand;
    public ICommand SetIrCutFilterCommand => _setIrCutFilterCommand ??= new RelayCommand(p => { var m = p?.ToString(); if (!string.IsNullOrEmpty(m)) IrCutFilterRequested?.Invoke(this, m); });
    public ICommand SetAutoFocusCommand => _setAutoFocusCommand ??= new RelayCommand(p => { if (bool.TryParse(p?.ToString(), out var b)) AutoFocusRequested?.Invoke(this, b); });

    public ICommand CloseCommand =>
        _closeCommand ??= new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

    public ICommand ToggleSizeCommand =>
        _toggleSizeCommand ??= new RelayCommand(ToggleSize);

    private void ToggleSize()
    {
        IsLarge = !IsLarge;
        PopupWidth = IsLarge ? LargeWidth : DefaultWidth;
        PopupHeight = IsLarge ? LargeHeight : DefaultHeight;
    }

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;   // 멱등(타이머 Tick + 수동 Close 동시 진입 방어)
        _disposed = true;

        // Hub Lease 해제(C-03: Row가 Stop→Dispose 순서 담당)
        try { await _row.DisposeAsync().ConfigureAwait(false); }
        catch { /* 종료 경로 — 무해 */ }
        CloseRequested = null;
    }
}

/// <summary>우버튼 드래그-PTZ 완료 이벤트 인자 — 픽셀 델타 + 영상 영역 치수(정규화·변환 기준).</summary>
public sealed class PtzDragEventArgs : EventArgs
{
    public PtzDragEventArgs(double dx, double dy, double imageW, double imageH)
    {
        Dx = dx; Dy = dy; ImageWidth = imageW; ImageHeight = imageH;
    }

    public double Dx { get; }
    public double Dy { get; }
    public double ImageWidth { get; }
    public double ImageHeight { get; }
}

/// <summary>PTZ 탭 방향 패드 nudge 인자 — 방향 단위벡터(-1/0/1).</summary>
public sealed class PtzNudgeEventArgs : EventArgs
{
    public PtzNudgeEventArgs(double dx, double dy) { Dx = dx; Dy = dy; }
    public double Dx { get; }
    public double Dy { get; }
}
