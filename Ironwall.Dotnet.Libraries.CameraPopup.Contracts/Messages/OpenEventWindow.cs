using System.Text.Json.Serialization;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// GIS → 호스트: 이벤트 창 열기(FR-09~16, 계약 판 2). 이벤트 1건 = 창 1개 — 같은 <see cref="EventKey"/> 가
/// 다시 오면 호스트는 새 창 없이 기존 창을 앞으로 가져온다(감시자 재시작 복원도 이 멱등성에 기댄다).
/// <para>좌표는 전부 <b>물리 픽셀</b>이다. GIS 는 모니터 작업영역 · 계단 배치(T-04)를 물리 픽셀로 계산해 넣고,
/// 호스트는 <see cref="DpiScale"/>(대상 모니터 배율) 로 WPF DIU 로 바꾼다(호스트는 모니터별 DPI 인지 v2).</para>
/// </summary>
public sealed class OpenEventWindow : IIpcMessage
{
    /// <summary>탐지 · 장애.</summary>
    public EventWindowKind Kind { get; init; }

    /// <summary>서버 이벤트 id(문자열 그대로).</summary>
    public string EventId { get; init; } = string.Empty;

    /// <summary>창 식별 키 = <see cref="EventKeys.Build"/>(<see cref="Kind"/>, <see cref="EventId"/>). 직렬화하지 않는다(파생값).</summary>
    [JsonIgnore]
    public string EventKey => EventKeys.Build(Kind, EventId);

    /// <summary>머리 한 줄(배지 · 구역 · 장비 · 종류 · 시각).</summary>
    public EventWindowHeader Header { get; init; } = new();

    /// <summary>표시할 카메라(매핑 priority 순, 최대 <see cref="TileGrid.MaxCameras"/>). 순서 = 타일 순서.</summary>
    public List<EventWindowCamera> Cameras { get; init; } = new();

    /// <summary>격자 열 · 행. 0 이거나 카메라를 못 담으면 호스트가 자동 격자(<see cref="TileGrid.Resolve(int,int,int)"/>).</summary>
    public int GridColumns { get; init; }
    public int GridRows { get; init; }

    /// <summary>매핑 카메라 중 창에 못 담은 수(꼬리 "+N"). 0 이면 표시 안 함.</summary>
    public int ExtraCameraCount { get; init; }

    /// <summary>대상 모니터 전체 영역(물리 픽셀). 기록 · 화면 밖 보정용.</summary>
    public PixelRect MonitorBounds { get; init; } = new();

    /// <summary>대상 모니터 작업영역(물리 픽셀) — 창이 이 밖으로 나가면 호스트가 안쪽으로 당긴다.</summary>
    public PixelRect MonitorWorkArea { get; init; } = new();

    /// <summary>대상 모니터 DPI 배율(96dpi = 1.0). 0 이하면 1.0.</summary>
    public double DpiScale { get; init; } = 1.0;

    /// <summary>모니터 장치 이름(예 <c>\\.\DISPLAY2</c>) — 기록용.</summary>
    public string? MonitorDeviceName { get; init; }

    /// <summary>창 위치 · 크기(물리 픽셀).</summary>
    public PixelRect Window { get; init; } = new() { Width = 960, Height = 600 };

    public bool AlwaysOnTop { get; init; }

    /// <summary>타이머 닫기(초). 0 = 끔. 창을 만지면 다시 센다(FR-15).</summary>
    public int TimerCloseSeconds { get; init; }

    /// <summary>조치보고가 오면 닫힘 — 닫기 명령은 GIS 가 보낸다. 호스트는 꼬리 문구에만 쓴다.</summary>
    public bool CloseOnActionReport { get; init; } = true;

    /// <summary>닫을 때 자동 이동했던 PTZ 를 복귀 프리셋으로(FR-15). <see cref="CloseEventWindow.ReturnHome"/> 가 명령별로 덮어쓴다.</summary>
    public bool ReturnHomeOnClose { get; init; }

    /// <summary>📌 고정 상태로 연다(재시작 복원 때 GIS 가 <see cref="PinChanged"/> 를 반영해 다시 보낸다).</summary>
    public bool Pinned { get; init; }

    /// <summary>"Light" · "Dark". 비면 호스트의 현재 테마(<see cref="SetTheme"/>) 유지.</summary>
    public string? Theme { get; init; }

    /// <summary>창 제목(작업 표시줄 · Alt+Tab). 비면 머리 정보로 만든다.</summary>
    public string? Title { get; init; }
}
