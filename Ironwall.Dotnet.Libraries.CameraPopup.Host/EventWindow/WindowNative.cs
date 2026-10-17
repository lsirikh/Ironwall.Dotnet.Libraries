using System.Runtime.InteropServices;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 이벤트 창 배치용 Win32(물리 픽셀). 호스트는 모니터별 DPI 인지 v2(app.manifest)라
/// SetWindowPos · GetWindowRect · GetMonitorInfo 가 전부 물리 픽셀로 오간다.
/// </summary>
internal static class WindowNative
{
    private const uint MONITOR_DEFAULTTONULL = 0;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    /// <summary>
    /// 사각형이 걸친 모니터의 작업영역. 어느 모니터에도 안 걸리면(모니터를 뽑음 · 화면 밖) 주 모니터 작업영역과 false.
    /// </summary>
    public static bool TryGetWorkArea(PixelRect rect, out PixelRect workArea)
    {
        var r = new RECT { Left = rect.X, Top = rect.Y, Right = rect.X + Math.Max(1, rect.Width), Bottom = rect.Y + Math.Max(1, rect.Height) };
        var monitor = MonitorFromRect(ref r, MONITOR_DEFAULTTONULL);
        bool found = monitor != IntPtr.Zero;
        if (!found) monitor = MonitorFromRect(ref r, MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            workArea = new PixelRect();
            return false;
        }
        workArea = new PixelRect { X = info.rcWork.Left, Y = info.rcWork.Top, Width = info.rcWork.Right - info.rcWork.Left, Height = info.rcWork.Bottom - info.rcWork.Top };
        return found;
    }

    public static bool Place(IntPtr hwnd, PixelRect rect)
        => hwnd != IntPtr.Zero && SetWindowPos(hwnd, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height, SWP_NOZORDER | SWP_NOACTIVATE);

    public static PixelRect? GetBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return null;
        return new PixelRect { X = r.Left, Y = r.Top, Width = r.Right - r.Left, Height = r.Bottom - r.Top };
    }
}
