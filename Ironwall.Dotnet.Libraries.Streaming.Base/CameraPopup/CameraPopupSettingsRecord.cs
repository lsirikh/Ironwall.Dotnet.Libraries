namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 카메라 팝업 설정의 저장 모양 — appsettings "AppSettings:CameraPopup" 한 덩어리
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>appsettings.json</c> 의 <c>"CameraPopup": { … }</c> 를 <c>Configuration.Bind</c> 로 받는 그릇.
/// </summary>
/// <remarks>
/// <para><b>열거값은 문자열로 받는다</b> — 열거형 속성에 Bind 를 직접 걸면 오타("Brokr")에서
/// <c>InvalidOperationException</c> 으로 앱 기동 자체가 실패한다(<c>CameraPopupRtspSource</c> 와 같은 방어).
/// 해석은 <see cref="CameraPopupSettingsCodec"/> 이 관대하게 한다(모르는 값 = 기본값).</para>
/// <para><b>키가 통째로 없어도 된다</b> — 그러면 옛 키(<c>IsCameraPopupUsed</c> · <c>CameraPopupRtspSource</c>)에서
/// 이관하고 나머지는 코드 기본값이다. 일부 칸만 있어도 빠진 칸은 아래 기본값이 채운다.</para>
/// <para>더블클릭 자동 닫기는 여기에 없다 — 현행 키 <c>IsAutoDiscard</c> · <c>TimeoutSeconds</c> 를 그대로 쓴다.</para>
/// </remarks>
public sealed class CameraPopupSettingsRecord
{
    /// <summary><c>Self</c> · <c>Broker</c> · <c>None</c>. 비면 옛 <c>IsCameraPopupUsed</c> 에서 이관.</summary>
    public string? Mode { get; set; }

    /// <summary><c>Onvif</c> · <c>RtspUrl</c>(· <c>ExternalVms</c> 자리). 비면 옛 <c>CameraPopupRtspSource</c> 에서 이관.</summary>
    public string? Provider { get; set; }

    public string? VmsKind { get; set; }
    public string? VmsServerUrl { get; set; }
    public string? VmsAccount { get; set; }

    public bool EventWindowOnDetection { get; set; } = true;
    public bool EventWindowOnMalfunction { get; set; }
    public int CamerasPerWindow { get; set; } = 6;

    /// <summary>가로x세로 — <c>3x2</c>.</summary>
    public string? GridLayout { get; set; } = "3x2";
    public int MaxOpenWindows { get; set; } = 10;

    /// <summary><c>\\.\DISPLAY2@2560x1440</c>. 비면 주 모니터.</summary>
    public string? TargetMonitorId { get; set; }
    public int FirstWindowX { get; set; } = 24;
    public int FirstWindowY { get; set; } = 24;
    public int WindowWidth { get; set; } = 960;
    public int WindowHeight { get; set; } = 600;
    public int CascadeStepPx { get; set; } = 24;
    public bool AlwaysOnTop { get; set; } = true;

    public bool CloseOnActionReport { get; set; } = true;
    public bool CloseByTimer { get; set; } = true;
    public int CloseTimerSeconds { get; set; } = 60;
    public bool ReturnHomePresetOnClose { get; set; } = true;

    public int BrokerMonitor { get; set; } = 1;
    /// <summary>0 = 자동, 1~9.</summary>
    public int BrokerCell { get; set; }
    /// <summary><c>REPLACE</c> · <c>REJECT</c>(명세 표기).</summary>
    public string? BrokerOnOccupied { get; set; } = "REPLACE";
    public int BrokerResponseTimeoutSeconds { get; set; } = 5;

    /// <summary>얕은 사본(모두 값 · 문자열이라 깊은 사본과 같다).</summary>
    public CameraPopupSettingsRecord Clone() => (CameraPopupSettingsRecord)MemberwiseClone();
}
