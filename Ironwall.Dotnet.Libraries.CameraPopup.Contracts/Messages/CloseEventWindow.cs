using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// GIS → 호스트: 이벤트 창 닫기(조치보고 · 창 수 한도 정리 등). 호스트는 받은 명령을 그대로 따른다 —
/// 📌 고정 창을 자동 사유(<see cref="Protocol.EventWindowCloseReason.ActionReported"/> · <see cref="Protocol.EventWindowCloseReason.Evicted"/>)로
/// 닫지 않는 판단은 GIS 몫이다(호스트가 보낸 <see cref="PinChanged"/> 로 안다, FR-11/15). 호스트 스스로는 타이머만 📌 를 존중한다.
/// </summary>
public sealed class CloseEventWindow : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
    public EventWindowCloseReason Reason { get; init; } = EventWindowCloseReason.ActionReported;

    /// <summary>자동 이동했던 PTZ 를 복귀 프리셋으로 돌린다(FR-15).</summary>
    public bool ReturnHome { get; init; }
}
