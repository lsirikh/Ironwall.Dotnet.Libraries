namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

// 계약 열거형 묶음 — 값 이름이 곧 JSON 문자열이다(JsonStringEnumConverter). 이름을 바꾸면 계약 파손.

/// <summary>영상 제공자 종류(FR-17/18). 호스트가 어떤 생산자로 프레임을 만들지 고른다.</summary>
public enum VideoProviderKind
{
    /// <summary>LibVLC 없이 호스트가 움직이는 시험 무늬를 그린다(생존 시험 · 진단).</summary>
    TestPattern = 0,
    /// <summary>RTSP 주소(LibVLC).</summary>
    Rtsp = 1,
    /// <summary>로컬 파일(LibVLC) — 시험 · 진단.</summary>
    File = 2,
    /// <summary>ONVIF(영상 주소는 호스트가 얻는다) — T-02 에서 구현.</summary>
    Onvif = 3,
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
}
