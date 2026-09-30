using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// 호스트 → GIS 심박 응답. 호스트는 이 응답을 UI 스레드를 한 바퀴 돌아 보낸다 —
/// 파이프 스레드뿐 아니라 UI 스레드 멈춤도 무응답으로 드러나게.
/// </summary>
public sealed class HeartbeatAck : IIpcMessage
{
    public long Sequence { get; init; }
    public long PrivateBytes { get; init; }
    public int OpenStreams { get; init; }
    public int OpenWindows { get; init; }
}
