using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>호스트 → GIS, 핸드셰이크 수락.</summary>
public sealed class HelloAck : IIpcMessage
{
    public int ProtocolVersion { get; init; } = Protocol.ProtocolVersion.Current;
    public int HostProcessId { get; init; }
    public string? HostVersion { get; init; }
}
