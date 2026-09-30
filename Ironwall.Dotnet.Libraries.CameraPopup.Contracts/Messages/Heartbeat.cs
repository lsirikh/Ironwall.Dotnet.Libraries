using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>GIS → 호스트 심박(FR-25, 기본 1초).</summary>
public sealed class Heartbeat : IIpcMessage
{
    public long Sequence { get; init; }
}
