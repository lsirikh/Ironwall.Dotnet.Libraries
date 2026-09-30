using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// GIS → 호스트: 이벤트 창 열기(FR-09~16). 이벤트 1건 = 창 1개 — 같은 <see cref="EventKey"/> 가
/// 다시 오면 호스트는 새 창 없이 기존 창을 앞으로 가져온다. 위치 · 크기는 GIS 가 모니터 작업영역에
/// 맞춰 계산한 화면 좌표(DIU)이다(계단 배치는 GIS 의 순수 함수, T-04).
/// </summary>
public sealed class OpenEventWindow : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
    public string? Title { get; init; }
    public List<EventWindowCamera> Cameras { get; init; } = new();

    /// <summary>"열x행"(예 "3x2"). 비었거나 카메라 수와 안 맞으면 자동(<see cref="Protocol.TileGrid"/>).</summary>
    public string? Layout { get; init; }

    /// <summary>모니터 장치 이름(예 <c>\\.\DISPLAY2</c>) — 기록용, 위치는 이미 화면 좌표.</summary>
    public string? Monitor { get; init; }

    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; } = 960;
    public double Height { get; init; } = 600;
    public bool Topmost { get; init; }
    public WindowClosePolicy ClosePolicy { get; init; } = new();
}
