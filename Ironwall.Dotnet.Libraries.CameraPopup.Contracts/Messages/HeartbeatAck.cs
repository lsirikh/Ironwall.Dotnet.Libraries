using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// 호스트 → GIS 심박 응답. 호스트는 이 응답을 <b>파이프 읽기 줄(배경 스레드)</b>에서 바로 보낸다 —
/// UI 스레드가 창을 만드느라 몇 초 바빠도 심박은 끊기지 않는다(T-09: 3.8초 바쁨을 멈춤으로 오판해 멀쩡한 호스트를 죽였다).
/// UI 멈춤은 <see cref="UiStallMs"/> 로 따로 알린다 — 감시자가 긴 기준(기본 10초)으로 판단한다.
/// </summary>
public sealed class HeartbeatAck : IIpcMessage
{
    public long Sequence { get; init; }
    public long PrivateBytes { get; init; }
    public int OpenStreams { get; init; }
    public int OpenWindows { get; init; }

    /// <summary>호스트 UI 스레드가 메시지를 돌리지 못한 시간(ms). 돌고 있으면 0(옛 호스트는 늘 0).</summary>
    public long UiStallMs { get; init; }
}
