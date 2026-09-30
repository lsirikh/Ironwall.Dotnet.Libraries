using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>GIS → 호스트: 오버레이 스트림 닫기.</summary>
public sealed class CloseStream : IIpcMessage
{
    public string StreamId { get; init; } = string.Empty;
}
