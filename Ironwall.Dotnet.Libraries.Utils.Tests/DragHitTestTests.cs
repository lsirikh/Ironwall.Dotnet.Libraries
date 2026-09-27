using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 드롭존 판정의 "커서 아래" — 셸 창(MetroWindow)은 창 전체를 덮는 숨은 대화상자 덮개(PART_OverlayBox)를 둔다.
/// 걸러 내지 않은 HitTest 는 그 덮개를 집어 셸 콘솔의 모든 드롭이 말없이 사라졌다(2026-09-27 실창 기록).
/// </summary>
public class DragHitTestTests
{
    [Fact]
    public void should_find_the_content_under_a_hidden_overlay_when_the_window_keeps_a_dialog_overlay_on_top()
    {
        var (raw, filtered) = OnSta(() =>
        {
            var (root, content) = ShellLike(overlayVisibility: Visibility.Hidden, overlayHitTestVisible: true);
            var point = new Point(50, 50);
            return (VisualTreeHelper.HitTest(root, point)?.VisualHit, DragHitTest.Top(root, point) == content);
        });

        // 덮개가 창을 덮은 상태 그대로 — 거르지 않으면 덮개를 집는다(실창에서 본 것)
        Assert.IsType<Grid>(raw);
        Assert.True(filtered);
    }

    [Fact]
    public void should_skip_an_overlay_that_does_not_take_input_when_it_is_visible_but_not_hit_test_visible()
    {
        var found = OnSta(() =>
        {
            var (root, content) = ShellLike(overlayVisibility: Visibility.Visible, overlayHitTestVisible: false);
            return DragHitTest.Top(root, new Point(50, 50)) == content;
        });

        Assert.True(found);
    }

    [Fact]
    public void should_skip_the_drag_ghost_when_it_sits_under_the_pointer()
    {
        var found = OnSta(() =>
        {
            var content = new Border { Background = Brushes.White, Width = 200, Height = 200 };
            var decorator = new AdornerDecorator { Child = content };
            var window = new Window { Content = decorator, Width = 300, Height = 300, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000 };
            window.Show();
            try
            {
                var layer = AdornerLayer.GetAdornerLayer(content)!;
                var ghost = new DragGhostAdorner(content, layer, "끌고 있는 행", 1);
                layer.Add(ghost);
                ghost.MoveTo(new Point(50, 50));
                window.UpdateLayout();
                var hit = DragHitTest.Top(window, content.TranslatePoint(new Point(50, 50), window));
                return hit == content;
            }
            finally { window.Close(); }
        });

        Assert.True(found);
    }

    [Fact]
    public void should_return_null_when_nothing_takes_input_at_the_point()
    {
        var hit = OnSta(() =>
        {
            var (root, _) = ShellLike(overlayVisibility: Visibility.Hidden, overlayHitTestVisible: true);
            return DragHitTest.Top(root, new Point(5000, 5000));
        });

        Assert.Null(hit);
    }

    // ── 행 안의 드롭존(부대 편제 트리 · 2026-09-27 실창 기록) ──
    // 행의 드롭존(DropZoneChrome)은 배경이 없고 행 컨테이너(ListBoxItem)의 안쪽 여백보다 작다.
    // 그래서 행의 빈 곳 · 행 사이 틈에서 HitTest 는 ListBoxItem 의 Border#Bd 를 집었고 판정이 꺼졌다
    // (hit=Border#Bd < Grid < ListBoxItem, zone=(none)) — 놓으면 말없이 사라지고 끄는 동안 윤곽이 깜빡였다.

    [Fact]
    public void should_resolve_the_row_zone_when_the_pointer_is_in_the_row_padding_outside_the_zone()
    {
        var found = OnSta(() => WithRowList(1, (window, list, zones) =>
        {
            var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            var origin = container.TranslatePoint(new Point(0, 0), window);
            var inPadding = new Point(origin.X + 2, origin.Y + container.ActualHeight / 2);   // 행 안 · 드롭존 여백 밖
            return DragHitTest.ZoneFrom(DragHitTest.Top(window, inPadding)) == zones[0];
        }));

        Assert.True(found);
    }

    [Fact]
    public void should_resolve_the_row_zone_when_the_pointer_is_in_the_hollow_part_of_the_zone()
    {
        var found = OnSta(() => WithRowList(1, (window, list, zones) =>
        {
            var zone = zones[0];
            var origin = zone.TranslatePoint(new Point(0, 0), window);
            var hollow = new Point(origin.X + zone.ActualWidth - 10, origin.Y + zone.ActualHeight / 2);   // 글자 오른쪽 빈 곳
            return DragHitTest.ZoneFrom(DragHitTest.Top(window, hollow)) == zone;
        }));

        Assert.True(found);
    }

    [Fact]
    public void should_resolve_each_row_to_its_own_zone_when_several_rows_carry_zones()
    {
        var (first, second) = OnSta(() => WithRowList(2, (window, list, zones) =>
        {
            FrameworkElement? At(int index)
            {
                var c = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(index);
                var o = c.TranslatePoint(new Point(0, 0), window);
                return DragHitTest.ZoneFrom(DragHitTest.Top(window, new Point(o.X + 2, o.Y + c.ActualHeight / 2)));
            }
            return (At(0) == zones[0], At(1) == zones[1]);
        }));

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public void should_not_guess_a_zone_when_the_row_holds_two_zones()
    {
        var hit = OnSta(() => WithRowList(1, (window, list, zones) =>
        {
            var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            var origin = container.TranslatePoint(new Point(0, 0), window);
            return DragHitTest.ZoneFrom(DragHitTest.Top(window, new Point(origin.X + 2, origin.Y + container.ActualHeight / 2)));
        }, zonesPerRow: 2));

        // 한 행에 드롭존이 둘이면(칩 여럿) 빈 곳이 어느 쪽인지 모른다 — 짐작하지 않는다.
        Assert.Null(hit);
    }

    [Fact]
    public void should_keep_the_list_zone_when_the_list_itself_is_the_reorder_zone()
    {
        var isList = OnSta(() => WithRowList(1, (window, list, _) =>
        {
            DropZone.SetKey(list, "reorder");
            var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            var origin = container.TranslatePoint(new Point(0, 0), window);
            // 행 안에 드롭존이 없으면 행의 빈 곳은 목록(순서 드롭존)이다 — 새 규칙이 순서 드롭을 가로채면 안 된다.
            return DragHitTest.ZoneFrom(DragHitTest.Top(window, new Point(origin.X + 2, origin.Y + container.ActualHeight / 2))) is ListBox;
        }, zonesPerRow: 0));

        Assert.True(isList);
    }

    [Fact]
    public void should_not_route_a_row_header_into_a_nested_reorder_list_when_the_row_holds_one()
    {
        // 지도 레이어 패널 모양: 바깥 목록의 행(섹션) 안에 순서 드롭존 ListBox 가 들어 있다. 섹션 머리 위는 그 목록이 아니다 —
        // 목록으로 치면 삽입 위치가 "맨 끝"이 되어, 머리 위(목록 위쪽)에서 놓은 행이 맨 아래로 간다.
        var hit = OnSta(() =>
        {
            var inner = new ListBox { Height = 60 };
            inner.Items.Add("레이어 1");
            DropZone.SetKey(inner, "layer-reorder");
            DropZone.SetIsReorder(inner, true);
            var section = new StackPanel();
            section.Children.Add(new Border { Height = 30, Background = Brushes.LightGray, Child = new TextBlock { Text = "섹션 머리" } });
            section.Children.Add(inner);
            var outer = new ListBox { Width = 300 };
            outer.Items.Add(section);
            var window = new Window { Content = outer, Width = 400, Height = 300, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000 };
            window.Show();
            try
            {
                window.UpdateLayout();
                var header = section.Children[0];
                var point = header.TranslatePoint(new Point(40, 15), window);
                return DragHitTest.ZoneFrom(DragHitTest.Top(window, point));
            }
            finally { window.Close(); }
        });

        Assert.Null(hit);
    }

    [Fact]
    public void should_resolve_zones_when_another_ui_thread_still_holds_a_drop_zone()
    {
        // 드롭존 장부는 프로세스 전역이다. 다른 UI 스레드가 만든 드롭존이 살아 있으면(별도 스레드 창) 그것을 읽는 순간
        // "다른 스레드가 이 개체를 소유" 로 던졌다 — 끄는 도중 입력 처리 한가운데서.
        var foreign = OnSta(() => { var b = new Border(); DropZone.SetKey(b, "foreign"); return b; });

        var found = OnSta(() => WithRowList(1, (window, list, zones) =>
        {
            var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            var origin = container.TranslatePoint(new Point(0, 0), window);
            return DragHitTest.ZoneFrom(DragHitTest.Top(window, new Point(origin.X + 2, origin.Y + container.ActualHeight / 2))) == zones[0];
        }));

        Assert.True(found);
        GC.KeepAlive(foreign);
    }

    /// <summary>
    /// 부대 편제 트리 모양: 행(ListBoxItem, 안쪽 여백 8)마다 <b>배경 없는</b> 드롭존(Border, 여백 6) 안에 왼쪽 글자 하나.
    /// </summary>
    private static T WithRowList<T>(int rows, Func<Window, ListBox, List<FrameworkElement>, T> body, int zonesPerRow = 1)
    {
        var zones = new List<FrameworkElement>();
        var list = new ListBox { Width = 400 };
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        list.ItemContainerStyle = itemStyle;
        for (var i = 0; i < rows; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            for (var z = 0; z < Math.Max(1, zonesPerRow); z++)
            {
                var zone = new Border
                {
                    Margin = new Thickness(6),
                    Width = zonesPerRow <= 1 ? 300 : 120,
                    Child = new TextBlock { Text = $"부대 {i}", HorizontalAlignment = HorizontalAlignment.Left },
                };
                if (zonesPerRow > 0) { DropZone.SetKey(zone, "unit-parent"); zones.Add(zone); }
                row.Children.Add(zone);
            }
            list.Items.Add(row);
        }
        var window = new Window { Content = list, Width = 500, Height = 300, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000 };
        window.Show();
        try
        {
            window.UpdateLayout();
            return body(window, list, zones);
        }
        finally { window.Close(); }
    }

    /// <summary>셸 모양: 내용 위에 창 전체를 덮는 덮개 Grid(배경 있음) — MetroWindow 템플릿의 PART_OverlayBox 와 같은 배치.</summary>
    private static (Grid Root, Border Content) ShellLike(Visibility overlayVisibility, bool overlayHitTestVisible)
    {
        var content = new Border { Background = Brushes.White };
        var overlay = new Grid { Background = Brushes.Black, Visibility = overlayVisibility, IsHitTestVisible = overlayHitTestVisible };
        var root = new Grid { Width = 200, Height = 200 };
        root.Children.Add(content);
        root.Children.Add(overlay);
        root.Measure(new Size(200, 200));
        root.Arrange(new Rect(0, 0, 200, 200));
        root.UpdateLayout();
        return (root, content);
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw new InvalidOperationException("STA body failed", error);
        return result;
    }
}
