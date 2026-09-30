using Ironwall.Dotnet.Libraries.Streaming.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 저장 모양 ↔ 설정 값 + 옛 키 이관 (camera-popup-modes PRD FR-01 · FR-18)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 설정 파일에서 읽은 것을 <see cref="CameraPopupSettings"/> 로, 그 반대로. 순수 함수.
/// </summary>
/// <remarks>
/// <para><b>이관 규칙</b>(PRD FR-01 · §3):</para>
/// <list type="bullet">
///   <item>모드 — 새 키 <c>CameraPopup.Mode</c> 가 읽히면 그것. 없거나 모르는 값이면 옛 <c>IsCameraPopupUsed</c>:
///         <c>true</c> → <see cref="CameraPopupMode.Self"/>, <c>false</c> → <see cref="CameraPopupMode.None"/>.</item>
///   <item>제공자 — 새 키 <c>CameraPopup.Provider</c> 가 읽히면 그것. 없으면 옛 <c>CameraPopupRtspSource</c>:
///         <c>Onvif</c> → <see cref="VideoProviderKind.Onvif"/>, <c>Url</c> → <see cref="VideoProviderKind.RtspUrl"/>
///         (지금 쓰던 동작을 그대로 잇는다 — 새 설치의 기본 ONVIF 는 설정 파일에 새 키로 적는다).</item>
///   <item>더블클릭 자동 닫기 — 옛 키 <c>IsAutoDiscard</c> · <c>TimeoutSeconds</c> 가 곧 정본이다(이관 아님).</item>
/// </list>
/// <para><b>저장할 때는 옛 키도 함께 맞춘다</b>(<see cref="LegacyIsCameraPopupUsed"/> · <see cref="LegacyRtspSource"/>) —
/// 제공자 전환(T-02) 전까지 지도 더블클릭은 옛 키를 읽기 때문이다.</para>
/// </remarks>
public static class CameraPopupSettingsCodec
{
    /// <summary>appsettings <c>AppSettings</c> 안의 새 키 이름.</summary>
    public const string SectionKey = "CameraPopup";

    /// <summary>
    /// 읽은 것 → 설정 값(정규화까지).
    /// </summary>
    /// <param name="record">새 키 덩어리. 없으면 <c>null</c>(이관).</param>
    /// <param name="legacyIsCameraPopupUsed">옛 <c>IsCameraPopupUsed</c>.</param>
    /// <param name="legacyRtspSource">옛 <c>CameraPopupRtspSource</c>(관대 파싱된 값).</param>
    /// <param name="autoDiscard">현행 <c>IsAutoDiscard</c>(더블클릭 자동 닫기).</param>
    /// <param name="timeoutSeconds">현행 <c>TimeoutSeconds</c>.</param>
    public static CameraPopupSettings Resolve(CameraPopupSettingsRecord? record,
                                              bool legacyIsCameraPopupUsed,
                                              EnumCameraPopupRtspSource legacyRtspSource,
                                              bool autoDiscard,
                                              int timeoutSeconds)
    {
        var r = record ?? new CameraPopupSettingsRecord { Mode = null, Provider = null };

        var mode = TryParseMode(r.Mode, out var parsedMode)
            ? parsedMode
            : legacyIsCameraPopupUsed ? CameraPopupMode.Self : CameraPopupMode.None;

        var provider = TryParseProvider(r.Provider, out var parsedProvider)
            ? parsedProvider
            : legacyRtspSource == EnumCameraPopupRtspSource.Onvif ? VideoProviderKind.Onvif : VideoProviderKind.RtspUrl;

        var layout = CameraPopupGridLayout.TryParse(r.GridLayout, out var parsedLayout)
            ? parsedLayout
            : CameraPopupGridLayouts.Default(r.CamerasPerWindow);

        return new CameraPopupSettings
        {
            Mode = mode,
            Provider = provider,
            VmsKind = r.VmsKind ?? string.Empty,
            VmsServerUrl = r.VmsServerUrl ?? string.Empty,
            VmsAccount = r.VmsAccount ?? string.Empty,
            DoubleClickAutoClose = autoDiscard,
            DoubleClickAutoCloseSeconds = timeoutSeconds,
            EventWindowOnDetection = r.EventWindowOnDetection,
            EventWindowOnMalfunction = r.EventWindowOnMalfunction,
            CamerasPerWindow = r.CamerasPerWindow,
            GridLayout = layout,
            MaxOpenWindows = r.MaxOpenWindows,
            TargetMonitorId = r.TargetMonitorId ?? string.Empty,
            FirstWindowX = r.FirstWindowX,
            FirstWindowY = r.FirstWindowY,
            WindowWidth = r.WindowWidth,
            WindowHeight = r.WindowHeight,
            CascadeStepPx = r.CascadeStepPx,
            AlwaysOnTop = r.AlwaysOnTop,
            CloseOnActionReport = r.CloseOnActionReport,
            CloseByTimer = r.CloseByTimer,
            CloseTimerSeconds = r.CloseTimerSeconds,
            ReturnHomePresetOnClose = r.ReturnHomePresetOnClose,
            BrokerMonitor = r.BrokerMonitor,
            BrokerCell = r.BrokerCell,
            BrokerOnOccupied = TryParseOnOccupied(r.BrokerOnOccupied, out var occupied) ? occupied : CameraPopupOnOccupied.Replace,
            BrokerResponseTimeoutSeconds = r.BrokerResponseTimeoutSeconds,
        }.Normalize();
    }

    /// <summary>설정 값 → 저장 모양. 더블클릭 자동 닫기는 옛 키로 따로 쓴다(여기 없음).</summary>
    public static CameraPopupSettingsRecord ToRecord(CameraPopupSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        var s = settings.Normalize();

        return new CameraPopupSettingsRecord
        {
            Mode = s.Mode.ToString(),
            Provider = s.Provider.ToString(),
            VmsKind = s.VmsKind,
            VmsServerUrl = s.VmsServerUrl,
            VmsAccount = s.VmsAccount,
            EventWindowOnDetection = s.EventWindowOnDetection,
            EventWindowOnMalfunction = s.EventWindowOnMalfunction,
            CamerasPerWindow = s.CamerasPerWindow,
            GridLayout = s.GridLayout.Key,
            MaxOpenWindows = s.MaxOpenWindows,
            TargetMonitorId = s.TargetMonitorId,
            FirstWindowX = s.FirstWindowX,
            FirstWindowY = s.FirstWindowY,
            WindowWidth = s.WindowWidth,
            WindowHeight = s.WindowHeight,
            CascadeStepPx = s.CascadeStepPx,
            AlwaysOnTop = s.AlwaysOnTop,
            CloseOnActionReport = s.CloseOnActionReport,
            CloseByTimer = s.CloseByTimer,
            CloseTimerSeconds = s.CloseTimerSeconds,
            ReturnHomePresetOnClose = s.ReturnHomePresetOnClose,
            BrokerMonitor = s.BrokerMonitor,
            BrokerCell = s.BrokerCell,
            BrokerOnOccupied = OnOccupiedWireValue(s.BrokerOnOccupied),
            BrokerResponseTimeoutSeconds = s.BrokerResponseTimeoutSeconds,
        };
    }

    /// <summary>옛 <c>IsCameraPopupUsed</c> 에 함께 쓸 값 — 사용 안 함만 <c>false</c>.</summary>
    public static bool LegacyIsCameraPopupUsed(CameraPopupSettings settings) => settings.Mode != CameraPopupMode.None;

    /// <summary>옛 <c>CameraPopupRtspSource</c> 에 함께 쓸 값 — ONVIF 면 <c>Onvif</c>, 아니면 <c>Url</c>.</summary>
    public static EnumCameraPopupRtspSource LegacyRtspSource(CameraPopupSettings settings)
        => settings.Provider == VideoProviderKind.Onvif ? EnumCameraPopupRtspSource.Onvif : EnumCameraPopupRtspSource.Url;

    /// <summary>명세 표기(<c>REPLACE</c> · <c>REJECT</c>).</summary>
    public static string OnOccupiedWireValue(CameraPopupOnOccupied value)
        => value == CameraPopupOnOccupied.Reject ? "REJECT" : "REPLACE";

    public static bool TryParseMode(string? text, out CameraPopupMode mode)
        => TryParseName(text, out mode);

    /// <summary>제공자 — <c>Url</c>(옛 표기)도 <see cref="VideoProviderKind.RtspUrl"/> 로 받는다.</summary>
    public static bool TryParseProvider(string? text, out VideoProviderKind provider)
    {
        if (string.Equals(text?.Trim(), "Url", StringComparison.OrdinalIgnoreCase))
        {
            provider = VideoProviderKind.RtspUrl;
            return true;
        }
        return TryParseName(text, out provider);
    }

    public static bool TryParseOnOccupied(string? text, out CameraPopupOnOccupied value)
        => TryParseName(text, out value);

    /// <summary>이름으로만 받는다(숫자 문자열 "7" 같은 정의 밖 값은 실패) · 대소문자 무관.</summary>
    private static bool TryParseName<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        if (char.IsDigit(trimmed[0]) || trimmed[0] == '-') return false;
        return Enum.TryParse(trimmed, ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}
