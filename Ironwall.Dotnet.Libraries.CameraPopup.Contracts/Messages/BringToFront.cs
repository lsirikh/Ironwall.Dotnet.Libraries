using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>GIS → 호스트: 기존 이벤트 창을 앞으로(같은 이벤트 재수신 · 카드 클릭).</summary>
public sealed class BringToFront : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
}
