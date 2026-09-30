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

    /// <summary>
    /// <see cref="PtzOperation.DragMove"/>: 드래그 벡터 — 영상 상자 가로 · 세로에 대한 비율(오른쪽 +, 아래 +, 보통 -1..1).
    /// 드래그 방향으로 카메라가 돈다: 화면 중심에서 이 벡터만큼 떨어진 점이 중심으로 온다.
    /// </summary>
    public double ViewX { get; init; }
    public double ViewY { get; init; }

    /// <summary>영상 상자 가로/세로(세로 화각 계산용). 0 이면 16:9 로 본다.</summary>
    public double ViewAspect { get; init; }

    public string? PresetToken { get; init; }
    public VideoProviderInfo? Provider { get; init; }
}
