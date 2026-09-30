namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// GIS → 호스트 요청 · 응답 종류(<see cref="Messages.CameraRequest"/>). 결과가 화면에 필요한 조작만 여기 온다 —
/// 누름 · 뗌 같은 순간 조작(연속 이동 · 정지 · 포커스)은 보내고 잊는 <see cref="Messages.PtzCommand"/> ·
/// <see cref="Messages.PtzFocusCommand"/> 로 간다(FR-28). 값 이름이 JSON 문자열이다 — 바꾸면 계약 파손.
/// </summary>
public enum CameraRequestKind
{
    /// <summary>PTZ 준비(ONVIF 연결 · GetNode) → PTZ 가능 · 영상 옵션 가능 여부.</summary>
    PreparePtz = 0,
    /// <summary>카메라에 저장된 프리셋 목록.</summary>
    GetPresets = 1,
    GotoPreset = 2,
    /// <summary>현재 위치를 새 프리셋으로(토큰은 카메라가 정한다).</summary>
    SetPreset = 3,
    RemovePreset = 4,
    /// <summary>현재 위치를 Home 으로.</summary>
    SetHome = 5,
    GotoHome = 6,
    /// <summary>주야간(IrCutFilter) · 오토포커스 조회.</summary>
    GetImaging = 7,
    SetIrCutFilter = 8,
    SetAutoFocus = 9,
}

/// <summary>호스트 응답의 실패 사유 코드(기계 판독용 — 화면 문구는 GIS 가 고른다).</summary>
public static class CameraErrorCodes
{
    /// <summary>제한 시간 초과(FR-26).</summary>
    public const string Timeout = "timeout";
    /// <summary>제공자가 이 조작을 지원하지 않는다(외부 VMS · 영상만 제공자 · 비PTZ 카메라).</summary>
    public const string NotSupported = "not-supported";
    /// <summary>카메라가 거부 · 응답 실패.</summary>
    public const string Failed = "failed";
    /// <summary>요청 모양 오류(카메라 id · 토큰 없음).</summary>
    public const string BadRequest = "bad-request";
    /// <summary>호스트가 없음 · 재시작 중 · 일시 중지(GIS 쪽에서 만든다).</summary>
    public const string HostUnavailable = "host-unavailable";
    /// <summary>응답 전에 호스트가 재시작됐다(GIS 쪽에서 만든다).</summary>
    public const string HostRestarted = "host-restarted";
    /// <summary>ONVIF 로 영상 주소를 얻지 못했다(스트림 상태 Detail).</summary>
    public const string ResolveFailed = "resolve-failed";
}
