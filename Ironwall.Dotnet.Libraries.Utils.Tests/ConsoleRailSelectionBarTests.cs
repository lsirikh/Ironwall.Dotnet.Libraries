using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 레일 선택 막대(<c>Bar</c>)가 선택 배경(<c>Bd</c>) <b>안쪽 왼쪽 가장자리</b>에 붙는가.
/// </summary>
/// <remarks>
/// 2026-09-29 사용자 보고(장비 · 이벤트 콘솔 캡처): 막대가 <c>Margin -8</c> 로 배경 밖 레일 가장자리에 떨어져 창 테두리 위에 그려졌다.
/// 정본 목업 <c>.rail .ri.on::before{left:0; border-radius:3px 0 0 3px}</c> — 막대는 배경의 왼쪽 가장자리에 붙는다.
/// </remarks>
[Collection(WpfFocusCollection.Name)]
public class ConsoleRailSelectionBarTests
{
    [Fact]
    public void should_draw_the_selection_bar_on_the_left_edge_inside_the_selected_background_when_an_item_is_selected()
    {
        var (bar, background, railLeft) = OnSta(() =>
        {
            var entries = new[] { new ConsoleRailEntry("a", "가"), new ConsoleRailEntry("b", "나") };
            var rail = new ConsoleRail { ItemsSource = entries };
            var window = new Window
            {
                Content = rail, Width = 240, Height = 300, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false,
            };
            window.Show();
            rail.SelectedItem = entries[1];
            Pump(DispatcherPriority.Loaded);

            var container = (ConsoleRailItem)rail.ItemContainerGenerator.ContainerFromItem(entries[1]);
            var barPart = (FrameworkElement)container.Template.FindName("Bar", container);
            var bdPart = (FrameworkElement)container.Template.FindName("Bd", container);
            var result = (Bounds(barPart, rail), Bounds(bdPart, rail), 0d);
            window.Close();
            return result;
        });

        Assert.Equal(background.Left, bar.Left, 1);          // 배경 왼쪽 가장자리에 붙는다(예전: 8 DIU 바깥)
        Assert.True(bar.Left > railLeft + 0.5, $"막대가 레일 가장자리(창 테두리)에 붙어 있다 — 막대 {bar} · 배경 {background}");
        Assert.True(bar.Top >= background.Top - 0.5 && bar.Bottom <= background.Bottom + 0.5, $"막대가 배경 세로 범위를 벗어난다 — 막대 {bar} · 배경 {background}");
        Assert.Equal(3, bar.Width, 1);
    }

    private static Rect Bounds(FrameworkElement element, Visual root)
        => element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    private static void Pump(DispatcherPriority priority)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() => { try { result = body(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 스레드가 제시간에 끝나지 않았다");
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
        return result;
    }
}
