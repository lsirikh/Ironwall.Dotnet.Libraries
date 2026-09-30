namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

// 계약 열거형 묶음 — 값 이름이 곧 JSON 문자열이다(JsonStringEnumConverter). 이름을 바꾸면 계약 파손.

/// <summary>영상 제공자 종류(FR-17/18). 호스트가 어떤 생산자로 프레임을 만들지 고른다.</summary>
public enum VideoProviderKind
{
    /// <summary>LibVLC 없이 호스트가 움직이는 시험 무늬를 그린다(생존 시험 · 진단).</summary>
    TestPattern = 0,
    /// <summary>RTSP 주소(LibVLC) — 설정의 "RTSP 주소" 제공자(장비에 저장된 주소). 계정이 없으면 호스트가 싣는다.</summary>
    Rtsp = 1,
    /// <summary>로컬 파일(LibVLC) — 시험 · 진단.</summary>
    File = 2,
    /// <summary>ONVIF — 호스트가 GetProfiles/GetStreamUri 로 영상 주소를 얻는다(T-02). 실패하면 <c>FallbackUri</c>.</summary>
    Onvif = 3,
    /// <summary>외부 VMS API — 자리만(FR-18). 호스트는 "지원 안 함"(<c>not-supported</c>)으로 답한다.</summary>
    ExternalVms = 4,
}

/// <summary>스트림(오버레이 또는 타일) 상태.</summary>
public enum StreamState
{
    Opening = 0,
    Playing = 1,
    Stalled = 2,
    Failed = 3,
    Closed = 4,
}

/// <summary>PTZ 조작 종류.</summary>
public enum PtzOperation
{
    ContinuousMove = 0,
    Stop = 1,
    GotoPreset = 2,
    GotoHome = 3,
}

/// <summary>
/// 디버그 전용 명령 — 호스트가 <c>--debug-commands</c> 로 시작됐을 때만 받는다.
/// 생존 시험(K1~K3)이 "네이티브 충돌은 호스트 안에 머문다"를 증명하는 데 쓴다.
/// </summary>
public enum DebugCommandKind
{
    /// <summary>UI 스레드를 영원히 막아 심박 응답을 끊는다(K2).</summary>
    Hang = 0,
    /// <summary>Environment.FailFast.</summary>
    FailFast = 1,
    /// <summary>네이티브 코드 안 접근 위반(RtlMoveMemory 로 잘못된 주소 쓰기).</summary>
    NativeAccessViolation = 2,
    /// <summary>배경 스레드의 처리되지 않은 관리 예외.</summary>
    UnhandledException = 3,
    /// <summary>UI 스레드(디스패처)의 처리되지 않은 예외.</summary>
    DispatcherException = 4,
    /// <summary>UI 스레드를 <c>Argument</c> ms 동안만 막는다(창을 몰아 여는 바쁨 재현 — 심박 오탐 시험).</summary>
    UiBusy = 5,
    /// <summary>메모리 한도 종료를 흉내 — 알림을 보내고 파이프를 끊은 뒤 <c>Argument</c> ms 늦게 종료 코드 20 으로 내려간다.</summary>
    PlannedExitSlow = 6,
}

/// <summary>이벤트 창 종류 — 키 · 배지 · 테두리(탐지 = 빨간 테두리 + 배지 글자)를 가른다.</summary>
public enum EventWindowKind
{
    Detection = 0,
    Malfunction = 1,
}

/// <summary>이벤트 창이 닫힌 사유(GIS → 호스트 닫기 명령 · 호스트 → GIS 닫힘 알림 공통).</summary>
public enum EventWindowCloseReason
{
    /// <summary>그 이벤트의 조치보고(내 조치 · 다른 GIS 조치).</summary>
    ActionReported = 0,
    /// <summary>타이머 N초 경과(호스트가 판단).</summary>
    Timer = 1,
    /// <summary>사람이 ✕ · Alt+F4 로 닫음.</summary>
    User = 2,
    /// <summary>동시 창 한도 초과로 가장 오래된 창 정리(GIS 창 관리자, FR-11) · 모드 전환 등 GIS 사정.</summary>
    Evicted = 3,
}
