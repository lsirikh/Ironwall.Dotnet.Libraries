using System.Runtime.InteropServices;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 모니터 열거 — Win32 EnumDisplayMonitors / GetMonitorInfo (DPI 인지) (PRD §3 모니터)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// Win32 로 모니터를 연다. 열거하는 동안만 스레드 DPI 인지를 <b>모니터별 v2</b> 로 올려
/// 프로세스 DPI 설정과 상관없이 <b>물리 픽셀</b> 좌표를 받는다(가상화된 좌표 방지). 끝나면 원래대로 돌린다.
/// </summary>
/// <remarks>
/// 실패(구형 OS · API 없음 · 원격 세션 전환 중)는 예외 대신 빈 목록 — 설정 화면은 "모니터를 읽지 못했습니다"로 안내하고
/// 창 관리자는 주 모니터 폴백을 쓴다(PRD §0 — 팝업 쪽 실패로 GIS 를 멈추지 않는다).
/// </remarks>
public sealed class Win32DisplayMonitorProvider : IDisplayMonitorProvider
{
    public IReadOnlyList<DisplayMonitorInfo> GetMonitors()
    {
        var result = new List<DisplayMonitorInfo>();
        var previous = IntPtr.Zero;
        var raised = false;

        try
        {
            try
            {
                previous = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
                raised = previous != IntPtr.Zero;
            }
            catch (EntryPointNotFoundException) { /* Windows 10 1703 이전 — 프로세스 인지 그대로 */ }

            bool Callback(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data)
            {
                var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
                if (!GetMonitorInfo(monitor, ref info)) return true;

                var dpi = 96;
                try
                {
                    if (GetDpiForMonitor(monitor, MonitorDpiTypeEffective, out var dpiX, out _) == 0 && dpiX > 0)
                        dpi = (int)dpiX;
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }

                result.Add(new DisplayMonitorInfo(
                    info.DeviceName ?? string.Empty,
                    info.Monitor.ToPixelRect(),
                    info.Work.ToPixelRect(),
                    (info.Flags & MonitorInfoPrimary) != 0,
                    dpi));
                return true;
            }

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        }
        catch (Exception)
        {
            return Array.Empty<DisplayMonitorInfo>();
        }
        finally
        {
            if (raised)
            {
                try { SetThreadDpiAwarenessContext(previous); } catch { /* 되돌리기 실패는 무시 — 이 스레드 한정 */ }
            }
        }

        // 주 모니터 먼저, 그다음 왼쪽 → 오른쪽 · 위 → 아래(목록이 화면 배치와 같은 순서로 읽히게).
        return result
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.Bounds.X)
            .ThenBy(m => m.Bounds.Y)
            .ToList();
    }

    #region - Win32 -
    private const uint MonitorInfoPrimary = 1;
    private const int MonitorDpiTypeEffective = 0;
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
        public PixelRect ToPixelRect() => new(Left, Top, Right - Left, Bottom - Top);
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

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
    #endregion
}
