using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// GIS → 호스트: PTZ 조작. 같은 카메라의 명령은 대기열에서 최신 것 하나로 합쳐진다(FR-28) —
/// 밀린 이동 뒤에 정지가 오면 정지만 나간다.
/// </summary>
public sealed class PtzCommand : IIpcMessage
{
    public string CameraId { get; init; } = string.Empty;
    public PtzOperation Operation { get; init; }

    /// <summary>-1..1 속도(ContinuousMove).</summary>
    public double Pan { get; init; }
    public double Tilt { get; init; }
    public double Zoom { get; init; }

    public string? PresetToken { get; init; }
    public VideoProviderInfo? Provider { get; init; }
}
