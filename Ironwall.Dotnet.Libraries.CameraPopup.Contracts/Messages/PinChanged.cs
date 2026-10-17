using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>호스트 → GIS: 📌 고정 전환. 고정 창은 자동 닫기 · 오래된 창 정리에서 빠진다(FR-11/15).</summary>
public sealed class PinChanged : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
    public bool Pinned { get; init; }
}
