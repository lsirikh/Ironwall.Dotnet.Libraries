using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 편제 창(별도 OS 창)의 겉 — 제목 줄 색 · 도킹 폭 보정 (visual-review #38)
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 편제는 호스트 카드가 아니라 <c>IWindowManager.ShowDialogAsync</c> 로 뜨는 <b>OS 창</b>이다. 그래서 두 가지가 어긋났다.
/// </summary>
/// <remarks>
/// <para>① <b>제목 줄</b> — OS 기본 크림색 제목 줄 아래에 다크 본문이 붙어 두 창을 이어 붙인 것처럼 보였다(2026-09-27 실창 캡처 063).
/// DWM 에 제목 줄 색을 콘솔 머리(<c>SurfaceAltBrush</c>)와 같은 색으로, 글자를 <c>TextPrimaryBrush</c> 로 칠해 달라고 한다
/// (Windows 11 22000+ 의 <c>DWMWA_CAPTION_COLOR</c>/<c>DWMWA_TEXT_COLOR</c>, 그 전은 다크 모드 표지만). 지원하지 않는 OS 에서는
/// 조용히 아무것도 하지 않는다 — 실패해도 창은 그대로 쓸 수 있다.</para>
/// <para>② <b>폭</b> — 런처는 창 폭 1280 을 준다. 그런데 창 폭에는 OS 테두리가 들어 있어 콘솔이 1280 보다 좁아지고,
/// 커널은 1280 미만을 "서랍" 으로 판정해 상세 칸을 숨겼다(부대를 고르기 전까지 상세가 없는 창). 콘솔 실폭이 도킹 기준에
/// 조금 모자라면(테두리 몫, <see cref="MaxChromeDeficit"/> 이하) 그만큼 창을 넓힌다. 사용자가 일부러 좁힌 창은 건드리지 않는다.</para>
/// <para>색은 <b>그릴 때마다 다시 찾는다</b> — 한 번 찾아 쥐면 테마를 바꿔도 옛 색으로 굳는다(저장소 규칙).</para>
/// </remarks>
public static class UnitWindowChrome
{
    /// <summary>테두리 몫으로 볼 수 있는 최대 부족분(DIU) — 이보다 크게 모자라면 사용자가 좁힌 것으로 본다.</summary>
    public const double MaxChromeDeficit = 48;

    /// <summary>
    /// 콘솔 실폭이 도킹 기준에 모자라는 만큼(테두리 몫) — 넓혀야 할 폭. 넓히지 않아야 하면 0.
    /// </summary>
    /// <param name="consoleWidth">콘솔(ConsoleShell)의 실제 폭.</param>
    /// <param name="windowWidth">창 폭(테두리 포함).</param>
    /// <param name="workAreaWidth">작업 영역 폭 — 넘치게 넓히지 않는다.</param>
    public static double DockingDeficit(double consoleWidth, double windowWidth, double workAreaWidth)
    {
        if (double.IsNaN(consoleWidth) || double.IsNaN(windowWidth) || consoleWidth <= 0 || windowWidth <= 0) return 0;
        var deficit = Math.Ceiling(ConsoleLayoutMath.DockedMinWidth - consoleWidth);
        if (deficit <= 0 || deficit > MaxChromeDeficit) return 0;
        return windowWidth + deficit <= workAreaWidth ? deficit : 0;
    }

    /// <summary>창이 처음 뜰 때 한 번 — 폭을 보정하고 제목 줄을 칠한다.</summary>
    public static void Apply(Window window, FrameworkElement console)
    {
        if (window.WindowState == WindowState.Normal)
        {
            var extra = DockingDeficit(console.ActualWidth, window.ActualWidth, SystemParameters.WorkArea.Width);
            if (extra > 0) window.Width = window.ActualWidth + extra;
        }
        PaintCaption(window, console);
    }

    /// <summary>제목 줄을 콘솔 머리 색으로 칠한다. 창 핸들이 아직 없으면 생긴 뒤에 칠한다.</summary>
    public static void PaintCaption(Window window, FrameworkElement console)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        var caption = (console.TryFindResource("SurfaceAltBrush") as SolidColorBrush)?.Color;
        var text = (console.TryFindResource("TextPrimaryBrush") as SolidColorBrush)?.Color;
        if (caption is not { } back) return;

        try
        {
            var dark = IsDark(back) ? 1 : 0;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            var colorRef = ToColorRef(back);
            DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref colorRef, sizeof(int));
            if (text is { } fore)
            {
                var textRef = ToColorRef(fore);
                DwmSetWindowAttribute(handle, DWMWA_TEXT_COLOR, ref textRef, sizeof(int));
            }
        }
        catch (DllNotFoundException) { }          // DWM 이 없는 환경(서버 코어 등) — 기본 제목 줄 그대로
        catch (EntryPointNotFoundException) { }
    }

    /// <summary>상대 휘도가 절반 아래면 어두운 바탕이다.</summary>
    public static bool IsDark(Color color) => (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 < 0.5;

    /// <summary>WPF 색 → Win32 COLORREF(0x00BBGGRR).</summary>
    public static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
