using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// 호스트 → GIS: 이벤트 창이 닫혔다. 어떤 사유든 GIS 는 재시작 복원 목록에서 뺀다
/// (카드 · 지도 깜빡임은 조치보고로만 풀린다 — 창 닫힘과 무관, FR-15).
/// </summary>
public sealed class WindowClosed : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
    public EventWindowCloseReason Reason { get; init; }
}
