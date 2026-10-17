using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// GIS → 호스트: 지도 오버레이용 프레임 생산 시작(FR-24).
/// 공유 메모리는 GIS 가 만들고(<see cref="SharedMemoryName"/>) 머리말을 채운다 — 호스트는 열어 쓰기만 한다.
/// 그래서 호스트가 죽어도 GIS 쪽 매핑은 살아 있고, 재시작 뒤 같은 이름으로 다시 연다.
/// </summary>
public sealed class OpenOverlayStream : IIpcMessage
{
    public string StreamId { get; init; } = string.Empty;
    public CameraRef Camera { get; init; } = new();
    public VideoProviderInfo Provider { get; init; } = new();
    public int Width { get; init; }
    public int Height { get; init; }
    public string SharedMemoryName { get; init; } = string.Empty;
}
