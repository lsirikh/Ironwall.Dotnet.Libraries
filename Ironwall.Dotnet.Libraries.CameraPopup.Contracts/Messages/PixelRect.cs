namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>화면 사각형(물리 픽셀, 가상 화면 좌표 — 주 모니터 왼쪽 위가 0,0).</summary>
public sealed class PixelRect
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public override string ToString() => $"{X},{Y} {Width}x{Height}";
}
