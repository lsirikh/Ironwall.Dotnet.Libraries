using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 커널 달력 팝업 두 벌(<see cref="DateTimeRangeField"/> · <see cref="DateTimeField"/>)이 함께 쓰는 달력 배선 — 한 곳에만 둔다.
/// </summary>
/// <remarks>
/// <para>원래 <see cref="DateTimeRangeField"/> 안에 있던 K-13 재도색 경로를 그대로 옮겼다(B7 — 단일 날짜 칸이 같은 달력을 쓰게 되면서
/// 베끼지 않고 나눠 쓰려고). 동작은 바뀌지 않는다.</para>
/// <para>K-13 — CalendarDayButton/CalendarButton/Button(헤더·화살표)은 Style.Resources 도, Calendar 인스턴스 자신의 Resources 도,
/// CalendarItem 자신의 ControlTemplate.Resources 도 전부 무력했다(실측 5종). 이기는 것은 <b>로컬 값</b> 뿐이라, 달력이 낱칸을 만든 뒤
/// (초기 · 월 이동 · 새로 열 때마다) 시각 트리를 걸어 Style 을 인스턴스별로 직접 대입한다.</para>
/// </remarks>
internal static class ConsoleCalendarSkin
{
    /// <summary>
    /// K-13 실측 계속 — <c>TryFindResource(...)</c> 로는 같은 테마 사전의 형제 키("Console.DateTimeRange.CalendarDayCell")를 못 찾는다
    /// (DefaultStyleKey 해석은 그 컨트롤 자신의 키만 낱개로 찾아준다). 사전을 직접 로드해서 읽는다.
    /// </summary>
    private static readonly Lazy<ResourceDictionary> CalendarResources = new(() => new ResourceDictionary
    {
        Source = new Uri("pack://application:,,,/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml"),
    });

    /// <summary>달력 낱칸 재도색을 Loaded 우선순위로 미룬다 — 낱칸은 이 시점엔 아직 안 만들어졌을 수 있다.</summary>
    public static void RestyleDeferred(Calendar calendar)
        => calendar.Dispatcher.BeginInvoke(new Action(() => Restyle(calendar)), DispatcherPriority.Loaded);

    /// <summary>달력 안의 날짜 칸 · 월/연 칸 · 머리 단추에 커널 스타일을 로컬 값으로 준다.</summary>
    public static void Restyle(Calendar calendar)
    {
        var resources = CalendarResources.Value;
        var dayStyle = resources["Console.DateTimeRange.CalendarDayCell"] as Style;
        var monthStyle = resources["Console.DateTimeRange.CalendarButtonCell"] as Style;
        var chromeStyle = resources["Console.DateTimeRange.CalendarButtonChrome"] as Style;

        foreach (var child in Descendants(calendar))
        {
            switch (child)
            {
                case CalendarDayButton dayButton when dayStyle is not null:
                    dayButton.Style = dayStyle;
                    break;
                case CalendarButton monthButton when monthStyle is not null:
                    monthButton.Style = monthStyle;
                    break;
                case Button plainButton when chromeStyle is not null:
                    plainButton.Style = chromeStyle;
                    break;
            }
        }
    }

    /// <summary>
    /// WPF 표준 Calendar 는 날짜를 누른 뒤에도 마우스 캡처를 쥔 채 놓지 않는다(CalendarItem) — 그대로 두면 팝업 안 [적용] · [취소] 가
    /// 첫 클릭을 캡처 해제에 빼앗겨 두 번 눌러야 먹는다. 마우스를 뗄 때 CalendarItem 이 쥔 캡처만 푼다(끌어 고르는 동안에는 건드리지 않는다).
    /// </summary>
    public static void ReleaseCalendarCapture()
    {
        if (Mouse.Captured is CalendarItem) Mouse.Capture(null);
    }

    /// <summary>눌린 곳이 날짜 칸(CalendarDayButton) 안인가 — 단일 날짜 칸이 "누르면 바로 확정" 을 판정한다.</summary>
    public static bool IsInsideDayButton(object? originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is CalendarDayButton) return true;
            if (current is Calendar) return false;
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var grandchild in Descendants(child)) yield return grandchild;
        }
    }
}
