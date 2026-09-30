using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>GIS → 호스트, 연결 직후 첫 메시지. 토큰이 실행 인자의 토큰과 같아야 받아들여진다.</summary>
public sealed class Hello : IIpcMessage
{
    public int ProtocolVersion { get; init; } = Protocol.ProtocolVersion.Current;
    public string Token { get; init; } = string.Empty;
    public int ClientProcessId { get; init; }
}
