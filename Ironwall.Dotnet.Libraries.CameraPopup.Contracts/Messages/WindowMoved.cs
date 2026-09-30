using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// 호스트 → GIS: 사람이 창 머리를 끌어 옮겼다 — 그 창은 계단 줄에서 빠진다(FR-12).
/// 좌표는 옮긴 뒤 창의 물리 픽셀 위치.
/// </summary>
public sealed class WindowMoved : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
    public int X { get; init; }
    public int Y { get; init; }
}
