namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>카메라 식별 · 표시 이름.</summary>
public sealed class CameraRef
{
    public string CameraId { get; init; } = string.Empty;
    public string? Name { get; init; }

    public override string ToString() => $"{CameraId}({Name})";
}
