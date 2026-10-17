using System.Globalization;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;

/****************************************************************************
   Purpose      : 모니터 식별 카드 — 번호 · 글자 · 강조 · 배치 계산 (화면 없이 잠그는 순수 함수)
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 모니터 한 대에 띄울 식별 카드 한 장(Windows "식별"과 같은 것).
/// </summary>
/// <param name="Number">크게 쓰는 번호 — 고르는 목록의 번호와 같아야 한다.</param>
/// <param name="Label">고르는 목록에 보이는 글자 그대로(예: <c>모니터 2 · 1920×1080 · 주</c>).</param>
/// <param name="DeviceName">Win32 장치 이름(<c>\\.\DISPLAY2</c>) — 배치 때 이 이름으로 모니터를 다시 찾는다.</param>
/// <param name="PhysicalBounds">모니터 전체(물리 픽셀). 장치 이름으로 못 찾을 때만 쓴다.</param>
/// <param name="Dpi">그 모니터의 유효 DPI(96 = 100%).</param>
/// <param name="IsSelected">지금 고른 모니터인가 — 굵은 테두리 + "선택됨" 글자로 표시한다(색만으로 구분하지 않는다).</param>
public sealed record MonitorIdentifyCard(int Number, string Label, string DeviceName, Int32Rect PhysicalBounds, int Dpi, bool IsSelected);

/// <summary>배치 좌표계 — 이 스레드(프로세스)의 DPI 인지 수준.</summary>
public enum MonitorIdentifyDpiMode
{
    /// <summary>DPI 비인지 — 모든 좌표가 96 DPI 로 가상화된다.</summary>
    Unaware,

    /// <summary>시스템 DPI 인지(WPF 기본) — 좌표는 시스템 DPI 기준 논리 픽셀, 다른 DPI 모니터는 OS 가 늘려 그린다.</summary>
    System,

    /// <summary>모니터별 DPI 인지 — 좌표는 물리 픽셀, WPF 가 모니터 DPI 로 그린다.</summary>
    PerMonitor,
}

/// <summary>
/// 식별 카드의 판정 — 시간 · 크기 · 위치 · 창 확장 스타일 · 접근성 식별자. WPF 창과 Win32 호출은
/// <see cref="MonitorIdentifyWindow"/> 가 하고, 무엇을 어디에 얼마나 띄울지는 전부 여기서 정한다.
/// </summary>
public static class MonitorIdentifyMath
{
    #region - 상수 -
    /// <summary>카드 창 크기(DIU). 카드는 이 안 가운데에 놓이고, 넘치면 줄어든다(잘리지 않는다).</summary>
    public const double WindowWidthDiu = 440;
    public const double WindowHeightDiu = 300;

    /// <summary>보이는 동안의 불투명도 — 뒤가 살짝 비치는 반투명.</summary>
    public const double ShownOpacity = 0.94;

    /// <summary>강조 테두리 두께(고른 모니터) · 보통 테두리 두께.</summary>
    public const double SelectedOutline = 6;
    public const double NormalOutline = 1;

    /// <summary>고른 모니터 카드에 붙는 글자 — 테두리 두께와 함께 형태로 구분한다.</summary>
    public const string SelectedText = "선택됨";

    public const int BaseDpi = 96;

    /// <summary>카드가 떠 있는 전체 시간(나타남 · 사라짐 포함).</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(300);

    /// <summary>WS_EX_* — 클릭 통과 · 활성화 안 함 · 작업 표시줄 · Alt+Tab 에서 빠짐 · 계층 창.</summary>
    public const long WsExTransparent = 0x00000020;
    public const long WsExToolWindow = 0x00000080;
    public const long WsExAppWindow = 0x00040000;
    public const long WsExLayered = 0x00080000;
    public const long WsExNoActivate = 0x08000000;
    #endregion

    /// <summary>창 루트의 접근성 식별자 — <c>{prefix}.{번호}</c>.</summary>
    public static string AutomationId(string prefix, int number)
        => string.Create(CultureInfo.InvariantCulture, $"{prefix}.{number}");

    /// <summary>크게 쓰는 번호 글자.</summary>
    public static string NumberText(int number) => number.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// 움직임 줄이기 — Windows "애니메이션 효과"가 꺼져 있으면(<see cref="SystemParameters.ClientAreaAnimation"/> = false)
    /// 나타남 · 사라짐 없이 바로 보이고 바로 닫는다.
    /// </summary>
    public static bool ShouldAnimate(bool clientAreaAnimation) => clientAreaAnimation;

    /// <summary>
    /// 사라짐을 시작할 때 — 애니메이션이면 <see cref="Duration"/> − <see cref="FadeOut"/>, 아니면 <see cref="Duration"/>.
    /// 어느 쪽이든 카드는 <see cref="Duration"/> 안에 닫힌다.
    /// </summary>
    public static TimeSpan DismissAfter(bool animate) => animate ? Duration - FadeOut : Duration;

    /// <summary>쓸 수 없는 DPI(0 · 음수 · 터무니없이 큰 값)는 96 으로.</summary>
    public static int SafeDpi(int dpi) => dpi is > 0 and <= 960 ? dpi : BaseDpi;

    /// <summary>
    /// 이 좌표계에서 DIU 1 이 몇 픽셀인가. 시스템 인지면 시스템 DPI, 모니터별 인지면 그 모니터 DPI, 비인지면 1.
    /// </summary>
    public static double PixelsPerDiu(MonitorIdentifyDpiMode mode, int systemDpi, int monitorDpi) => mode switch
    {
        MonitorIdentifyDpiMode.Unaware => 1.0,
        MonitorIdentifyDpiMode.PerMonitor => SafeDpi(monitorDpi) / (double)BaseDpi,
        _ => SafeDpi(systemDpi) / (double)BaseDpi,
    };

    /// <summary>
    /// 모니터 가운데에 놓을 창 자리(그 좌표계의 픽셀). 창이 모니터보다 크면 모니터 크기로 줄인다(카드는 안에서 줄어든다).
    /// 빈 모니터면 <see cref="Int32Rect.Empty"/> — 띄우지 않는다.
    /// </summary>
    public static Int32Rect CenteredWindow(Int32Rect monitor, double pixelsPerDiu,
                                           double widthDiu = WindowWidthDiu, double heightDiu = WindowHeightDiu)
    {
        if (monitor.IsEmpty || monitor.Width <= 0 || monitor.Height <= 0) return Int32Rect.Empty;
        var ppd = double.IsFinite(pixelsPerDiu) && pixelsPerDiu > 0 ? pixelsPerDiu : 1.0;

        var width = Math.Min(monitor.Width, (int)Math.Ceiling(widthDiu * ppd));
        var height = Math.Min(monitor.Height, (int)Math.Ceiling(heightDiu * ppd));
        var x = monitor.X + (monitor.Width - width) / 2;
        var y = monitor.Y + (monitor.Height - height) / 2;
        return new Int32Rect(x, y, width, height);
    }

    /// <summary>
    /// 이 좌표계에서 본 모니터 자리 — 장치 이름이 같은 것(대소문자 무시). 없으면 카드의 물리 자리 그대로.
    /// </summary>
    /// <remarks>
    /// 목록은 모니터별 인지로 읽은 <b>물리</b> 픽셀이고, 창은 이 프로세스의 좌표계(보통 시스템 인지)로 놓인다.
    /// 둘의 DPI 가 다른 모니터에서는 같은 숫자가 다른 자리를 뜻하므로, 이름으로 다시 찾아 이 좌표계의 값을 쓴다.
    /// </remarks>
    public static Int32Rect PickBounds(IEnumerable<(string DeviceName, Int32Rect Bounds)> seenInThreadSpace,
                                       string deviceName, Int32Rect fallback)
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            foreach (var (name, bounds) in seenInThreadSpace)
            {
                if (string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase) && !bounds.IsEmpty)
                    return bounds;
            }
        }
        return fallback;
    }

    /// <summary>
    /// 식별 창의 확장 스타일 — 클릭 통과 · 활성화 안 함 · 도구 창(작업 표시줄 · Alt+Tab 제외) · 계층 창을 켜고
    /// 작업 표시줄 강제 표시(APPWINDOW)는 끈다. 나머지 비트는 그대로 둔다.
    /// </summary>
    public static long ClickThroughExStyle(long current)
        => (current | WsExTransparent | WsExNoActivate | WsExToolWindow | WsExLayered) & ~WsExAppWindow;

    /// <summary>확장 스타일이 식별 창 조건(클릭 통과 · 활성화 안 함 · 도구 창)을 모두 갖췄는가.</summary>
    public static bool IsClickThrough(long exStyle)
        => (exStyle & WsExTransparent) != 0 && (exStyle & WsExNoActivate) != 0 && (exStyle & WsExToolWindow) != 0;
}
