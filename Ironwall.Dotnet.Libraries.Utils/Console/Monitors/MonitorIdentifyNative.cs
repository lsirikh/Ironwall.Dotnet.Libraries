using System.Runtime.InteropServices;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;

/****************************************************************************
   Purpose      : 모니터 식별 창의 Win32 — 확장 스타일 · 좌표계 · 모니터 다시 찾기 · 맨 위 배치
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 판정은 <see cref="MonitorIdentifyMath"/> 가 한다 — 여기는 값을 읽고 쓰기만 한다. 실패는 예외 대신 기본값
/// (식별 카드가 못 떠도 GIS 는 멈추지 않는다).
/// </summary>
internal static class MonitorIdentifyNative
{
    private const int GwlExStyle = -20;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private static readonly IntPtr HwndTopmost = new(-1);

    /// <summary>창의 확장 스타일을 읽는다(0 = 못 읽음).</summary>
    public static long GetExStyle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return 0;
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64() : GetWindowLong32(hwnd, GwlExStyle);
    }

    /// <summary>클릭 통과 · 활성화 안 함 · 도구 창 스타일을 입힌다.</summary>
    public static void ApplyClickThrough(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var next = MonitorIdentifyMath.ClickThroughExStyle(GetExStyle(hwnd));
        if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, GwlExStyle, new IntPtr(next));
        else SetWindowLong32(hwnd, GwlExStyle, unchecked((int)next));
    }

    /// <summary>맨 위(topmost)로, 활성화하지 않고 자리 · 크기를 놓는다. 보이기 · 숨기기는 건드리지 않는다.</summary>
    public static bool PlaceTopmost(IntPtr hwnd, Int32Rect rect)
        => hwnd != IntPtr.Zero && !rect.IsEmpty
        && SetWindowPos(hwnd, HwndTopmost, rect.X, rect.Y, rect.Width, rect.Height, SwpNoActivate | SwpNoOwnerZOrder);

    /// <summary>이 스레드의 DPI 인지 수준(API 가 없으면 시스템 인지 — WPF 기본).</summary>
    public static MonitorIdentifyDpiMode ThreadDpiMode()
    {
        try
        {
            return GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) switch
            {
                0 => MonitorIdentifyDpiMode.Unaware,
                2 => MonitorIdentifyDpiMode.PerMonitor,
                _ => MonitorIdentifyDpiMode.System,
            };
        }
        catch (EntryPointNotFoundException) { return MonitorIdentifyDpiMode.System; }
    }

    /// <summary>시스템 DPI(API 가 없으면 96).</summary>
    public static int SystemDpi()
    {
        try { return (int)GetDpiForSystem(); }
        catch (EntryPointNotFoundException) { return MonitorIdentifyMath.BaseDpi; }
    }

    /// <summary>이 스레드 좌표계로 본 모니터들(장치 이름 · 전체 자리). 실패하면 빈 목록.</summary>
    public static IReadOnlyList<(string DeviceName, Int32Rect Bounds)> MonitorsInThreadSpace()
    {
        var result = new List<(string, Int32Rect)>();
        try
        {
            bool Callback(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data)
            {
                var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
                if (GetMonitorInfo(monitor, ref info))
                {
                    var r = info.Monitor;
                    result.Add((info.DeviceName ?? string.Empty, new Int32Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top)));
                }
                return true;
            }

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        }
        catch (EntryPointNotFoundException) { return Array.Empty<(string, Int32Rect)>(); }
        catch (DllNotFoundException) { return Array.Empty<(string, Int32Rect)>(); }
        return result;
    }

    #region - Win32 -
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string? DeviceName;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
    #endregion
}
