namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 카메라 팝업 설정 한 벌 (camera-popup-modes PRD §3 설정 모델)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 카메라 팝업 설정의 <b>값 객체</b>(record — 같은 값이면 같다). 설정 화면의 초안 · 저장본, 팝업 호스트 · 창 관리자가
/// 읽는 값이 모두 이 모양이다. 저장 모양(<see cref="CameraPopupSettingsRecord"/>)과 옛 키 이관은
/// <see cref="CameraPopupSettingsCodec"/> 이 맡는다.
/// </summary>
/// <remarks>
/// 좌표 · 크기는 <b>물리 픽셀</b>이다(모니터 목록 <see cref="DisplayMonitorInfo"/> 와 같은 단위).
/// 첫 창 위치는 고른 모니터 <b>작업 영역 왼쪽 위</b>에서 잰 거리다.
/// </remarks>
public sealed record CameraPopupSettings
{
    #region - 범위(PRD FR-10 · 11 · 12 · 15 · 08) -
    public const int MinMaxWindows = 1;
    public const int MaxMaxWindows = 10;
    public const int MinWindowWidth = 320;
    public const int MinWindowHeight = 200;
    public const int MaxWindowSide = 7680;
    public const int MaxCascadeStep = 200;
    public const int MinCloseTimerSeconds = 5;
    public const int MaxCloseTimerSeconds = 3600;
    public const int MinAutoCloseSeconds = 1;
    public const int MaxAutoCloseSeconds = 3600;
    public const int MaxBrokerMonitor = 16;
    /// <summary>브로커 칸 — 0 = 자동, 1~9 = NVR 쪽 3×3 칸 번호.</summary>
    public const int MaxBrokerCell = 9;
    public const int MinBrokerTimeoutSeconds = 1;
    public const int MaxBrokerTimeoutSeconds = 60;
    #endregion

    // ── 모드 · 제공자 ─────────────────────────────────────────────
    public CameraPopupMode Mode { get; init; } = CameraPopupMode.Self;
    public VideoProviderKind Provider { get; init; } = VideoProviderKind.Onvif;

    /// <summary>외부 VMS — 자리만(PRD FR-18). 구현 없음.</summary>
    public string VmsKind { get; init; } = string.Empty;
    public string VmsServerUrl { get; init; } = string.Empty;
    public string VmsAccount { get; init; } = string.Empty;

    // ── 더블클릭 팝업(현행 키 IsAutoDiscard · TimeoutSeconds) ────────
    public bool DoubleClickAutoClose { get; init; } = true;
    public int DoubleClickAutoCloseSeconds { get; init; } = 15;

    // ── 탐지 팝업 — 이벤트 창 ─────────────────────────────────────
    public bool EventWindowOnDetection { get; init; } = true;
    public bool EventWindowOnMalfunction { get; init; }
    public int CamerasPerWindow { get; init; } = 6;
    public CameraPopupGridLayout GridLayout { get; init; } = new(3, 2);
    public int MaxOpenWindows { get; init; } = MaxMaxWindows;

    /// <summary>대상 모니터(<see cref="CameraPopupMonitorId"/> 형식). 비면 주 모니터.</summary>
    public string TargetMonitorId { get; init; } = string.Empty;
    public int FirstWindowX { get; init; } = 24;
    public int FirstWindowY { get; init; } = 24;
    public int WindowWidth { get; init; } = 960;
    public int WindowHeight { get; init; } = 600;
    public int CascadeStepPx { get; init; } = 24;
    public bool AlwaysOnTop { get; init; } = true;

    // ── 창 닫기(PRD FR-15) ───────────────────────────────────────
    public bool CloseOnActionReport { get; init; } = true;
    public bool CloseByTimer { get; init; } = true;
    public int CloseTimerSeconds { get; init; } = 60;
    public bool ReturnHomePresetOnClose { get; init; } = true;

    // ── 브로커 요청(PRD FR-08) ───────────────────────────────────
    public int BrokerMonitor { get; init; } = 1;
    public int BrokerCell { get; init; }
    public CameraPopupOnOccupied BrokerOnOccupied { get; init; } = CameraPopupOnOccupied.Replace;
    public int BrokerResponseTimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// 범위 밖 값을 안쪽으로 당긴 사본. 격자는 창당 카메라 수에 맞춰 스냅한다.
    /// 외부 VMS 는 구현이 없어 <see cref="VideoProviderKind.Onvif"/> 로 되돌린다(설정 파일을 손으로 고친 경우 방어).
    /// </summary>
    public CameraPopupSettings Normalize()
    {
        var cameras = CameraPopupGridLayouts.ClampCount(CamerasPerWindow);
        return this with
        {
            Mode = Enum.IsDefined(Mode) ? Mode : CameraPopupMode.Self,
            Provider = Provider is VideoProviderKind.Onvif or VideoProviderKind.RtspUrl ? Provider : VideoProviderKind.Onvif,
            VmsKind = VmsKind ?? string.Empty,
            VmsServerUrl = VmsServerUrl ?? string.Empty,
            VmsAccount = VmsAccount ?? string.Empty,
            DoubleClickAutoCloseSeconds = Math.Clamp(DoubleClickAutoCloseSeconds, MinAutoCloseSeconds, MaxAutoCloseSeconds),
            CamerasPerWindow = cameras,
            GridLayout = CameraPopupGridLayouts.Snap(cameras, GridLayout),
            MaxOpenWindows = Math.Clamp(MaxOpenWindows, MinMaxWindows, MaxMaxWindows),
            TargetMonitorId = TargetMonitorId ?? string.Empty,
            FirstWindowX = Math.Max(0, FirstWindowX),
            FirstWindowY = Math.Max(0, FirstWindowY),
            WindowWidth = Math.Clamp(WindowWidth, MinWindowWidth, MaxWindowSide),
            WindowHeight = Math.Clamp(WindowHeight, MinWindowHeight, MaxWindowSide),
            CascadeStepPx = Math.Clamp(CascadeStepPx, 0, MaxCascadeStep),
            CloseTimerSeconds = Math.Clamp(CloseTimerSeconds, MinCloseTimerSeconds, MaxCloseTimerSeconds),
            BrokerMonitor = Math.Clamp(BrokerMonitor, 1, MaxBrokerMonitor),
            BrokerCell = Math.Clamp(BrokerCell, 0, MaxBrokerCell),
            BrokerOnOccupied = Enum.IsDefined(BrokerOnOccupied) ? BrokerOnOccupied : CameraPopupOnOccupied.Replace,
            BrokerResponseTimeoutSeconds = Math.Clamp(BrokerResponseTimeoutSeconds, MinBrokerTimeoutSeconds, MaxBrokerTimeoutSeconds),
        };
    }

    /// <summary>탐지 · 장애 이벤트 창을 띄우는가(자체 모드에서만 — 브로커 모드의 탐지는 프록시 매니저 몫, PRD FR-07).</summary>
    public bool OpensEventWindows => Mode == CameraPopupMode.Self && (EventWindowOnDetection || EventWindowOnMalfunction);
}
