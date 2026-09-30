using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>호스트 → GIS: 사람이 타일 하나를 닫았다(창은 그대로). 재시작 복원 때 이 카메라는 빼고 보낸다.</summary>
public sealed class TileClosed : IIpcMessage
{
    public string EventKey { get; init; } = string.Empty;
    public string CameraId { get; init; } = string.Empty;
}
