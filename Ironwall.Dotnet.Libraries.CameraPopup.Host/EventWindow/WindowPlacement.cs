using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>창 배치 보정(순수, FR-12 안전망). 계단 배치 자체는 GIS(T-04) 몫 — 호스트는 화면 밖만 막는다.</summary>
internal static class WindowPlacement
{
    public const int MinWidth = 320;
    public const int MinHeight = 200;

    /// <summary>
    /// 창을 작업영역 안으로(물리 픽셀). 작업영역이 비면 크기 하한만 적용한다.
    /// 작업영역보다 크면 줄이고, 밖으로 나간 만큼 안쪽으로 민다.
    /// </summary>
    public static PixelRect ClampToWorkArea(PixelRect window, PixelRect workArea)
    {
        int width = Math.Max(MinWidth, window.Width);
        int height = Math.Max(MinHeight, window.Height);
        if (workArea.IsEmpty) return new PixelRect { X = window.X, Y = window.Y, Width = width, Height = height };

        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);
        int x = Math.Clamp(window.X, workArea.X, workArea.X + workArea.Width - width);
        int y = Math.Clamp(window.Y, workArea.Y, workArea.Y + workArea.Height - height);
        return new PixelRect { X = x, Y = y, Width = width, Height = height };
    }

    /// <summary>물리 픽셀 → DIU(초기 크기 힌트용). 배율이 0 이하이면 1.</summary>
    public static double ToDiu(int pixels, double dpiScale) => pixels / (dpiScale > 0 ? dpiScale : 1.0);
}
