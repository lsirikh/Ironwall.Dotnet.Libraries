using System.Collections.ObjectModel;
using System.Globalization;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 설정 콘솔 이벤트 절 "카메라 팝업 연동" 블록 (camera-popup-modes PRD T-03)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 카메라 팝업 설정 블록 — 초안을 들고 있다가 설정 창 [저장]에서 한 번에 쓴다(SB §2 와이어프레임 v0.8).
/// </summary>
/// <remarks>
/// <para><b>초안 규칙은 설정 창과 같다</b>: 손대면 <see cref="DirtyCount"/> 가 늘고 [저장] · [되돌리기] 가 켜진다.
/// 저장은 <see cref="Apply"/>, 되돌리기는 <see cref="Revert"/>. 호스트가 이 둘을 저장 막대에 잇는다.</para>
/// <para>모드별 보이는 칸(PRD FR-02): 자체 = 영상 · 제어 방식 + 더블클릭 + 탐지 팝업, 브로커 = 브로커 요청만,
/// 사용 안 함 = 안내 한 줄만.</para>
/// </remarks>
public sealed class CameraPopupSettingsViewModel : CameraPopupObservable
{
    #region - 묶음 이름(Touched 인덱서 키) -
    public const string GroupMode = "Mode";
    public const string GroupProvider = "Provider";
    public const string GroupDoubleClick = "DoubleClick";
    public const string GroupTrigger = "Trigger";
    public const string GroupCameras = "Cameras";
    public const string GroupMaxWindows = "MaxWindows";
    public const string GroupMonitor = "Monitor";
    public const string GroupWindow = "Window";
    public const string GroupClose = "Close";
    public const string GroupBrokerTarget = "BrokerTarget";
    public const string GroupBrokerOccupied = "BrokerOccupied";
    public const string GroupBrokerTimeout = "BrokerTimeout";

    /// <summary>
    /// 초안 ↔ 저장본 비교 대상. VMS 세 칸은 자리만이라 고칠 수 없어 뺀다.
    /// </summary>
    internal static readonly (string Group, string Name, Func<CameraPopupSettings, object> Get)[] TrackedFields =
    {
        (GroupMode, nameof(CameraPopupSettings.Mode), s => s.Mode),
        (GroupProvider, nameof(CameraPopupSettings.Provider), s => s.Provider),
        (GroupDoubleClick, nameof(CameraPopupSettings.DoubleClickAutoClose), s => s.DoubleClickAutoClose),
        (GroupDoubleClick, nameof(CameraPopupSettings.DoubleClickAutoCloseSeconds), s => s.DoubleClickAutoCloseSeconds),
        (GroupTrigger, nameof(CameraPopupSettings.EventWindowOnDetection), s => s.EventWindowOnDetection),
        (GroupTrigger, nameof(CameraPopupSettings.EventWindowOnMalfunction), s => s.EventWindowOnMalfunction),
        (GroupCameras, nameof(CameraPopupSettings.CamerasPerWindow), s => s.CamerasPerWindow),
        (GroupCameras, nameof(CameraPopupSettings.GridLayout), s => s.GridLayout),
        (GroupMaxWindows, nameof(CameraPopupSettings.MaxOpenWindows), s => s.MaxOpenWindows),
        (GroupMonitor, nameof(CameraPopupSettings.TargetMonitorId), s => s.TargetMonitorId),
        (GroupMonitor, nameof(CameraPopupSettings.FirstWindowX), s => s.FirstWindowX),
        (GroupMonitor, nameof(CameraPopupSettings.FirstWindowY), s => s.FirstWindowY),
        (GroupWindow, nameof(CameraPopupSettings.WindowWidth), s => s.WindowWidth),
        (GroupWindow, nameof(CameraPopupSettings.WindowHeight), s => s.WindowHeight),
        (GroupWindow, nameof(CameraPopupSettings.CascadeStepPx), s => s.CascadeStepPx),
        (GroupWindow, nameof(CameraPopupSettings.AlwaysOnTop), s => s.AlwaysOnTop),
        (GroupClose, nameof(CameraPopupSettings.CloseOnActionReport), s => s.CloseOnActionReport),
        (GroupClose, nameof(CameraPopupSettings.CloseByTimer), s => s.CloseByTimer),
        (GroupClose, nameof(CameraPopupSettings.CloseTimerSeconds), s => s.CloseTimerSeconds),
        (GroupClose, nameof(CameraPopupSettings.ReturnHomePresetOnClose), s => s.ReturnHomePresetOnClose),
        (GroupBrokerTarget, nameof(CameraPopupSettings.BrokerMonitor), s => s.BrokerMonitor),
        (GroupBrokerTarget, nameof(CameraPopupSettings.BrokerCell), s => s.BrokerCell),
        (GroupBrokerOccupied, nameof(CameraPopupSettings.BrokerOnOccupied), s => s.BrokerOnOccupied),
        (GroupBrokerTimeout, nameof(CameraPopupSettings.BrokerResponseTimeoutSeconds), s => s.BrokerResponseTimeoutSeconds),
    };
    #endregion

    #region - 문구 -
    internal const string AutomationPrefix = "Settings.CameraPopup";
    internal const string NoMonitorsNote = "모니터를 읽지 못했습니다 — [다시 조회]를 누르세요. 창은 주 모니터에 뜹니다";
    internal const string ExternalVmsNote = "연동처가 생기면 추가됩니다 — 지금은 고를 수 없습니다";
    internal const string SaveFailedReason = "설정 파일에 쓰지 못했습니다";
    internal const string NotTakenReason = "저장했지만 다시 읽은 값이 다릅니다";
    internal const string FetchingMonitorsNote = "NVR 관제석에 모니터 목록을 묻는 중…";
    #endregion

    private static readonly CameraPopupSizeChoice[] SizePresets =
    {
        new(640, 400), new(800, 500), new(960, 600), new(1280, 800), new(1600, 1000), new(1920, 1080),
    };

    private readonly ICameraPopupSettingsPort _port;
    private readonly IDisplayMonitorProvider _monitors;

    private CameraPopupSettings _saved;
    private CameraPopupSettings _draft;

    private string _autoCloseText = string.Empty;
    private string _stepText = string.Empty;
    private string _timerText = string.Empty;
    private string _brokerTimeoutText = string.Empty;
    private string _autoCloseError = string.Empty;
    private string _stepError = string.Empty;
    private string _timerError = string.Empty;
    private string _brokerTimeoutError = string.Empty;

    private IReadOnlyList<CameraPopupLayoutChoice> _layoutChoices = Array.Empty<CameraPopupLayoutChoice>();
    private int _layoutChoicesFor = -1;
    private CameraPopupMonitorChoice? _selectedMonitor;
    private MonitorMatch _monitorMatch = MonitorMatch.NoMonitors;
    private string _placementNotice = string.Empty;

    private bool _isFetchingBrokerMonitors;
    private bool _brokerMonitorAttention;
    private string _brokerMonitorNote = string.Empty;

    private bool _pressed;
    private bool _dragging;
    private int _pressX;
    private int _pressY;

    public CameraPopupSettingsViewModel(ICameraPopupSettingsPort port, IDisplayMonitorProvider monitors)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));

        Touched = new CameraPopupTouchedMap(IsGroupTouched);

        ModeChoices = new[]
        {
            Choice(CameraPopupMode.Self, "GIS 자체 팝업", "Mode.Self", OnModePicked),
            Choice(CameraPopupMode.Broker, "브로커 요청", "Mode.Broker", OnModePicked),
            Choice(CameraPopupMode.None, "사용 안 함", "Mode.None", OnModePicked),
        };
        ProviderChoices = new[]
        {
            Choice(VideoProviderKind.Onvif, "ONVIF (기본)", "Provider.Onvif", OnProviderPicked),
            Choice(VideoProviderKind.RtspUrl, "RTSP 주소", "Provider.RtspUrl", OnProviderPicked),
            Choice(VideoProviderKind.ExternalVms, "외부 VMS API", "Provider.ExternalVms", OnProviderPicked,
                   isEnabled: false, toolTip: ExternalVmsNote),
        };
        CameraCountChoices = Enumerable.Range(CameraPopupGridLayouts.MinCameras, CameraPopupGridLayouts.MaxCameras)
            .Select(n => Choice(n, n.ToString(CultureInfo.InvariantCulture), $"Event.Cameras.{n}", OnCameraCountPicked))
            .ToList();
        OnOccupiedChoices = new[]
        {
            Choice(CameraPopupOnOccupied.Replace, "교체", "Broker.OnOccupied.Replace", OnOccupiedPicked),
            Choice(CameraPopupOnOccupied.Reject, "거부", "Broker.OnOccupied.Reject", OnOccupiedPicked),
        };
        MaxWindowChoices = Enumerable.Range(CameraPopupSettings.MinMaxWindows, CameraPopupSettings.MaxMaxWindows)
            .Select(n => new CameraPopupNumberChoice(n, $"{n} 개")).ToList();
        BrokerMonitorChoices = Enumerable.Range(1, 8).Select(n => new CameraPopupNumberChoice(n, $"모니터 {n}")).ToList();
        BrokerCellChoices = new[] { new CameraPopupNumberChoice(0, "칸 자동") }
            .Concat(Enumerable.Range(1, CameraPopupSettings.MaxBrokerCell).Select(n => new CameraPopupNumberChoice(n, $"칸 {n}")))
            .ToList();

        _saved = SafeLoad();
        _draft = _saved;
        RefreshMonitors();
        ResetTexts();
        SyncAll();
    }

    #region - 블록 계약(호스트 저장 막대) -
    /// <summary>초안이나 오류가 바뀌었다 — 호스트가 막대를 다시 그린다.</summary>
    public event EventHandler? DraftChanged;

    /// <summary>저장본과 다른 칸 수.</summary>
    public int DirtyCount => TrackedFields.Count(f => !Equals(f.Get(_draft), f.Get(_saved)));

    public bool IsDirty => DirtyCount > 0;

    /// <summary>글자로 넣는 칸 중 어긴 것이 있는가(있으면 [저장]이 꺼진다).</summary>
    public bool HasError => !string.IsNullOrEmpty(_autoCloseError) || !string.IsNullOrEmpty(_stepError)
                          || !string.IsNullOrEmpty(_timerError) || !string.IsNullOrEmpty(_brokerTimeoutError);

    /// <summary>지금 초안(정규화 전).</summary>
    public CameraPopupSettings Draft => _draft;

    /// <summary>마지막으로 저장(또는 읽은) 값.</summary>
    public CameraPopupSettings Saved => _saved;

    /// <summary>
    /// [저장] — 초안을 창구로 쓰고 <b>다시 읽어</b> 같은 값인지 확인한다(설정 창의 "실제로 들어간 것만 저장했다고 말한다" 규칙).
    /// </summary>
    public CameraPopupApplyResult Apply()
    {
        if (!IsDirty) return new CameraPopupApplyResult(false, true);
        if (HasError) return new CameraPopupApplyResult(false, false, "입력을 고쳐야 저장할 수 있습니다");

        var wanted = _draft.Normalize();
        try
        {
            _port.SaveCameraPopup(wanted);
        }
        catch (Exception ex)
        {
            return new CameraPopupApplyResult(false, false, SaveFailedReason, ex.Message);
        }

        var after = SafeLoad();
        if (after != wanted) return new CameraPopupApplyResult(false, false, NotTakenReason);

        _saved = after;
        _draft = after;
        ResetTexts();
        SyncAll();
        return new CameraPopupApplyResult(true, false);
    }

    /// <summary>[되돌리기] — 초안을 버린다. 창구는 부르지 않는다.</summary>
    public void Revert()
    {
        _draft = _saved;
        EndFirstWindowDrag(commit: false);
        ResetTexts();
        SyncAll();
    }

    /// <summary>
    /// 절에 들어왔다 — 저장본을 다시 읽는다. 초안이 있으면 초안은 그대로 둔다(설정 창 FR-05: 절을 넘나들어도 초안은 산다).
    /// </summary>
    public void Reload()
    {
        var wasDirty = IsDirty || HasError;
        _saved = SafeLoad();
        if (!wasDirty)
        {
            _draft = _saved;
            ResetTexts();
        }
        RefreshMonitors();
        SyncAll();
    }
    #endregion

    #region - 모드 · 제공자 -
    public IReadOnlyList<CameraPopupChoice> ModeChoices { get; }
    public IReadOnlyList<CameraPopupChoice> ProviderChoices { get; }

    public CameraPopupMode Mode
    {
        get => _draft.Mode;
        set => Update(_draft with { Mode = value });
    }

    public VideoProviderKind Provider
    {
        get => _draft.Provider;
        set
        {
            if (value == VideoProviderKind.ExternalVms) return;   // 자리만 — 고를 수 없다(PRD FR-18)
            Update(_draft with { Provider = value });
        }
    }

    /// <summary>자체 모드 묶음(영상 · 제어 방식 · 더블클릭 · 탐지 팝업)을 보이는가(PRD FR-02).</summary>
    public bool ShowSelfGroups => _draft.Mode == CameraPopupMode.Self;

    /// <summary>브로커 요청 묶음을 보이는가.</summary>
    public bool ShowBrokerGroup => _draft.Mode == CameraPopupMode.Broker;

    /// <summary>사용 안 함 안내를 보이는가.</summary>
    public bool ShowNoneNote => _draft.Mode == CameraPopupMode.None;

    /// <summary>모드 한 줄 설명.</summary>
    public string ModeNote => _draft.Mode switch
    {
        CameraPopupMode.Broker => "더블클릭하면 NVR 관제석에 팝업을 요청합니다(GIS 는 영상을 띄우지 않고 결과만 지도 하단에 알립니다). 탐지 팝업은 프록시 매니저가 처리합니다.",
        CameraPopupMode.None => "더블클릭하면 영상 대신 카메라 상세(속성)를 엽니다. 탐지 창은 뜨지 않습니다.",
        _ => "더블클릭은 지도 위 상자로, 탐지는 이벤트마다 창 하나로 띄웁니다.",
    };

    public string ProviderNote => _draft.Provider == VideoProviderKind.RtspUrl
        ? "장비에 저장된 RTSP 주소로 영상만 봅니다 — PTZ · 프리셋 메뉴는 꺼집니다."
        : "영상 주소 · PTZ 모두 카메라에 직접 묻습니다.";

    /// <summary>외부 VMS 칸 — 자리만(고칠 수 없다).</summary>
    public bool IsVmsEditable => false;

    /// <summary>외부 VMS 칸 묶음은 그 제공자를 골랐을 때만 보인다(지금은 고를 수 없으므로 늘 숨김 — 연동처가 생기면 칩을 켜면 된다).</summary>
    public bool ShowVmsFields => _draft.Provider == VideoProviderKind.ExternalVms;
    public string VmsKind => _draft.VmsKind;
    public string VmsServerUrl => _draft.VmsServerUrl;
    public string VmsAccount => _draft.VmsAccount;
    public string VmsNote => ExternalVmsNote;
    #endregion

    #region - 더블클릭 팝업 -
    public bool DoubleClickAutoClose
    {
        get => _draft.DoubleClickAutoClose;
        set => Update(_draft with { DoubleClickAutoClose = value });
    }

    public string DoubleClickAutoCloseSecondsText
    {
        get => _autoCloseText;
        set => EditNumber(ref _autoCloseText, ref _autoCloseError, value,
                          CameraPopupSettings.MinAutoCloseSeconds, CameraPopupSettings.MaxAutoCloseSeconds, "초",
                          n => _draft with { DoubleClickAutoCloseSeconds = n });
    }

    public string DoubleClickAutoCloseSecondsError => _autoCloseError;
    public bool HasDoubleClickAutoCloseSecondsError => !string.IsNullOrEmpty(_autoCloseError);
    #endregion

    #region - 탐지 팝업(이벤트 창) -
    public bool EventWindowOnDetection
    {
        get => _draft.EventWindowOnDetection;
        set => Update(_draft with { EventWindowOnDetection = value });
    }

    public bool EventWindowOnMalfunction
    {
        get => _draft.EventWindowOnMalfunction;
        set => Update(_draft with { EventWindowOnMalfunction = value });
    }

    public IReadOnlyList<CameraPopupChoice> CameraCountChoices { get; }

    public int CamerasPerWindow
    {
        get => _draft.CamerasPerWindow;
        set
        {
            var count = CameraPopupGridLayouts.ClampCount(value);
            // 수를 바꾸면 격자를 스냅한다 — 새 수에서 고를 수 없는 격자는 그 수의 기본으로(PRD FR-10).
            Update(_draft with { CamerasPerWindow = count, GridLayout = CameraPopupGridLayouts.Snap(count, _draft.GridLayout) });
        }
    }

    /// <summary>지금 카메라 수에서 고를 수 있는 격자 칩(미니 격자).</summary>
    public IReadOnlyList<CameraPopupLayoutChoice> LayoutChoices => _layoutChoices;

    public CameraPopupGridLayout GridLayout
    {
        get => _draft.GridLayout;
        set
        {
            if (!CameraPopupGridLayouts.IsAllowed(_draft.CamerasPerWindow, value)) return;
            Update(_draft with { GridLayout = value });
        }
    }

    public string LayoutNote
        => "카메라 수를 바꾸면 그 수에 맞는 격자만 나옵니다 — 표기는 가로×세로. 매핑 카메라가 더 많으면 창 꼬리에 +N.";

    public IReadOnlyList<CameraPopupNumberChoice> MaxWindowChoices { get; }

    public CameraPopupNumberChoice? SelectedMaxWindows
    {
        get => MaxWindowChoices.FirstOrDefault(c => c.Value == _draft.MaxOpenWindows);
        set { if (value is not null) Update(_draft with { MaxOpenWindows = value.Value }); }
    }
    #endregion

    #region - 모니터 · 첫 위치 · 크기 -
    public ObservableCollection<CameraPopupMonitorChoice> Monitors { get; } = new();

    public bool HasMonitors => Monitors.Count > 0;

    public CameraPopupMonitorChoice? SelectedMonitor
    {
        get => _selectedMonitor;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedMonitor)) return;
            _selectedMonitor = value;
            _monitorMatch = MonitorMatch.Exact;
            // 모니터를 바꾸면 첫 위치 · 크기를 새 작업 영역 안으로 당긴다(화면 밖 방지, PRD FR-12).
            var c = CameraPopupPlacement.ClampRelative(value.Monitor.WorkArea, _draft.FirstWindowX, _draft.FirstWindowY,
                                                       _draft.WindowWidth, _draft.WindowHeight);
            _placementNotice = c.Clamped ? "고른 모니터 안으로 첫 창 위치를 당겼습니다" : string.Empty;
            Update(_draft with { TargetMonitorId = value.Id, FirstWindowX = c.X, FirstWindowY = c.Y });
        }
    }

    /// <summary>모니터 줄 설명 — 조회 대수 · 못 찾은 경우 안내.</summary>
    public string MonitorNote => _monitorMatch switch
    {
        MonitorMatch.NoMonitors => NoMonitorsNote,
        MonitorMatch.FallbackPrimary => $"저장된 모니터({Describe(_draft.TargetMonitorId)})가 없어 주 모니터에 뜹니다 — 다른 모니터를 고르고 저장하세요",
        MonitorMatch.ResolutionChanged => "저장된 모니터의 해상도가 바뀌었습니다 — 위치를 확인하고 저장하세요",
        _ => $"조회된 {Monitors.Count}대 중 하나",
    };

    public bool MonitorNeedsAttention => _monitorMatch is MonitorMatch.NoMonitors or MonitorMatch.FallbackPrimary or MonitorMatch.ResolutionChanged;

    /// <summary>[다시 조회] — 모니터를 다시 읽고 저장된 대상을 다시 찾는다.</summary>
    public void RefreshMonitors()
    {
        IReadOnlyList<DisplayMonitorInfo> list;
        try { list = _monitors.GetMonitors() ?? Array.Empty<DisplayMonitorInfo>(); }
        catch { list = Array.Empty<DisplayMonitorInfo>(); }

        Monitors.Clear();
        for (var i = 0; i < list.Count; i++) Monitors.Add(new CameraPopupMonitorChoice(list[i], list[i].Describe(i + 1)));

        var resolution = CameraPopupPlacement.ResolveMonitor(list, _draft.TargetMonitorId);
        _monitorMatch = resolution.Match;
        _selectedMonitor = resolution.Monitor is null ? null : Monitors.FirstOrDefault(m => ReferenceEquals(m.Monitor, resolution.Monitor));
        SyncAll();
    }

    /// <summary>미리보기 기준 작업 영역 — 모니터를 못 읽었으면 1920×1040 가상 영역.</summary>
    public PixelRect WorkArea => _selectedMonitor?.Monitor.WorkArea ?? new PixelRect(0, 0, 1920, 1040);

    public double PreviewScale => CameraPopupPreviewMath.Scale(WorkArea);
    public double PreviewWidth => WorkArea.Width * PreviewScale;
    public double PreviewHeight => WorkArea.Height * PreviewScale;

    /// <summary>첫 창(끌 수 있는 것).</summary>
    public CameraPopupPreviewWindow FirstWindowPreview => PreviewWindows[0];

    /// <summary>계단으로 쌓일 뒤 창들(흐린 고스트) — 최대 2장(SB §2 그림과 같다).</summary>
    public IReadOnlyList<CameraPopupPreviewWindow> GhostWindows => PreviewWindows.Skip(1).ToList();

    private IReadOnlyList<CameraPopupPreviewWindow> PreviewWindows
    {
        get
        {
            var scale = PreviewScale;
            var count = Math.Clamp(_draft.MaxOpenWindows, 1, 3);
            var rects = CameraPopupPlacement.Cascade(WorkArea, _draft, count);
            return rects.Select((r, i) =>
            {
                var p = CameraPopupPreviewMath.ToPreview(WorkArea, r, scale);
                return new CameraPopupPreviewWindow(i + 1, p.Left, p.Top, p.Width, p.Height,
                                                    _draft.GridLayout.Columns, _draft.GridLayout.Rows, i == 0);
            }).ToList();
        }
    }

    /// <summary>미리보기 모서리 표지 — 계단 방향 · 간격.</summary>
    public string CascadeCaption => _draft.CascadeStepPx <= 0
        ? "간격 0 — 모든 창이 한 자리"
        : string.Create(CultureInfo.InvariantCulture, $"↘ 다음 창 +{_draft.CascadeStepPx} px");

    public int FirstWindowX => _draft.FirstWindowX;
    public int FirstWindowY => _draft.FirstWindowY;

    /// <summary>첫 위치 · 계단 설명.</summary>
    public string PlacementNote
    {
        get
        {
            var first = CameraPopupPlacement.FirstWindow(WorkArea, _draft.FirstWindowX, _draft.FirstWindowY, _draft.WindowWidth, _draft.WindowHeight);
            var period = CameraPopupPlacement.CascadePeriod(WorkArea, first, _draft.CascadeStepPx);
            var text = string.Create(CultureInfo.InvariantCulture,
                $"첫 창 x {_draft.FirstWindowX} · y {_draft.FirstWindowY} px(작업 영역 왼쪽 위 기준). 1번 창을 끌어 놓거나 방향키(Shift = 10칸)로 옮깁니다. ");
            text += period <= 1
                ? "간격이 0 이거나 자리가 없어 모든 창이 같은 자리에 뜹니다."
                : string.Create(CultureInfo.InvariantCulture, $"창 {period}개마다 화면 끝에 닿아 첫 위치로 돌아옵니다.");
            if (!string.IsNullOrEmpty(_placementNotice)) text = _placementNotice + " · " + text;
            return text;
        }
    }

    public IReadOnlyList<CameraPopupSizeChoice> SizeChoices
    {
        get
        {
            var current = new CameraPopupSizeChoice(_draft.WindowWidth, _draft.WindowHeight);
            return SizePresets.Contains(current) ? SizePresets : SizePresets.Prepend(current).ToArray();
        }
    }

    public CameraPopupSizeChoice SelectedSize
    {
        get => new(_draft.WindowWidth, _draft.WindowHeight);
        set
        {
            if (value is null) return;
            // 크기를 바꾸면 첫 위치가 화면 밖으로 밀릴 수 있다 — 작업 영역 안으로 당긴다.
            var c = CameraPopupPlacement.ClampRelative(WorkArea, _draft.FirstWindowX, _draft.FirstWindowY, value.Width, value.Height);
            _placementNotice = c.Width != value.Width || c.Height != value.Height
                ? "창이 모니터보다 커서 모니터 크기로 뜹니다"
                : c.Clamped ? "창이 화면 밖으로 나가지 않게 첫 위치를 당겼습니다" : string.Empty;
            Update(_draft with { WindowWidth = value.Width, WindowHeight = value.Height, FirstWindowX = c.X, FirstWindowY = c.Y });
        }
    }

    public string CascadeStepText
    {
        get => _stepText;
        set => EditNumber(ref _stepText, ref _stepError, value, 0, CameraPopupSettings.MaxCascadeStep, "px",
                          n => _draft with { CascadeStepPx = n });
    }

    public string CascadeStepError => _stepError;
    public bool HasCascadeStepError => !string.IsNullOrEmpty(_stepError);

    public bool AlwaysOnTop
    {
        get => _draft.AlwaysOnTop;
        set => Update(_draft with { AlwaysOnTop = value });
    }
    #endregion

    #region - 첫 창 끌기 · 키보드 (drag-first-ux: 캡처 드래그 · 8 DIU 데드존 · Esc 취소) -
    /// <summary>끄는 중인가(시각: 그림자).</summary>
    public bool IsDraggingFirstWindow => _dragging;

    /// <summary>1번 창을 눌렀다 — 누른 때 위치를 기억한다.</summary>
    public void PressFirstWindow()
    {
        _pressed = true;
        _dragging = false;
        _pressX = _draft.FirstWindowX;
        _pressY = _draft.FirstWindowY;
    }

    /// <summary>누른 자리에서 (dx, dy) DIU 움직였다. 데드존 안이면 아무 일도 없다. 끌기로 넘어갔으면 <c>true</c>.</summary>
    public bool DragFirstWindow(double dxDiu, double dyDiu)
    {
        if (!_pressed) return false;
        var next = CameraPopupPreviewMath.DragTo(WorkArea, _pressX, _pressY, _draft.WindowWidth, _draft.WindowHeight,
                                                 dxDiu, dyDiu, PreviewScale, _dragging);
        if (next is null) return false;

        if (!_dragging)
        {
            _dragging = true;
            Raise(nameof(IsDraggingFirstWindow));
        }
        _placementNotice = string.Empty;
        Update(_draft with { FirstWindowX = next.Value.X, FirstWindowY = next.Value.Y });
        return true;
    }

    /// <summary>
    /// 끌기 끝 — 놓았으면(<paramref name="commit"/>) 그 자리, Esc · 캡처 잃음이면 누른 때 자리로 되돌린다.
    /// </summary>
    /// <returns>끌기 중이었는가(데드존 안에서 뗐으면 <c>false</c> — 클릭).</returns>
    public bool EndFirstWindowDrag(bool commit)
    {
        var wasDragging = _dragging;
        if (_pressed && wasDragging && !commit)
            Update(_draft with { FirstWindowX = _pressX, FirstWindowY = _pressY });

        _pressed = false;
        if (_dragging)
        {
            _dragging = false;
            Raise(nameof(IsDraggingFirstWindow));
        }
        return wasDragging;
    }

    /// <summary>키보드 — 방향키 10 px, Shift 면 100 px.</summary>
    public void NudgeFirstWindow(int dirX, int dirY, bool big)
    {
        var next = CameraPopupPreviewMath.Nudge(WorkArea, _draft.FirstWindowX, _draft.FirstWindowY,
                                                _draft.WindowWidth, _draft.WindowHeight, dirX, dirY, big);
        _placementNotice = string.Empty;
        Update(_draft with { FirstWindowX = next.X, FirstWindowY = next.Y });
    }
    #endregion

    #region - 창 닫기 -
    public bool CloseOnActionReport
    {
        get => _draft.CloseOnActionReport;
        set => Update(_draft with { CloseOnActionReport = value });
    }

    public bool CloseByTimer
    {
        get => _draft.CloseByTimer;
        set => Update(_draft with { CloseByTimer = value });
    }

    public string CloseTimerSecondsText
    {
        get => _timerText;
        set => EditNumber(ref _timerText, ref _timerError, value,
                          CameraPopupSettings.MinCloseTimerSeconds, CameraPopupSettings.MaxCloseTimerSeconds, "초",
                          n => _draft with { CloseTimerSeconds = n });
    }

    public string CloseTimerSecondsError => _timerError;
    public bool HasCloseTimerSecondsError => !string.IsNullOrEmpty(_timerError);

    public bool ReturnHomePresetOnClose
    {
        get => _draft.ReturnHomePresetOnClose;
        set => Update(_draft with { ReturnHomePresetOnClose = value });
    }

    public string CloseNote => _draft.CloseOnActionReport || _draft.CloseByTimer
        ? "둘 다 켜면 먼저 오는 쪽 · 창을 만지면 타이머를 다시 셉니다. 창을 닫아도 카드 · 지도 깜빡임은 조치보고로만 풀립니다."
        : "둘 다 끄면 사람이 닫을 때까지 남습니다.";
    #endregion

    #region - 브로커 요청 -
    /// <summary>관제석 식별자(읽기 전용).</summary>
    public string ClientId
    {
        get
        {
            try { return string.IsNullOrWhiteSpace(_port.CameraPopupClientId) ? "(없음)" : _port.CameraPopupClientId; }
            catch { return "(읽지 못함)"; }
        }
    }

    /// <summary>모니터 칸 — 처음엔 1~8, [모니터 목록 가져오기]가 성공하면 NVR 관제석이 알려 준 목록(PRD FR-08).</summary>
    public IReadOnlyList<CameraPopupNumberChoice> BrokerMonitorChoices { get; private set; }

    /// <summary>[모니터 목록 가져오기]가 응답을 기다리는 중.</summary>
    public bool IsFetchingBrokerMonitors => _isFetchingBrokerMonitors;

    /// <summary>[모니터 목록 가져오기] 누를 수 있는가(기다리는 동안 꺼진다 — 연타 방지).</summary>
    public bool CanFetchBrokerMonitors => !_isFetchingBrokerMonitors;

    /// <summary>가져오기 결과 한 줄(성공 · 실패 사유 · 기다림). 비면 칸 설명만 보인다.</summary>
    public string BrokerMonitorNote => _brokerMonitorNote;

    public bool HasBrokerMonitorNote => !string.IsNullOrEmpty(_brokerMonitorNote);

    /// <summary>가져오기가 실패했거나 저장된 모니터가 목록에 없다(칸 아래 경고색).</summary>
    public bool BrokerMonitorNeedsAttention => _brokerMonitorAttention;

    /// <summary>
    /// [모니터 목록 가져오기] — NVR Manager 에 <c>POPUP_LAYOUT_GET</c>(창구 경유). 예외를 내지 않는다.
    /// 실패는 칸 아래 한 줄로만 알린다(목록은 그대로 둔다). 초안의 모니터 값은 건드리지 않는다.
    /// </summary>
    public async Task FetchBrokerMonitorsAsync(CancellationToken ct = default)
    {
        if (_isFetchingBrokerMonitors) return;
        _isFetchingBrokerMonitors = true;
        _brokerMonitorAttention = false;
        _brokerMonitorNote = FetchingMonitorsNote;
        Raise(string.Empty);

        NvrPopupLayoutResult result;
        try
        {
            result = await _port.FetchBrokerLayoutAsync(_draft.Normalize().BrokerResponseTimeoutSeconds, ct)
                     ?? NvrPopupLayoutResult.Unavailable;
        }
        catch (Exception ex)
        {
            result = NvrPopupLayoutResult.Fail($"모니터 목록을 가져오지 못했습니다 — {ex.Message}");
        }

        _isFetchingBrokerMonitors = false;
        ApplyBrokerLayout(result);
        SyncAll();
    }

    private void ApplyBrokerLayout(NvrPopupLayoutResult result)
    {
        if (!result.Success || result.Monitors.Count == 0)
        {
            _brokerMonitorAttention = true;
            _brokerMonitorNote = string.IsNullOrWhiteSpace(result.Message) ? "모니터 목록을 가져오지 못했습니다" : result.Message;
            return;
        }

        var choices = result.Monitors.Select(m => new CameraPopupNumberChoice(m.Index, m.Describe())).ToList();
        var current = _draft.BrokerMonitor;
        var missing = choices.All(c => c.Value != current);
        if (missing) choices.Add(new CameraPopupNumberChoice(current, string.Create(CultureInfo.InvariantCulture, $"모니터 {current} · 목록에 없음")));
        BrokerMonitorChoices = choices;

        var slot = result.DefaultMonitor is { } dm
            ? string.Create(CultureInfo.InvariantCulture, $" · NVR 기본 자리 모니터 {dm}{(result.DefaultCell is { } dc ? $" · 칸 {dc}" : string.Empty)}")
            : string.Empty;
        _brokerMonitorAttention = missing;
        _brokerMonitorNote = missing
            ? string.Create(CultureInfo.InvariantCulture, $"저장된 모니터 {current} 가 관제석 목록({result.Monitors.Count}대)에 없습니다 — 다른 모니터를 고르세요")
            : string.Create(CultureInfo.InvariantCulture, $"관제석 모니터 {result.Monitors.Count}대를 가져왔습니다{slot}");
    }

    public CameraPopupNumberChoice? SelectedBrokerMonitor
    {
        get => BrokerMonitorChoices.FirstOrDefault(c => c.Value == _draft.BrokerMonitor);
        set { if (value is not null) Update(_draft with { BrokerMonitor = value.Value }); }
    }

    public IReadOnlyList<CameraPopupNumberChoice> BrokerCellChoices { get; }

    public CameraPopupNumberChoice? SelectedBrokerCell
    {
        get => BrokerCellChoices.FirstOrDefault(c => c.Value == _draft.BrokerCell);
        set { if (value is not null) Update(_draft with { BrokerCell = value.Value }); }
    }

    public IReadOnlyList<CameraPopupChoice> OnOccupiedChoices { get; }

    public CameraPopupOnOccupied BrokerOnOccupied
    {
        get => _draft.BrokerOnOccupied;
        set => Update(_draft with { BrokerOnOccupied = value });
    }

    public string BrokerTimeoutText
    {
        get => _brokerTimeoutText;
        set => EditNumber(ref _brokerTimeoutText, ref _brokerTimeoutError, value,
                          CameraPopupSettings.MinBrokerTimeoutSeconds, CameraPopupSettings.MaxBrokerTimeoutSeconds, "초",
                          n => _draft with { BrokerResponseTimeoutSeconds = n });
    }

    public string BrokerTimeoutError => _brokerTimeoutError;
    public bool HasBrokerTimeoutError => !string.IsNullOrEmpty(_brokerTimeoutError);
    #endregion

    #region - 손댐 -
    /// <summary>줄마다 손댐(커널 ConsoleField.IsTouched) — <c>{Binding Touched[Mode]}</c>.</summary>
    public CameraPopupTouchedMap Touched { get; }

    public bool IsGroupTouched(string group)
        => TrackedFields.Any(f => f.Group == group && !Equals(f.Get(_draft), f.Get(_saved)))
        || group switch
        {
            GroupDoubleClick => !string.IsNullOrEmpty(_autoCloseError),
            GroupWindow => !string.IsNullOrEmpty(_stepError),
            GroupClose => !string.IsNullOrEmpty(_timerError),
            GroupBrokerTimeout => !string.IsNullOrEmpty(_brokerTimeoutError),
            _ => false,
        };
    #endregion

    #region - 내부 -
    private CameraPopupChoice Choice(object value, string label, string id, Action<CameraPopupChoice> onPick,
                                     bool isEnabled = true, string toolTip = "")
        => new(value, label, $"{AutomationPrefix}.{id}", onPick, isEnabled, toolTip);

    private void OnModePicked(CameraPopupChoice c) => Mode = (CameraPopupMode)c.Value;
    private void OnProviderPicked(CameraPopupChoice c) => Provider = (VideoProviderKind)c.Value;
    private void OnCameraCountPicked(CameraPopupChoice c) => CamerasPerWindow = (int)c.Value;
    private void OnLayoutPicked(CameraPopupChoice c) => GridLayout = (CameraPopupGridLayout)c.Value;
    private void OnOccupiedPicked(CameraPopupChoice c) => BrokerOnOccupied = (CameraPopupOnOccupied)c.Value;

    private CameraPopupSettings SafeLoad()
    {
        try { return (_port.LoadCameraPopup() ?? new CameraPopupSettings()).Normalize(); }
        catch { return new CameraPopupSettings().Normalize(); }
    }

    private void Update(CameraPopupSettings next)
    {
        if (next == _draft) { SyncAll(); return; }
        _draft = next;
        SyncAll();
    }

    /// <summary>글자로 넣는 숫자 칸 — 범위 안 정수면 초안에 넣고, 아니면 오류만 남긴다(초안은 그대로).</summary>
    private void EditNumber(ref string text, ref string error, string? value, int min, int max, string unit,
                            Func<int, CameraPopupSettings> apply)
    {
        text = value ?? string.Empty;
        if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max)
        {
            error = string.Empty;
            _draft = apply(n);
        }
        else
        {
            error = string.Create(CultureInfo.InvariantCulture, $"{min}~{max} {unit} 사이 정수를 넣으세요");
        }
        SyncAll();
    }

    private void ResetTexts()
    {
        _autoCloseText = _draft.DoubleClickAutoCloseSeconds.ToString(CultureInfo.InvariantCulture);
        _stepText = _draft.CascadeStepPx.ToString(CultureInfo.InvariantCulture);
        _timerText = _draft.CloseTimerSeconds.ToString(CultureInfo.InvariantCulture);
        _brokerTimeoutText = _draft.BrokerResponseTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        _autoCloseError = _stepError = _timerError = _brokerTimeoutError = string.Empty;
        _placementNotice = string.Empty;
    }

    /// <summary>초안에서 칩 표시 · 격자 목록 · 미리보기를 맞추고 전부 다시 알린다.</summary>
    private void SyncAll()
    {
        foreach (var c in ModeChoices) c.Sync(Equals(c.Value, _draft.Mode));
        foreach (var c in ProviderChoices) c.Sync(Equals(c.Value, _draft.Provider));
        foreach (var c in CameraCountChoices) c.Sync(Equals(c.Value, _draft.CamerasPerWindow));
        foreach (var c in OnOccupiedChoices) c.Sync(Equals(c.Value, _draft.BrokerOnOccupied));

        if (_layoutChoicesFor != _draft.CamerasPerWindow)
        {
            _layoutChoicesFor = _draft.CamerasPerWindow;
            _layoutChoices = CameraPopupGridLayouts.Allowed(_draft.CamerasPerWindow)
                .Select(l => new CameraPopupLayoutChoice(l, l.Display, $"{AutomationPrefix}.Event.Layout.{l.Key}", OnLayoutPicked,
                                                         l.Columns, l.Rows, Math.Min(l.Capacity, _draft.CamerasPerWindow)))
                .ToList();
        }
        foreach (var c in _layoutChoices) c.Sync(Equals(c.Value, _draft.GridLayout));

        Raise(string.Empty);
        Touched.Refresh();
        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Describe(string id)
        => CameraPopupMonitorId.TryParse(id, out var device, out var w, out var h)
            ? (w > 0 ? $"{device} · {w}×{h}" : device)
            : id;
    #endregion
}
