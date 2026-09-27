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
