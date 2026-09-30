namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>이벤트 창 타일 하나 — 카메라 · 제공자 · 자동 이동 프리셋(FR-13).</summary>
public sealed class EventWindowCamera
{
    public CameraRef Camera { get; init; } = new();
    public VideoProviderInfo Provider { get; init; } = new();
    public string? PresetToken { get; init; }

    /// <summary>매핑 delay_time — 프리셋 이동 최소 시간(초).</summary>
    public int PresetDelaySeconds { get; init; }
}
