using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>GIS → 호스트: 이벤트 창 닫기(조치보고 · 창 수 한도 정리 등).</summary>
public sealed class CloseEventWindow : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
    public EventWindowCloseReason Reason { get; init; } = EventWindowCloseReason.ActionReported;

    /// <summary>자동 이동했던 PTZ 를 복귀 프리셋으로 돌린다(FR-15).</summary>
    public bool ReturnHome { get; init; }
}
