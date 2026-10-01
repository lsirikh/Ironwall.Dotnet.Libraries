using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 셸 안 카드의 자리 — 가운데에 뜨고, 머리를 끌면 옮겨지되 셸 밖으로 나가지 않는다(2026-10-01 "로그아웃창이 구석에 처박혀 있고 창 이동이 안 된다").
/// </summary>
/// <remarks>
/// 자리 계산은 순수 함수(<see cref="DialogPlacementRules"/>)라 창 없이 본다. 끌기는 화면 밖 창에 틀을 실제로 세우고
/// 손잡이(<see cref="ConsoleDialogFrame.PartMove"/>)에 Thumb 사건을 올리며 포인터 자리만 <see cref="DragPointer.Override"/> 로 알려 준다
/// (UIA 에 드래그 패턴이 없다 — 판정은 이 길과 키보드 길로 단언한다).
/// </remarks>
[Collection(WpfFocusCollection.Name)]
public class DialogPlacementTests
{
    private static readonly Size Card = new(400, 200);
    private static readonly Size Host = new(1000, 700);

    // ─────────────── 순수 함수 ───────────────

    [Fact]
    public void should_center_the_card_in_the_host_when_it_has_not_moved()
    {
        var origin = DialogPlacementRules.Origin(new Vector(), Card, Host);

        Assert.Equal(new Point(300, 250), origin);
    }

    [Theory]
    [InlineData(1000, 1000, 300, 250)]       // 오른쪽 아래 끝까지 — 카드 전체가 남는다
    [InlineData(-1000, -1000, -300, -250)]   // 왼쪽 위 끝까지
    [InlineData(120, -40, 120, -40)]         // 안쪽은 그대로
    public void should_keep_the_whole_card_inside_the_host_when_it_is_dragged_far(double dx, double dy, double x, double y)
    {
        var offset = DialogPlacementRules.Drag(new Vector(), dx, dy, Card, Host);
        var origin = DialogPlacementRules.Origin(offset, Card, Host);

        Assert.Equal(new Vector(x, y), offset);
        Assert.InRange(origin.X, 0, Host.Width - Card.Width);
        Assert.InRange(origin.Y, 0, Host.Height - Card.Height);
    }

    [Fact]
    public void should_stay_centered_when_the_card_is_larger_than_the_host()
    {
        var offset = DialogPlacementRules.Drag(new Vector(), 200, 200, new Size(800, 900), new Size(600, 500));

        Assert.Equal(new Vector(), offset);   // 옮길 자리가 없다 — 배치가 정한 가운데 그대로
    }

    [Fact]
    public void should_pull_a_moved_card_back_inside_when_the_host_shrinks()
    {
        var moved = DialogPlacementRules.Drag(new Vector(), 290, 240, Card, Host);   // 거의 오른쪽 아래 끝
        var reclamped = DialogPlacementRules.Clamp(moved, Card, new Size(600, 400));  // 셸이 줄었다

        Assert.Equal(new Vector(100, 100), reclamped);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void should_fall_back_to_the_center_when_a_value_is_unusable(double value, double expected)
    {
        Assert.Equal(new Vector(expected, expected), DialogPlacementRules.Clamp(new Vector(value, value), Card, Host));
        Assert.Equal(new Vector(), DialogPlacementRules.Clamp(new Vector(50, 50), Card, new Size(double.NaN, double.NaN)));
        Assert.Equal(new Vector(), DialogPlacementRules.Drag(new Vector(), value, value, Card, Host));
    }

    [Theory]
    [InlineData(1, 0, false, 1, 0)]
    [InlineData(0, -1, true, 0, -10)]
    public void should_move_by_one_step_when_an_arrow_is_pressed_on_the_handle(int dirX, int dirY, bool coarse, double x, double y)
        => Assert.Equal(new Vector(x, y), DialogPlacementRules.KeyboardMove(new Vector(), dirX, dirY, coarse, Card, Host));

    [Fact]
    public void should_stop_at_the_edge_when_arrows_push_past_it()
        => Assert.Equal(new Vector(300, 0), DialogPlacementRules.KeyboardMove(new Vector(300, 0), 1, 0, coarse: true, Card, Host));

    // ─────────────── 실제 틀(화면 밖 창) ───────────────

    [Fact]
    public void should_not_move_the_card_when_the_pointer_stays_inside_the_dead_zone()
    {
        var offset = OnSta(() => WithFrame((window, frame, move, pointer) =>
        {
            Press(move, pointer, new Point(500, 120));
            Drag(move, pointer, new Point(505, 124));   // √41 ≈ 6.4 < 8
            Release(move);
            return frame.Offset;
        }));

        Assert.Equal(new Vector(), offset);
    }

    [Fact]
    public void should_follow_the_pointer_from_the_press_when_the_header_is_dragged()
    {
        var result = OnSta(() => WithFrame((window, frame, move, pointer) =>
        {
            var centered = CardOrigin(frame);
            Press(move, pointer, new Point(500, 120));
            Drag(move, pointer, new Point(530, 120));
            Drag(move, pointer, new Point(620, 60));     // 누적이 아니라 누른 자리 기준 — 증분이면 두 번 더해진다
            Release(move);
            return (frame.Offset, centered, moved: CardOrigin(frame));
        }));

        Assert.Equal(new Point(300, result.centered.Y), result.centered);   // 처음엔 가로 가운데(1000 - 400) / 2
        Assert.Equal(new Vector(120, -60), result.Offset);
        Assert.Equal(new Vector(120, -60), result.moved - result.centered); // 화면에서도 그만큼 비켰다(RenderTransform)
    }

    [Fact]
    public void should_keep_the_card_inside_the_shell_when_it_is_dragged_past_the_edge()
    {
        var result = OnSta(() => WithFrame((window, frame, move, pointer) =>
        {
            Press(move, pointer, new Point(500, 120));
            Drag(move, pointer, new Point(-2000, -2000));
            var topLeft = CardOrigin(frame);
            Drag(move, pointer, new Point(5000, 5000));
            var bottomRight = CardOrigin(frame);
            Release(move);
            var card = Part<FrameworkElement>(frame, ConsoleDialogFrame.PartCard)!;
            return (topLeft, bottomRight, Size: new Size(Math.Round(card.ActualWidth), Math.Round(card.ActualHeight)));
        }));

        Assert.Equal(new Point(0, 0), result.topLeft);   // 왼쪽 위 끝 — 셸 밖으로 한 점도 나가지 않는다
        Assert.Equal(new Point(Host.Width - result.Size.Width, Host.Height - result.Size.Height), result.bottomRight);
    }

    [Fact]
    public void should_put_the_card_back_and_keep_the_dialog_open_when_esc_is_pressed_during_a_drag()
    {
        var result = OnSta(() => WithFrame((window, frame, move, pointer) =>
        {
            var cancels = 0;
            frame.SecondaryInvoked += (_, _) => cancels++;
            Press(move, pointer, new Point(500, 120));
            Drag(move, pointer, new Point(600, 200));
            var during = frame.Offset;
            Key(frame, System.Windows.Input.Key.Escape);
            return (during, after: frame.Offset, cancels);
        }));

        Assert.NotEqual(new Vector(), result.during);
        Assert.Equal(new Vector(), result.after);   // 누른 자리로
        Assert.Equal(0, result.cancels);            // 끄는 중의 ESC 는 창을 닫지 않는다
    }

    [Fact]
    public void should_still_cancel_the_dialog_once_when_esc_is_pressed_without_a_drag()
    {
        var cancels = OnSta(() => WithFrame((window, frame, move, pointer) =>
        {
            var count = 0;
            frame.SecondaryInvoked += (_, _) => count++;
            Key(frame, System.Windows.Input.Key.Escape);
            return count;
        }));

        Assert.Equal(1, cancels);
    }

    [Fact]
    public void should_move_with_arrows_when_the_handle_has_keyboard_focus()
    {
        var result = OnSta(() => WithFrame((window, frame, move, pointer) =>
        {
            window.Activate();
            move.Focus();
            Keyboard.Focus(move);
            var focused = move.IsKeyboardFocused;
            Key(frame, System.Windows.Input.Key.Right);
            Key(frame, System.Windows.Input.Key.Down);
            return (focused, frame.Offset);
        }));

        Assert.True(result.focused, "손잡이가 초점을 받아야 화살표가 닿는다");
        Assert.Equal(new Vector(DialogPlacementRules.KeyboardStep, DialogPlacementRules.KeyboardStep), result.Offset);
    }

    [Fact]
    public void should_open_centered_again_when_the_dialog_is_shown_again()
    {
        var offset = OnSta(() => WithFrame((window, frame, move, pointer) =>
        {
            Press(move, pointer, new Point(500, 120));
            Drag(move, pointer, new Point(600, 200));
            Release(move);

            // 호스트 층이 같은 뷰를 내렸다가 다시 올린다(Caliburn 뷰 캐시)
            var host = (Grid)window.Content;
            host.Children.Remove(frame);
            Pump(DispatcherPriority.ContextIdle);
            host.Children.Add(frame);
            Pump(DispatcherPriority.ContextIdle);
            return frame.Offset;
        }));

        Assert.Equal(new Vector(), offset);
    }

    [Fact]
    public void should_expose_the_move_handle_to_automation_with_its_own_id_when_the_card_is_in_the_shell()
    {
        var result = OnSta(() => WithFrame((window, frame, move, pointer) =>
            (Id: AutomationProperties.GetAutomationId(move), move.IsVisible, Close: AutomationProperties.GetAutomationId(Part<Button>(frame, ConsoleDialogFrame.PartClose)!))));

        Assert.Equal("Dialog.Test.Move.Move", result.Id);
        Assert.True(result.IsVisible);
        Assert.Equal("Dialog.Test.Move.Close", result.Close);   // 기존 식별자는 그대로
    }

    [Fact]
    public void should_hide_the_move_handle_when_the_frame_is_the_root_of_a_dressed_window()
    {
        var visible = OnSta(() =>
        {
            var frame = NewFrame();
            var window = new Window
            {
                Title = "창",
                Content = new UserControl { Content = frame },
                Width = DialogSizeRules.WindowWidth(DialogSize.Small),
                Height = 360,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false,
                ShowActivated = false,
            };
            window.Show();
            Pump(DispatcherPriority.ApplicationIdle);
            Pump(DispatcherPriority.ContextIdle);
            var shown = (frame.IsWindowRoot, Part<Thumb>(frame, ConsoleDialogFrame.PartMove)?.IsVisible);
            window.Close();
            return shown;
        });

        Assert.True(visible.IsWindowRoot);
        Assert.False(visible.Item2);   // OS 제목 줄이 옮긴다
    }

    #region - Fixtures -
    private sealed class PointerBox { public Point Value; }

    private static T WithFrame<T>(Func<Window, ConsoleDialogFrame, Thumb, PointerBox, T> body)
    {
        var frame = NewFrame();
        var window = new Window
        {
            Content = new Grid { Width = Host.Width, Height = Host.Height, Children = { frame } },
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        var pointer = new PointerBox();
        DragPointer.Override = relativeTo => frame.TranslatePoint(pointer.Value, (UIElement)relativeTo);
        try
        {
            window.Show();
            Pump(DispatcherPriority.ApplicationIdle);
            Pump(DispatcherPriority.ContextIdle);
            var move = Part<Thumb>(frame, ConsoleDialogFrame.PartMove) ?? throw new InvalidOperationException("옮기기 손잡이가 없다");
            return body(window, frame, move, pointer);
        }
        finally
        {
            DragPointer.Override = null;
            window.Close();
        }
    }

    private static ConsoleDialogFrame NewFrame() => new()
    {
        Title = "로그아웃",
        Size = DialogSize.Small,
        DialogKey = "Test.Move",
        PrimaryText = "로그아웃",
        SecondaryText = "취소",
        Content = new Border { Height = 102 },   // 머리 + 몸통 + 버튼 줄 ≈ 200
    };

    private static void Press(Thumb move, PointerBox pointer, Point at)
    {
        pointer.Value = at;
        move.RaiseEvent(new DragStartedEventArgs(0, 0));
    }

    private static void Drag(Thumb move, PointerBox pointer, Point to)
    {
        pointer.Value = to;
        move.RaiseEvent(new DragDeltaEventArgs(0, 0));
    }

    private static void Release(Thumb move) => move.RaiseEvent(new DragCompletedEventArgs(0, 0, false));

    private static void Key(FrameworkElement target, Key key)
        => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });

    /// <summary>카드 왼쪽 위 — 틀 좌표, RenderTransform 포함(눈에 보이는 자리).</summary>
    private static Point CardOrigin(ConsoleDialogFrame frame)
    {
        var card = Part<FrameworkElement>(frame, ConsoleDialogFrame.PartCard)!;
        var p = card.TranslatePoint(new Point(0, 0), frame);
        return new Point(Math.Round(p.X), Math.Round(p.Y));
    }

    private static T? Part<T>(ConsoleDialogFrame frame, string name) where T : class => frame.Template?.FindName(name, frame) as T;

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
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("STA body failed", failure);
        return result;
    }
    #endregion
}
