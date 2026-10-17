using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 팝업 사용자 배치 콜백용 경계 — 대상이 있는 창(클라이언트 영역)과 그 모니터의 작업 영역을 <b>콜백 좌표계</b>(대상 왼쪽 위 원점,
/// 콜백이 주는 크기와 같은 단위)로 옮겨 준다. 판정은 각 순수 함수가 하고 여기서는 단위만 맞춘다.
/// </summary>
/// <remarks>
/// 콜백이 주는 크기의 단위(DIU 또는 장치 픽셀)는 대상의 실제 폭과 견주어 알아낸다. 화면에 붙기 전이면 둘 다 <see cref="Rect.Empty"/>.
/// 호출 스레드: UI.
/// </remarks>
internal static class PopupScreenBounds
{
    public static (Rect Window, Rect Work) For(FrameworkElement target, Size targetSize)
    {
        try
        {
            if (PresentationSource.FromVisual(target) is null || target.ActualWidth <= 0 || targetSize.Width <= 0) return (Rect.Empty, Rect.Empty);
            var origin = target.PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(target).DpiScaleX;
            var unitsPerPixel = targetSize.Width / target.ActualWidth / (dpi <= 0 ? 1 : dpi);
            Rect ToCallback(Rect pixels) => new((pixels.Left - origin.X) * unitsPerPixel, (pixels.Top - origin.Y) * unitsPerPixel,
                                               pixels.Width * unitsPerPixel, pixels.Height * unitsPerPixel);

            var work = WorkAreaPixels(origin) is { } w ? ToCallback(w) : Rect.Empty;
            var window = Window.GetWindow(target) is { } owner && WindowClientPixels(owner) is { } c ? ToCallback(c) : Rect.Empty;
            return (window, work);
        }
        catch (InvalidOperationException) { return (Rect.Empty, Rect.Empty); }   // 화면에 붙기 전
    }

    private static Rect? WindowClientPixels(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !NativeMethods.GetClientRect(handle, out var client)) return null;
        var topLeft = new NativeMethods.POINT { X = client.Left, Y = client.Top };
        if (!NativeMethods.ClientToScreen(handle, ref topLeft)) return null;
        var width = client.Right - client.Left;
        var height = client.Bottom - client.Top;
        return width <= 0 || height <= 0 ? null : new Rect(topLeft.X, topLeft.Y, width, height);
    }

    private static Rect? WorkAreaPixels(Point screenPoint)
    {
        var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = (int)screenPoint.X, Y = (int)screenPoint.Y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return null;
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return null;
        var w = info.rcWork;
        return new Rect(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top);
    }

    private static class NativeMethods
    {
        public const int MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    }
}
