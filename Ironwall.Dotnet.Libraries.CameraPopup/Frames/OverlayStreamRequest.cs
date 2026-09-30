using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Frames;

/// <summary>지도 오버레이 영상 요청. 크기는 오버레이 상자 크기(px) — 그 해상도로 받아 전송량을 줄인다(FR-24).</summary>
public sealed class OverlayStreamRequest
{
    /// <summary>비우면 새 id 를 만든다. 같은 id 로 다시 열면 이전 스트림을 교체한다.</summary>
    public string? StreamId { get; init; }
    public CameraRef Camera { get; init; } = new();
    public VideoProviderInfo Provider { get; init; } = new();
    public int Width { get; init; } = 640;
    public int Height { get; init; } = 360;
}
