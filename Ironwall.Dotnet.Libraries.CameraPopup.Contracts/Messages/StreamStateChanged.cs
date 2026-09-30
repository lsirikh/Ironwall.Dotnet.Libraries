using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>호스트 → GIS: 오버레이 스트림 또는 이벤트 창 타일의 상태(FR-26 — 실패는 그 상자 · 타일에만).</summary>
public sealed class StreamStateChanged : IIpcMessage
{
    public string StreamId { get; init; } = string.Empty;

    /// <summary>이벤트 창 타일이면 그 창의 이벤트 키, 오버레이면 null.</summary>
    public string? EventKey { get; init; }
    public StreamState State { get; init; }
    public string? Detail { get; init; }
}
