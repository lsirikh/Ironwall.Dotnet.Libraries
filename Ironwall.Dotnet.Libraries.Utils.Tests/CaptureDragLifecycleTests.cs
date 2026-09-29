using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Microsoft.Xaml.Behaviors;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 캡처 드래그의 생애 — 실제 창(화면 밖) · 실제 마우스 캡처로 손잡이를 누르고, 포인터 자리만 <see cref="DragPointer.Override"/> 로 알려 준다.
/// </summary>
/// <remarks>
/// 이벤트 맵핑 워크벤치에서 잡힌 커널 결함 세 가지(2026-09-29 M-1):
/// <list type="number">
/// <item>끄는 도중 목록이 다시 그려져 잡은 행의 컨테이너가 바뀌면 손잡이의 끝남(DragCompleted)이 목록까지 올라오지 못해
/// 고스트 · 삽입선 · 커서 · <see cref="DragSession"/> 이 영영 남았다(Esc 도 삼키기만 했다).</item>
/// <item>출발 목록이 스크롤 뷰어(상세 칸) 안에 있으면 고스트가 그 뷰어의 어도너 층에 올라 칸 밖으로 나가면 잘렸고,
/// 가용 영역 하한이 0(출발 목록 왼쪽 끝)이라 왼쪽 칸 위로 끌어도 고스트가 출발 목록 가장자리에 붙어 따라오지 않았다.</item>
/// <item>놓을 수 없는 자리에서 놓으면 담당이 그 사실을 몰라 사유를 말할 기회가 없었다 — 말없이 사라졌다.</item>
/// </list>
/// </remarks>
[Collection(WpfFocusCollection.Name)]
public class CaptureDragLifecycleTests
{
    private sealed class Handler : IDragDropHandler, IDropRefusalHandler
    {
        public bool Accepts { get; set; }
        public List<string> Dropped { get; } = new();
        public List<string> Refused { get; } = new();
        public bool CanDrop(DragPayload payload, DropTarget target) => Accepts && target.ZoneKey == "target";
        public void Drop(DragPayload payload, DropTarget target) => Dropped.Add(target.ZoneKey);
        void IDropRefusalHandler.Refused(DragPayload payload, DropTarget target) => Refused.Add(target.ZoneKey);
    }

    private sealed class Stage
    {
        public required Window Window { get; init; }
        public required ListBox Source { get; init; }
        public required Border Target { get; init; }
        public required ScrollViewer Viewer { get; init; }
        public required ObservableCollection<string> Items { get; init; }
        public required Handler Handler { get; init; }
        public Point Pointer;

        public DragHandle HandleOf(int index)
        {
            var row = (DependencyObject)Source.ItemContainerGenerator.ContainerFromIndex(index);
            return Find<DragHandle>(row) ?? throw new InvalidOperationException($"행 {index} 에 손잡이가 없다");
        }

        public Point Center(FrameworkElement e) => e.TranslatePoint(new Point(e.ActualWidth / 2, e.ActualHeight / 2), Window);

        public void Press(DragHandle handle)
        {
            Pointer = Center(handle);
            handle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
            handle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent });
        }

        public void MoveTo(DragHandle handle, Point pointInWindow)
        {
            Pointer = pointInWindow;
            handle.RaiseEvent(new DragDeltaEventArgs(0, 0));
        }

        public void Release(DragHandle handle)
        {
            handle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });
            Window.UpdateLayout();
        }

        public IReadOnlyList<Adorner> DragAdorners() => Descendants<Adorner>(Window).Where(a => a is DragGhostAdorner or InsertionLineAdorner).ToList();
    }

    [Fact]
    public void should_clean_up_the_drag_when_the_held_row_is_replaced_while_dragging()
    {
        // 끄는 도중 목록을 다시 채운다(서버 재조회 · [되돌리기] · 탭 전환과 같다) — 잡은 행의 컨테이너가 사라진다.
        var after = OnStage((stage, handle) =>
        {
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X - 20, stage.Pointer.Y));
            stage.MoveTo(handle, stage.Center(stage.Target));
            var during = (stage.DragAdorners().Count, DragSession.IsActive);

            var fresh = stage.Items.ToList();
            stage.Items.Clear();
            foreach (var item in fresh) stage.Items.Add(item);
            Pump();                                     // 캡처 재평가 · 레이아웃은 디스패처가 돈다(실앱과 같다)

            return (during, stage.DragAdorners().Count, DragSession.IsActive, stage.Window.ReadLocalValue(FrameworkElement.CursorProperty) == DependencyProperty.UnsetValue);
        });

        Assert.Equal((1, true), after.during);          // 전제 — 실제로 끌고 있었다(고스트 1)
        Assert.Equal(0, after.Item2);                   // 고스트 · 삽입선이 남지 않는다
        Assert.False(after.Item3);                      // 다른 콘솔의 다시 읽기를 영영 막지 않는다
        Assert.True(after.Item4);                       // 창 커서가 SizeAll 로 남지 않는다
    }

    [Fact]
    public void should_float_the_ghost_on_the_console_layer_when_the_source_list_sits_in_a_scroll_viewer()
    {
        // 상세 칸(팔레트)처럼 출발 목록이 ScrollViewer 안에 있다 — 그 뷰어의 어도너 층은 뷰어 밖을 잘라 낸다.
        var owner = OnStage((stage, handle) =>
        {
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X - 20, stage.Pointer.Y));
            stage.MoveTo(handle, stage.Center(stage.Target));
            var ghost = stage.DragAdorners().OfType<DragGhostAdorner>().Single();
            var layer = (AdornerLayer)VisualTreeHelper.GetParent(ghost);
            return VisualTreeHelper.GetParent(layer)?.GetType().Name;
        });

        Assert.Equal(nameof(AdornerDecorator), owner);
    }

    [Fact]
    public void should_draw_the_ghost_next_to_the_cursor_when_the_cursor_is_left_of_the_source_list()
    {
        // 출발 목록 기준 x=-300(왼쪽 칸 위). 레이어는 목록 왼쪽으로 500 까지 펼쳐져 있다 — 고스트는 커서 옆에 있어야 한다.
        var rect = DragMath.ClampGhostRect(new Point(-300, 40), new Size(100, 30), new Rect(-500, -60, 1300, 800));

        Assert.Equal(-288, rect.X, 3);     // -300 + 12
        Assert.Equal(50, rect.Y, 3);       // 40 + 10
    }

    [Fact]
    public void should_pin_the_ghost_to_the_layer_edge_not_the_source_list_edge_when_it_would_overflow_left()
    {
        var rect = DragMath.ClampGhostRect(new Point(-520, 40), new Size(100, 30), new Rect(-500, -60, 1300, 800));

        Assert.Equal(-500, rect.X, 3);
    }

    [Fact]
    public void should_tell_the_handler_when_released_over_a_zone_that_refuses()
    {
        var calls = OnStage((stage, handle) =>
        {
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X - 20, stage.Pointer.Y));
            stage.MoveTo(handle, stage.Center(stage.Target));
            stage.Release(handle);
            return (string.Join(",", stage.Handler.Dropped), string.Join(",", stage.Handler.Refused));
        }, accepts: false);

        Assert.Equal("", calls.Item1);
        Assert.Equal("target", calls.Item2);
    }

    [Fact]
    public void should_stay_silent_when_released_back_over_its_own_source_zone()
    {
        // 제자리(출발 목록 자신이 드롭존)에 도로 놓는 것은 "그만두기"다 — 거절 사유를 말하지 않는다.
        var refused = OnStage((stage, handle) =>
        {
            DropZone.SetKey(stage.Viewer, "source");
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X, stage.Pointer.Y + 20));
            stage.Release(handle);
            return string.Join(",", stage.Handler.Refused);
        }, accepts: false);

        Assert.Equal("", refused);
    }

    [Fact]
    public void should_still_drop_when_the_zone_accepts()
    {
        var dropped = OnStage((stage, handle) =>
        {
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X - 20, stage.Pointer.Y));
            stage.MoveTo(handle, stage.Center(stage.Target));
            stage.Release(handle);
            return (string.Join(",", stage.Handler.Dropped), stage.DragAdorners().Count, DragSession.IsActive);
        }, accepts: true);

        Assert.Equal(("target", 0, false), dropped);
    }

    /// <summary>
    /// 왼쪽 = 놓을 칸(Border 드롭존) · 오른쪽 = ScrollViewer 안의 출발 목록. 전체를 AdornerDecorator 가 감싼다(콘솔 셸과 같은 모양).
    /// </summary>
    private static T OnStage<T>(Func<Stage, DragHandle, T> body, bool accepts = true)
        => OnSta(() =>
        {
            var handler = new Handler { Accepts = accepts };
            var items = new ObservableCollection<string> { "a", "b", "c" };
            var source = new ListBox { ItemsSource = items, SelectionMode = SelectionMode.Extended };
            source.ItemTemplate = (DataTemplate)XamlReader.Parse(
                "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                "xmlns:d='clr-namespace:Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;assembly=Ironwall.Dotnet.Libraries.Utils'>" +
                "<DockPanel Height='30'><d:DragHandle Width='16' DockPanel.Dock='Left'/><TextBlock Text='{Binding}'/></DockPanel></DataTemplate>");
            Interaction.GetBehaviors(source).Add(new CaptureDragBehavior { KeyboardFallback = "시험", Handler = handler });

            var viewer = new ScrollViewer { Content = source, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var target = new Border { Background = Brushes.WhiteSmoke };
            DropZone.SetKey(target, "target");
            DropZone.SetHandler(target, handler);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.Children.Add(target);
            Grid.SetColumn(viewer, 1);
            grid.Children.Add(viewer);

            var window = new Window
            {
                Content = new AdornerDecorator { Child = grid },
                Width = 600, Height = 300, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000,
            };
            window.Show();
            var stage = new Stage { Window = window, Source = source, Target = target, Viewer = viewer, Items = items, Handler = handler };
            DragPointer.Override = relativeTo => window.TranslatePoint(stage.Pointer, (UIElement)relativeTo);
            DragHandle? handle = null;
            try
            {
                window.Activate();
                window.UpdateLayout();
                handle = stage.HandleOf(0);
                return body(stage, handle);
            }
            finally
            {
                if (handle is { IsDragging: true }) handle.CancelDrag();
                if (handle is { IsMouseCaptured: true }) handle.ReleaseMouseCapture();
                DragPointer.Override = null;
                window.Close();
            }
        });

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static T? Find<T>(DependencyObject d) where T : DependencyObject
    {
        if (d is T hit) return hit;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            if (Find<T>(VisualTreeHelper.GetChild(d, i)) is { } found) return found;
        return null;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deep in Descendants<T>(child)) yield return deep;
        }
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
