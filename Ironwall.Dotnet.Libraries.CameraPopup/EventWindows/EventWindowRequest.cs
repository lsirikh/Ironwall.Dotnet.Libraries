using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;

/****************************************************************************
   Purpose      : 탐지 트리거 → 창 관리자 요청 한 건 (PRD camera-popup-modes FR-09)
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 이벤트 창 하나의 <b>내용</b>(무엇을 보일지). 위치 · 크기 · 격자 · 닫기 조건 · 항상 위는 창 관리자가 설정에서 채운다.
/// </summary>
public sealed class EventWindowRequest
{
    public EventWindowKind Kind { get; init; }

    /// <summary>서버 이벤트 id(문자열).</summary>
    public string EventId { get; init; } = string.Empty;

    public EventWindowHeader Header { get; init; } = new();

    /// <summary>타일 순서대로(매핑 priority 순, 창당 카메라 수 이하).</summary>
    public IReadOnlyList<EventWindowCamera> Cameras { get; init; } = Array.Empty<EventWindowCamera>();

    /// <summary>매핑 카메라 중 창에 못 담은 수(꼬리 "+N").</summary>
    public int ExtraCameraCount { get; init; }

    /// <summary>창 제목(작업 표시줄). 비면 호스트가 머리 정보로 만든다.</summary>
    public string? Title { get; init; }

    public string EventKey => EventKeys.Build(Kind, EventId);
}

/// <summary>창 관리자 <c>Open</c> 결과.</summary>
public enum EventWindowOpenResult
{
    /// <summary>새 창 명령을 보냈다.</summary>
    Opened = 0,

    /// <summary>같은 이벤트의 창이 이미 있어 앞으로 가져왔다(FR-09).</summary>
    BroughtToFront = 1,

    /// <summary>자체 모드가 아니거나 탐지/장애 창을 끄는 설정.</summary>
    Disabled = 2,

    /// <summary>띄울 카메라가 없다.</summary>
    NoCameras = 3,

    /// <summary>열린 창이 한도만큼 있고 전부 📌 고정이라 뺄 창이 없다 — 새 창을 열지 않는다.</summary>
    AllPinned = 4,

    /// <summary>요청이 잘못됐거나(이벤트 id 없음) 관리자 안에서 예외(로그만, FR-27).</summary>
    Failed = 5,
}
