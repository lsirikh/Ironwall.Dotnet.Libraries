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
        public List<int> DroppedIndexes { get; } = new();
        public List<string> Refused { get; } = new();
        public bool CanDrop(DragPayload payload, DropTarget target) => Accepts && target.ZoneKey == "target";
        public void Drop(DragPayload payload, DropTarget target) { Dropped.Add(target.ZoneKey); DroppedIndexes.Add(target.InsertionIndex); }
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

        /// <summary>Thumb 을 거치지 않은 시작(미리보기 · 갤러리 재현과 같은 길) — 손잡이는 끌기 · 캡처를 쥐지 않는다.</summary>
        public void StartWithoutThumb(DragHandle handle)
        {
            Pointer = Center(handle);
            handle.RaiseEvent(new DragStartedEventArgs(0, 0));
        }

        public void Escape()
            => Window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(Window), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

        public void Reload()
        {
            var fresh = Items.ToList();
            Items.Clear();
            foreach (var item in fresh) Items.Add(item);
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

            stage.Reload();
            Pump();                                     // 캡처 재평가 · 레이아웃은 디스패처가 돈다(실앱과 같다)

            return (during, stage.DragAdorners().Count, DragSession.IsActive, stage.Window.ReadLocalValue(FrameworkElement.CursorProperty) == DependencyProperty.UnsetValue, stage.Handler.Dropped.Count);
        });

        Assert.Equal((1, true), after.during);          // 전제 — 실제로 끌고 있었다(고스트 1)
        Assert.Equal(0, after.Item5);                   // 받는 칸 위였어도 사라진 끌기는 드롭이 아니다(취소)
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
            var under = DragHitTest.ZoneFrom(DragHitTest.Top(stage.Window, stage.Pointer));
            stage.Release(handle);
            return (ReferenceEquals(under, stage.Viewer), string.Join(",", stage.Handler.Refused));
        }, accepts: false);

        Assert.True(refused.Item1, "전제 — 놓는 자리가 출발 목록을 품은 드롭존(source) 위여야 한다");
        Assert.Equal("", refused.Item2);
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

    #region - 스스로 끝내는 길(적대 검토 79de32d3) -
    [Fact]
    public void should_finish_on_the_auto_scroll_tick_when_the_held_row_left_the_list_without_any_completion()
    {
        // 손잡이가 끌기를 쥐지 않은 시작(미리보기 재현과 같은 길)이라 행이 빠져도 DragCompleted 가 오지 않는다 —
        // 목록 가장자리에서 도는 오토스크롤 틱이 "잡은 행이 목록을 떠났다" 를 보고 스스로 끝내야 한다.
        var after = OnStage((stage, handle) =>
        {
            DropZone.SetKey(stage.Source, "source-list");
            DropZone.SetIsReorder(stage.Source, true);
            stage.StartWithoutThumb(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X, stage.Pointer.Y + 20));
            var bottom = stage.Source.TranslatePoint(new Point(stage.Source.ActualWidth / 2, stage.Source.ActualHeight - 4), stage.Window);
            stage.MoveTo(handle, bottom);                // 아래 가장자리 띠 — 틱이 돈다
            var during = stage.DragAdorners().Count;

            stage.Reload();                              // 끄는 도중 다시 그려짐 — 델타는 더 오지 않는다
            Wait(TimeSpan.FromMilliseconds(250));        // 오토스크롤 틱(30ms)이 몇 번 돌 시간

            return (during, stage.DragAdorners().Count, DragSession.IsActive, stage.Handler.Dropped.Count);
        });

        Assert.True(after.during > 0, "전제 — 끌고 있었다");
        Assert.Equal((0, false, 0), (after.Item2, after.Item3, after.Item4));
    }

    [Fact]
    public void should_finish_on_escape_even_when_the_thumb_no_longer_holds_the_drag()
    {
        var after = OnStage((stage, handle) =>
        {
            stage.StartWithoutThumb(handle);            // Thumb 은 끌기 상태가 아니다 → CancelDrag 는 아무것도 내지 않는다
            stage.MoveTo(handle, new Point(stage.Pointer.X - 20, stage.Pointer.Y));
            stage.MoveTo(handle, stage.Center(stage.Target));
            var during = stage.DragAdorners().Count;
            stage.Escape();
            return (during, stage.DragAdorners().Count, DragSession.IsActive, stage.Handler.Dropped.Count);
        }, accepts: true);

        Assert.True(after.during > 0, "전제 — 끌고 있었다");
        Assert.Equal((0, false, 0), (after.Item2, after.Item3, after.Item4));
    }

    [Fact]
    public void should_finish_once_and_commit_nothing_when_escape_cancels_a_real_thumb_drag()
    {
        // Esc → CancelDrag 가 안쪽에서 DragCompleted(취소)를 두 번 내고, 그 뒤 Esc 처리기가 직접 끝내기를 한 번 더 시도한다.
        var after = OnStage((stage, handle) =>
        {
            var finishes = 0;
            handle.DragCompleted += (_, _) => finishes++;
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X - 20, stage.Pointer.Y));
            stage.MoveTo(handle, stage.Center(stage.Target));
            stage.Escape();
            stage.Release(handle);                      // 뒤늦은 뗌 — 이미 끝났으니 아무 일도 없어야 한다
            return (finishes, stage.DragAdorners().Count, DragSession.IsActive, stage.Handler.Dropped.Count + stage.Handler.Refused.Count, handle.IsDragging, Mouse.Captured == null);
        }, accepts: true);

        // 전제 — WPF Thumb.CancelDrag 는 캡처를 풀며 안쪽에서 CancelDrag 를 다시 불러 DragCompleted 를 두 번 낸다.
        Assert.Equal(2, after.finishes);
        // 그래도 끝내기는 한 번 — 드롭 · 거절 통지 0, 고스트 · 세션 · 끌기 · 캡처 전부 풀림.
        Assert.Equal((0, false, 0, false, true), (after.Item2, after.Item3, after.Item4, after.Item5, after.Item6));
    }

    [Fact]
    public void should_commit_once_when_the_completion_arrives_twice()
    {
        var dropped = OnStage((stage, handle) =>
        {
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X - 20, stage.Pointer.Y));
            stage.MoveTo(handle, stage.Center(stage.Target));
            stage.Release(handle);
            handle.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
            return stage.Handler.Dropped.Count;
        }, accepts: true);

        Assert.Equal(1, dropped);
    }

    [Fact]
    public void should_release_the_thumb_capture_when_the_drag_finishes_on_its_own()
    {
        // 분리(창이 행동을 떼어 냄)로 스스로 끝낸다 — 손잡이가 끌기 · 캡처를 계속 쥐면 다음 클릭을 먹는다(규칙 ④).
        var after = OnStage((stage, handle) =>
        {
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X - 20, stage.Pointer.Y));
            stage.MoveTo(handle, stage.Center(stage.Target));
            var held = (handle.IsDragging, handle.IsMouseCaptured);
            Interaction.GetBehaviors(stage.Source).Clear();
            return (held, handle.IsDragging, Mouse.Captured == null, stage.DragAdorners().Count, DragSession.IsActive, stage.Handler.Dropped.Count);
        }, accepts: true);

        Assert.Equal((true, true), after.held);          // 전제 — Thumb 이 실제로 끌기 · 캡처를 쥐고 있었다
        Assert.False(after.Item2);
        Assert.True(after.Item3);
        Assert.Equal((0, false, 0), (after.Item4, after.Item5, after.Item6));
    }

    [Fact]
    public void should_clamp_the_insertion_index_to_the_current_item_count_when_the_list_shrank_before_the_drop()
    {
        // 맨 끝(3) 에 놓으려는 사이 목록이 줄었다(재조회) — 담당이 받는 인덱스는 지금 항목 수를 넘지 않는다.
        var indexes = OnStage((stage, handle) =>
        {
            DropZone.SetKey(stage.Source, "target");
            DropZone.SetIsReorder(stage.Source, true);
            DropZone.SetHandler(stage.Source, stage.Handler);
            stage.Press(handle);
            stage.MoveTo(handle, new Point(stage.Pointer.X, stage.Pointer.Y + 20));
            var last = (FrameworkElement)stage.Source.ItemContainerGenerator.ContainerFromIndex(2);
            stage.MoveTo(handle, last.TranslatePoint(new Point(last.ActualWidth / 2, last.ActualHeight * 0.9), stage.Window));
            stage.Items.RemoveAt(2);
            stage.Items.RemoveAt(1);                    // 잡은 행(0)은 그대로 — 그 뒤 포인터는 움직이지 않았다
            stage.Window.UpdateLayout();
            stage.Release(handle);
            return (stage.Items.Count, string.Join(",", stage.Handler.DroppedIndexes));
        }, accepts: true);

        Assert.Equal((1, "1"), indexes);
    }

    [Fact]
    public void should_measure_the_ghost_bounds_through_a_scale_transform()
    {
        // 레이어(600×300) 안, (100,50) 에 두 배로 키운 요소 — 요소 좌표로는 레이어가 (-50,-25) 에서 300×150 이다.
        var bounds = OnSta(() =>
        {
            var adorned = new Border { Width = 40, Height = 20, RenderTransform = new ScaleTransform(2, 2), Background = Brushes.Gray };
            Canvas.SetLeft(adorned, 100);
            Canvas.SetTop(adorned, 50);
            var canvas = new Canvas { Width = 600, Height = 300 };
            canvas.Children.Add(adorned);
            var window = new Window
            {
                Content = new AdornerDecorator { Child = canvas }, SizeToContent = SizeToContent.WidthAndHeight,
                ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000,
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var layer = AdornerLayer.GetAdornerLayer(adorned)!;
                var ghost = new DragGhostAdorner(adorned, layer, "끄는 행", 1);
                return (ghost.AvailableBounds(), layer.RenderSize);
            }
            finally { window.Close(); }
        });

        Assert.Equal(new Size(600, 300), bounds.RenderSize);   // 전제 — 레이어 크기
        Assert.Equal(-50, bounds.Item1.X, 3);
        Assert.Equal(-25, bounds.Item1.Y, 3);
        Assert.Equal(300, bounds.Item1.Width, 3);
        Assert.Equal(150, bounds.Item1.Height, 3);
    }
    #endregion

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

    private static void Wait(TimeSpan span)
    {
        // 타이머로 도는 오토스크롤을 기다린다 — 디스패처를 돌리며(스레드를 재우지 않는다).
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = span };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

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
