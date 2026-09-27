using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 두 줄 툴바(D-2026-09-27-71c353) — 실제 커널 템플릿(Utils/Themes/Generic.xaml)을 물려 헤드리스로 잰다.
/// 필터 칩은 둘째 줄에 서고(있을 때만), 첫째 줄의 검색 · 동작 단추와 폭을 나눠 먹지 않는다.
/// </summary>
/// <remarks>
/// 판정 자체는 <see cref="ConsoleLayoutMath.ResolveToolbarFit"/>(순수 함수 — <c>ConsoleLayoutFitTests</c>)가 하지만,
/// "필터를 예산에 넣지 않는다" · "보이는 필터가 있을 때만 줄을 세운다" 는 <see cref="ConsoleToolbar"/> 쪽의 배선이라
/// 컨트롤을 직접 만들어 재야 한다. Theme 어셈블리의 버튼 스타일(DynamicResource)은 여기서 풀리지 않아 기본 버튼으로
/// 그려진다 — 폭 판정과는 무관하다(판정은 실제로 잰 폭을 입력으로 쓴다).
/// </remarks>
[Collection(WpfApplicationCollection.Name)]
public class ConsoleToolbarTwoRowTests
{
    private static readonly object EnsureApplicationGate = new();

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }

    private static ConsoleToolbar NewToolbar()
    {
        lock (EnsureApplicationGate)
        {
            if (Application.Current == null) _ = new Application();
        }
        var kernel = (ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml", UriKind.Relative));
        return new ConsoleToolbar { Style = (Style)kernel[typeof(ConsoleToolbar)], ConsoleKey = "Probe", ColumnsText = "열 6/11" };
    }

    /// <summary>
    /// 헤드리스 레이아웃 — 툴바의 판정은 <see cref="UIElement.LayoutUpdated"/> 에서 돈다. 판정이 값을 바꾸면
    /// 다음 패스가 필요하므로(필터 줄 여백 · 검색 폭) 몇 번 되풀이해 정착시킨다.
    /// </summary>
    private static void Settle(FrameworkElement element, double width)
    {
        for (var i = 0; i < 6; i++)
        {
            element.Measure(new Size(width, double.PositiveInfinity));
            element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height));
            element.UpdateLayout();
        }
    }

    /// <summary>칩 모양의 고정 크기 상자 n 개 — 폭 판정만 보면 되므로 실제 칩 스타일은 필요 없다.</summary>
    private static WrapPanel Chips(int count, double chipWidth = 90)
    {
        var panel = new WrapPanel();
        for (var i = 0; i < count; i++)
            panel.Children.Add(new Border { Width = chipWidth, Height = 26, Margin = new Thickness(0, 2, 6, 2) });
        return panel;
    }

    private static TextBox SearchBox(ConsoleToolbar toolbar) => (TextBox)toolbar.Template.FindName("Search", toolbar);

    [Fact]
    public void should_not_show_the_filter_row_when_the_toolbar_has_no_filters()
    {
        // Arrange + Act
        var (hasRow, height) = OnSta(() =>
        {
            var toolbar = NewToolbar();
            Settle(toolbar, 756);
            return (toolbar.HasFilterRow, toolbar.ActualHeight);
        });

        // Assert — 필터가 없는 콘솔(장비 · 서버 · 부대 · 템플릿)은 예전 그대로 48 한 줄이다
        Assert.False(hasRow);
        Assert.Equal(ConsoleLayoutMath.ToolbarHeight, height, 1);
    }

    [Fact]
    public void should_show_the_filter_row_under_the_action_row_when_filters_are_visible()
    {
        // Arrange + Act
        var (hasRow, height) = OnSta(() =>
        {
            var toolbar = NewToolbar();
            toolbar.Filters = Chips(5);
            Settle(toolbar, 756);
            return (toolbar.HasFilterRow, toolbar.ActualHeight);
        });

        // Assert — 첫째 줄 48 + 둘째 줄(최소 40)
        Assert.True(hasRow);
        Assert.True(height >= ConsoleLayoutMath.ToolbarHeight + ConsoleLayoutMath.FilterRowMinHeight - 0.5, $"툴바 높이 {height}");
    }

    [Fact]
    public void should_hide_the_filter_row_when_every_filter_is_collapsed()
    {
        // Arrange — 이벤트 개요 · 계정 사용자 레일처럼 필터 묶음은 있지만 이 레일에서는 전부 숨은 경우
        var (hasRow, height) = OnSta(() =>
        {
            var toolbar = NewToolbar();
            var chips = Chips(5);
            chips.Visibility = Visibility.Collapsed;
            toolbar.Filters = chips;
            Settle(toolbar, 756);
            return (toolbar.HasFilterRow, toolbar.ActualHeight);
        });

        // Assert — 빈 띠(여백 · 선)를 남기지 않는다
        Assert.False(hasRow);
        Assert.Equal(ConsoleLayoutMath.ToolbarHeight, height, 1);
    }

    [Fact]
    public void should_bring_the_filter_row_back_when_a_hidden_filter_becomes_visible()
    {
        // Arrange — 레일을 바꾸면 창이 필터의 Visibility 만 바꾼다(콘텐츠는 그대로)
        var (before, after) = OnSta(() =>
        {
            var toolbar = NewToolbar();
            var chips = Chips(3);
            chips.Visibility = Visibility.Collapsed;
            toolbar.Filters = chips;
            Settle(toolbar, 756);
            var hidden = toolbar.HasFilterRow;

            // Act
            chips.Visibility = Visibility.Visible;
            Settle(toolbar, 756);
            return (hidden, toolbar.HasFilterRow);
        });

        // Assert
        Assert.False(before);
        Assert.True(after);
    }

    [Theory]
    [InlineData(756)]   // 1280 도킹 — 목록 1280 − 184 − 340
    [InlineData(606)]   // 1150 서랍이 열림 — 목록 966 − 서랍 360
    [InlineData(484)]   // 900 접힘 + 서랍 열림 — 목록 844 − 서랍 360
    public void should_keep_the_search_box_at_full_width_even_when_the_filters_are_wider_than_the_toolbar(double width)
    {
        // Arrange — 칩 12개(약 1150px) — 옛 한 줄 구조였다면 검색이 아이콘으로 접히고 [열] 까지 [⋯] 로 밀렸다
        var (compact, searchWidth, actual, hasRow) = OnSta(() =>
        {
            var toolbar = NewToolbar();
            toolbar.Filters = Chips(12);
            Settle(toolbar, width);
            var search = SearchBox(toolbar);
            return (toolbar.IsSearchCompact, toolbar.SearchBoxWidth, search.ActualWidth, toolbar.HasFilterRow);
        });

        // Assert
        Assert.True(hasRow);
        Assert.False(compact);
        Assert.True(searchWidth >= ConsoleLayoutMath.ToolbarSearchFullMinWidth, $"검색 폭 {searchWidth}");
        Assert.Equal(searchWidth, actual, 1);
    }

    [Fact]
    public void should_not_change_the_action_row_when_filters_are_added()
    {
        // Arrange + Act — 같은 폭에서 필터 없음 / 넓은 필터 — 첫째 줄의 판정(검색 폭 · 넘침)이 같아야 한다
        var (without, with) = OnSta(() =>
        {
            ConsoleToolbar Make(object? filters)
            {
                var toolbar = NewToolbar();
                toolbar.Extra = new Border { Width = 200, Height = 32 };
                toolbar.Filters = filters;
                Settle(toolbar, 606);
                return toolbar;
            }

            var a = Make(null);
            var b = Make(Chips(12));
            return ((a.SearchBoxWidth, a.IsExtraOverflow, a.IsColumnsOverflow, a.IsSearchCompact),
                    (b.SearchBoxWidth, b.IsExtraOverflow, b.IsColumnsOverflow, b.IsSearchCompact));
        });

        // Assert
        Assert.Equal(without, with);
    }

    [Fact]
    public void should_wrap_filter_chips_inside_the_filter_row_instead_of_clipping_them_when_narrow()
    {
        // Arrange + Act — 좁은 폭에서 칩 12개: 한 줄에 다 안 들어가면 둘째 줄이 키를 키워 다 보인다(잘라 먹지 않는다)
        var (rowHeight, lastChipBottom, lastChipRight, toolbarWidth) = OnSta(() =>
        {
            var toolbar = NewToolbar();
            var chips = Chips(12);
            toolbar.Filters = chips;
            Settle(toolbar, 484);
            var last = (FrameworkElement)chips.Children[^1];
            var corner = last.TransformToAncestor(toolbar).Transform(new Point(last.ActualWidth, last.ActualHeight));
            return (toolbar.ActualHeight - ConsoleLayoutMath.ToolbarHeight, corner.Y, corner.X, toolbar.ActualWidth);
        });

        // Assert — 마지막 칩이 툴바 안(오른쪽 · 아래)에 온전히 들어온다
        Assert.True(rowHeight > ConsoleLayoutMath.FilterRowMinHeight, $"필터 줄 높이 {rowHeight}");
        Assert.True(lastChipRight <= toolbarWidth - 12 + 0.5, $"마지막 칩 오른쪽 {lastChipRight} / 툴바 {toolbarWidth}");
        Assert.True(lastChipBottom <= ConsoleLayoutMath.ToolbarHeight + rowHeight + 0.5, $"마지막 칩 아래 {lastChipBottom}");
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_keep_every_action_row_button_inside_the_toolbar_at_any_width(bool extraIsEmptyInside)
    {
        // Arrange — Extra 가 단추 둘(제자리 폭 있음) / 단추가 전부 숨은 묶음(폭 0 인데 자리 여백 8 은 남는다 — 장비 6.3 서버의 조립 · 프리셋)
        var cut = OnSta(() =>
        {
            var failures = new System.Collections.Generic.List<string>();
            for (var width = 300; width <= 820; width += 2)
            {
                var toolbar = NewToolbar();
                var extra = new StackPanel { Orientation = Orientation.Horizontal };
                extra.Children.Add(new Button { Content = "조립 · 프리셋", Width = 120, Margin = new Thickness(0, 0, 8, 0), Visibility = extraIsEmptyInside ? Visibility.Collapsed : Visibility.Visible });
                extra.Children.Add(new Button { Content = "셋업 · 결선", Width = 90, Visibility = extraIsEmptyInside ? Visibility.Collapsed : Visibility.Visible });
                toolbar.Extra = extra;
                toolbar.ColumnsText = "열 3/14";
                Settle(toolbar, width);

                // Act — 첫째 줄에서 보이는 단추 · 검색의 오른쪽 끝
                foreach (var element in Descendants(toolbar).OfType<FrameworkElement>()
                             .Where(e => e is System.Windows.Controls.Primitives.ButtonBase or TextBox && IsShown(e, toolbar) && e.ActualWidth > 0))
                {
                    var right = element.TransformToAncestor(toolbar).Transform(new Point(element.ActualWidth, 0)).X;
                    if (right > width - 12 + 0.5) failures.Add($"{width}: {element.GetType().Name} {System.Windows.Automation.AutomationProperties.GetAutomationId(element)} 오른쪽 {right:0.#}");
                }
            }
            return failures;
        });

        // Assert — 오른쪽 여백(12) 안쪽에서 끝난다(테두리 밖으로 잘리지 않는다)
        Assert.True(cut.Count == 0, string.Join(" ; ", cut.Take(8)));
    }

    /// <summary>
    /// 헤드리스에서는 <see cref="UIElement.IsVisible"/> 이 늘 거짓이다(보이는 창에 붙지 않았다) — 조상까지 Visibility 로 판정한다.
    /// (IsVisible 로 거르면 모든 요소가 빠져 시험이 공허하게 통과한다 — 실측.)
    /// </summary>
    private static bool IsShown(FrameworkElement element, FrameworkElement root)
    {
        for (DependencyObject? d = element; d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
            if (d is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deep in Descendants(child)) yield return deep;
        }
    }
}
