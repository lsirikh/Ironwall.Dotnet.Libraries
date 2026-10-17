using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// 이벤트 창 타일의 PTZ 조작 헤드리스 시험(실제 <see cref="TileView"/> · 가짜 PTZ 기록).
/// <list type="bullet">
/// <item>패드 단추는 누르는 동안만 이동 — 누름 = 이동 1건, 뗌 · 캡처 잃음 · 창 비활성 · 패드 닫힘 · 타일 닫힘 · 뷰 내려감 = 정지 1건.</item>
/// <item>영상 위 드래그 = 떼는 순간 상대 이동 1건(8 DIU 미만은 클릭 · Esc/캡처 잃음은 취소 · 못 하는 타일은 이유 알림).</item>
/// </list>
/// 화면에 없는 요소는 마우스 위치를 읽지 못하므로 위치는 <see cref="TileView.PointerPosition"/> 로 넣는다.
/// </summary>
[Collection(HostStaCollection.Name)]
public class TilePtzInteractionTests
{
    private readonly HostStaThread _sta;

    public TilePtzInteractionTests(HostStaThread sta) => _sta = sta;

    private sealed class Rig
    {
        public required EventWindowViewModel Window { get; init; }
        public required TileViewModel Tile { get; init; }
        public required TileView View { get; init; }
        public required RecordingControlFactory Controls { get; init; }
        public Point Pointer;

        public IReadOnlyList<string> Calls => Controls.Simulated(Tile.CameraId).Calls;
        public int Count(string prefix) => Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static Rig NewRig(bool ptz = true, bool allowed = true)
    {
        var controls = new RecordingControlFactory();
        var msg = new OpenEventWindow { Kind = EventWindowKind.Detection, EventId = "7", GridColumns = 1, GridRows = 1 };
        msg.Cameras.Add(new EventWindowCamera
        {
            CameraId = "p1", Name = "PTZ-1", IsPtz = ptz, PtzAllowed = allowed,
            Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern },
        });
        var window = new EventWindowViewModel(msg, controls, new FakeHostClock(), _ => { }, null);
        var tile = window.CameraTiles.Single();
        tile.Video = BitmapSource.Create(320, 180, 96, 96, PixelFormats.Bgr32, null, new byte[320 * 180 * 4], 320 * 4);
        var view = new TileView { DataContext = tile };
        var rig = new Rig { Window = window, Tile = tile, View = view, Controls = controls };
        view.PointerPosition = (_, _) => rig.Pointer;
        view.Measure(new Size(320, 180));
        view.Arrange(new Rect(0, 0, 320, 180));
        view.UpdateLayout();
        return rig;
    }

    private static MouseButtonEventArgs Button(RoutedEvent routed)
        => new(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = routed };

    private static void PressPad(Button pad) => pad.RaiseEvent(Button(Mouse.PreviewMouseDownEvent));
    private static void ReleasePad(Button pad) => pad.RaiseEvent(Button(Mouse.PreviewMouseUpEvent));
    private static void LoseCapture(UIElement element) => element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });

    /// <summary>KeyEventArgs 가 요구하는 입력 원본 자리(창 · HWND 를 만들지 않는다 — HwndSource 를 만들었다 버리면 같은 스레드의 오프스크린 렌더가 비어 나온다).</summary>
    private sealed class FakePresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static void Key(UIElement target, RoutedEvent routed, Key key)
        => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, new FakePresentationSource(), 0, key) { RoutedEvent = routed });

    // ───────── 패드: 누르는 동안만 이동 ─────────

    [Fact]
    public void should_move_once_on_press_and_stop_once_on_release_when_pad_button_is_held()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();

            PressPad(rig.View.PadUp);
            Assert.Equal(new[] { "move:0,0.5,0" }, rig.Calls);
            Assert.True(rig.Tile.IsPtzMoving);

            ReleasePad(rig.View.PadUp);
            LoseCapture(rig.View.PadUp);   // 뗌 뒤에 따라오는 캡처 해제 — 정지를 또 보내지 않는다

            Assert.Equal(new[] { "move:0,0.5,0", "stop" }, rig.Calls);
            Assert.False(rig.Tile.IsPtzMoving);
        });

    [Theory]
    [InlineData("zoom-in", "move:0,0,0.5")]
    [InlineData("zoom-out", "move:0,0,-0.5")]
    [InlineData("left", "move:-0.5,0,0")]
    [InlineData("down", "move:0,-0.5,0")]
    public void should_move_on_the_pressed_axis_when_each_pad_button_is_held(string tag, string expected)
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();
            var pad = new[] { rig.View.PadUp, rig.View.PadDown, rig.View.PadLeft, rig.View.PadRight, rig.View.PadZoomIn, rig.View.PadZoomOut }
                .Single(b => (string)b.Tag == tag);

            PressPad(pad);
            ReleasePad(pad);

            Assert.Equal(new[] { expected, "stop" }, rig.Calls);
        });

    [Fact]
    public void should_stop_when_mouse_capture_is_lost_while_holding()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();
            PressPad(rig.View.PadRight);

            LoseCapture(rig.View.PadRight);

            Assert.Equal(new[] { "move:0.5,0,0", "stop" }, rig.Calls);
        });

    [Fact]
    public void should_stop_when_window_is_deactivated_while_holding()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();
            PressPad(rig.View.PadLeft);

            rig.View.OnWindowDeactivated(null, EventArgs.Empty);
            rig.View.OnWindowDeactivated(null, EventArgs.Empty);

            Assert.Equal(1, rig.Count("stop"));
        });

    [Fact]
    public void should_stop_when_pad_is_closed_while_holding()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();
            PressPad(rig.View.PadDown);

            rig.Tile.HidePad();

            Assert.Equal(new[] { "move:0,-0.5,0", "stop" }, rig.Calls);
        });

    [Fact]
    public void should_stop_exactly_once_when_tile_is_closed_while_holding()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();
            PressPad(rig.View.PadUp);

            rig.Window.CloseTile(rig.Tile);
            rig.View.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));   // 닫힌 타일의 뷰가 내려간다

            Assert.Equal(1, rig.Count("stop"));
            Assert.Equal("stop", rig.Calls[^1]);
        });

    [Fact]
    public void should_stop_when_view_is_unloaded_while_holding()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();
            PressPad(rig.View.PadUp);

            rig.View.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(new[] { "move:0,0.5,0", "stop" }, rig.Calls);
        });

    [Fact]
    public async Task should_stop_when_window_shuts_down_while_holding()
    {
        Rig? rig = null;
        _sta.Invoke(() =>
        {
            rig = NewRig();
            rig.Tile.TogglePad();
            PressPad(rig.View.PadUp);
        });

        await rig!.Window.ShutdownAsync(returnHome: false);

        Assert.Equal(1, rig.Count("stop"));
    }

    [Fact]
    public void should_not_send_stop_when_nothing_is_held()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();

            ReleasePad(rig.View.PadUp);
            LoseCapture(rig.View.PadUp);
            rig.View.OnWindowDeactivated(null, EventArgs.Empty);
            rig.View.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Empty(rig.Calls);
        });

    [Fact]
    public void should_always_send_stop_when_stop_button_is_clicked()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();

            rig.View.PadStop.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.Equal(new[] { "stop" }, rig.Calls);   // ■ 는 눌린 게 없어도 정지
        });

    [Fact]
    public void should_move_on_key_down_and_stop_on_key_up_when_pad_button_has_focus()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();

            Key(rig.View.PadRight, Keyboard.PreviewKeyDownEvent, System.Windows.Input.Key.Space);
            Key(rig.View.PadRight, Keyboard.PreviewKeyDownEvent, System.Windows.Input.Key.Space);   // 누르고 있는 동안 다시 오는 KeyDown
            Assert.Equal(new[] { "move:0.5,0,0" }, rig.Calls);

            Key(rig.View.PadRight, Keyboard.PreviewKeyUpEvent, System.Windows.Input.Key.Space);

            Assert.Equal(new[] { "move:0.5,0,0", "stop" }, rig.Calls);
        });

    [Fact]
    public void should_move_with_arrow_key_and_stop_on_key_up_when_pad_is_open()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();

            Key(rig.View, Keyboard.PreviewKeyDownEvent, System.Windows.Input.Key.Left);
            Key(rig.View, Keyboard.PreviewKeyDownEvent, System.Windows.Input.Key.Left);
            Key(rig.View, Keyboard.PreviewKeyUpEvent, System.Windows.Input.Key.Left);

            Assert.Equal(new[] { "move:-0.5,0,0", "stop" }, rig.Calls);
        });

    [Fact]
    public void should_not_move_when_tile_has_no_ptz_permission()
        => _sta.Invoke(() =>
        {
            var rig = NewRig(allowed: false);

            PressPad(rig.View.PadUp);
            ReleasePad(rig.View.PadUp);

            Assert.Empty(rig.Calls);
        });

    // ───────── 영상 위 드래그 ─────────

    private static void DragStart(Rig rig, double x, double y)
    {
        rig.Pointer = new Point(x, y);
        rig.View.VideoImage.RaiseEvent(Button(Mouse.MouseDownEvent));
    }

    private static void DragTo(Rig rig, double x, double y)
    {
        rig.Pointer = new Point(x, y);
        rig.View.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
    }

    private static void DragEnd(Rig rig) => rig.View.RaiseEvent(Button(Mouse.MouseUpEvent));

    [Fact]
    public void should_send_one_relative_move_on_release_when_video_is_dragged()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();

            DragStart(rig, 100, 90);
            DragTo(rig, 140, 90);
            Assert.Equal(Visibility.Visible, rig.View.PtzDragOverlay.Visibility);   // 끄는 동안 목표 표시
            Assert.Empty(rig.Calls);                                               // 끄는 동안에는 보내지 않는다
            DragTo(rig, 180, 72);
            DragEnd(rig);

            Assert.Equal(new[] { "drag:0.25,-0.1" }, rig.Calls);   // 80/320 · −18/180
            Assert.Equal(Visibility.Collapsed, rig.View.PtzDragOverlay.Visibility);
        });

    [Fact]
    public void should_treat_as_click_when_drag_is_inside_dead_zone()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();

            DragStart(rig, 100, 90);
            DragTo(rig, 105, 94);
            DragEnd(rig);

            Assert.Empty(rig.Calls);
            Assert.Equal(Visibility.Collapsed, rig.View.PtzDragOverlay.Visibility);
        });

    [Fact]
    public void should_cancel_drag_when_escape_is_pressed()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            DragStart(rig, 100, 90);
            DragTo(rig, 200, 90);

            Key(rig.View, Keyboard.PreviewKeyDownEvent, System.Windows.Input.Key.Escape);
            DragEnd(rig);

            Assert.Empty(rig.Calls);
            Assert.Equal(Visibility.Collapsed, rig.View.PtzDragOverlay.Visibility);
        });

    [Fact]
    public void should_cancel_drag_when_mouse_capture_is_lost()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            DragStart(rig, 100, 90);
            DragTo(rig, 200, 90);

            LoseCapture(rig.View);
            DragEnd(rig);

            Assert.Empty(rig.Calls);
        });

    [Fact]
    public void should_show_reason_and_send_nothing_when_fixed_camera_video_is_dragged()
        => _sta.Invoke(() =>
        {
            var rig = NewRig(ptz: false);

            DragStart(rig, 100, 90);
            DragTo(rig, 200, 90);
            Assert.Equal(Visibility.Collapsed, rig.View.PtzDragOverlay.Visibility);   // 못 하는 타일은 목표 표시도 없다
            DragEnd(rig);

            Assert.Empty(rig.Calls);
            Assert.Equal(PtzAvailability.FixedCameraReason, rig.Tile.Notice);
        });

    [Fact]
    public void should_show_permission_reason_when_video_is_dragged_without_ptz_permission()
        => _sta.Invoke(() =>
        {
            var rig = NewRig(allowed: false);

            DragStart(rig, 100, 90);
            DragTo(rig, 200, 90);
            DragEnd(rig);

            Assert.Empty(rig.Calls);
            Assert.Equal(PtzAvailability.NoPermissionReason, rig.Tile.Notice);
        });

    // ───────── 영상 위 휠 줌(지도 팝업과 같은 규칙) ─────────

    private static void Wheel(UIElement target, int delta)
        => target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = Mouse.MouseWheelEvent });

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int ms = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return condition();
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-140")]
    public async Task should_zoom_then_stop_after_pulse_when_wheel_turns_over_video()
    {
        Rig? rig = null;
        _sta.Invoke(() =>
        {
            rig = NewRig();
            rig.Tile.WheelZoomPulse = TimeSpan.FromMilliseconds(60);
            Wheel(rig.View.VideoImage, 120);
        });

        Assert.True(await WaitUntilAsync(() => rig!.Calls.Contains("stop")), string.Join(" ", rig!.Calls));

        Assert.Equal(new[] { "move:0,0,0.5", "stop" }, rig.Calls);   // 줌 연속 이동 0.5(카메라 기본 속도 모름) → 펄스 뒤 정지
        _sta.Invoke(() => Assert.False(rig.Tile.IsPtzMoving));
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-141")]
    public async Task should_cancel_earlier_stop_and_stop_once_when_wheel_turns_repeatedly()
    {
        Rig? rig = null;
        _sta.Invoke(() =>
        {
            rig = NewRig();
            rig.Tile.WheelZoomPulse = TimeSpan.FromMilliseconds(250);
            Wheel(rig.View.VideoImage, 120);
            Wheel(rig.View.VideoImage, 120);
            Wheel(rig.View.VideoImage, -120);
        });

        Assert.True(await WaitUntilAsync(() => rig!.Calls.Contains("stop")), string.Join(" ", rig!.Calls));
        await Task.Delay(400);

        Assert.Equal(new[] { "move:0,0,0.5", "move:0,0,0.5", "move:0,0,-0.5", "stop" }, rig.Calls);   // 앞 칸의 정지는 취소 — 마지막 칸 뒤 한 번
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-142")]
    public void should_show_reason_and_send_nothing_when_wheel_turns_over_fixed_camera()
        => _sta.Invoke(() =>
        {
            var rig = NewRig(ptz: false);

            Wheel(rig.View.VideoImage, 120);

            Assert.Empty(rig.Calls);
            Assert.Equal(PtzAvailability.FixedCameraReason, rig.Tile.Notice);
        });

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-143")]
    public void should_let_wheel_pass_when_it_is_outside_video()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();
            rig.Tile.TogglePad();
            var onPad = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.MouseWheelEvent };
            var onHandle = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.MouseWheelEvent };

            rig.View.PadUp.RaiseEvent(onPad);
            rig.View.DragHandle.RaiseEvent(onHandle);

            Assert.Empty(rig.Calls);
            Assert.False(onPad.Handled);       // 흘려보낸다(처리하지 않음)
            Assert.False(onHandle.Handled);
        });

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-144")]
    public async Task should_show_moving_then_done_then_hide_when_video_is_dragged()
    {
        Rig? rig = null;
        _sta.Invoke(() =>
        {
            rig = NewRig();
            DragStart(rig, 100, 90);
            DragTo(rig, 180, 72);
            DragEnd(rig);
            Assert.True(rig.Tile.IsPtzMotionMoving);
            Assert.Equal("이동 중", rig.Tile.PtzMotionText);
            Assert.Equal(Visibility.Visible, rig.View.PtzMotionChip.Visibility);
        });

        // 시험 무늬 가짜는 MoveStatus 를 모른다 → 끌기 어림 시간(1.5초) 뒤 '이동 완료' → 잠깐 뒤 사라짐
        bool done = false, hidden = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 6000 && !hidden)
        {
            await Task.Delay(50);
            _sta.Invoke(() =>
            {
                done |= rig!.Tile.IsPtzMotionDone && rig.Tile.PtzMotionText == "이동 완료";
                hidden = done && !rig.Tile.IsPtzMotionVisible;
            });
        }

        Assert.True(done, "'이동 완료' 가 보이지 않았다");
        Assert.True(hidden, "'이동 완료' 뒤 사라지지 않았다");
    }

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-145")]
    public void should_show_arrow_amount_in_screens_while_dragging()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();

            DragStart(rig, 100, 90);
            DragTo(rig, 260, 90);   // 160 px = 영상 320 의 1/2 = 화면 반

            Assert.Equal("0.5화면", rig.View.DragAmountText.Text);
            DragTo(rig, 196, 90);   // 96 px = 320 의 0.3
            Assert.Equal("0.3화면", rig.View.DragAmountText.Text);
            DragTo(rig, 100, 180);  // 아래로 90 px = 높이 180 의 1/2(세로는 높이 기준)
            Assert.Equal("0.5화면", rig.View.DragAmountText.Text);
            DragEnd(rig);
        });

    [Fact]
    [Trait("Scenario", "PTZ-DRAG-146")]
    public void should_draw_arrow_from_press_point_to_pointer_and_send_nothing_until_release()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();

            DragStart(rig, 100, 90);
            DragTo(rig, 260, 90);

            // 형태: 몸통(시작점 → 촉 안쪽) + 끝점에 화살촉(오른쪽을 가리킴). 옛 과녁(원 + 십자)은 없다.
            Assert.Equal(Visibility.Visible, rig.View.PtzDragOverlay.Visibility);
            Assert.True(rig.View.PtzDragOverlay.Opacity < 1, "화살표는 반투명이어야 한다");
            Assert.Equal((100d, 90d), (rig.View.DragShaft.X1, rig.View.DragShaft.Y1));
            Assert.True(rig.View.DragShaft.X2 > 100 && rig.View.DragShaft.X2 < 260, $"몸통 끝 {rig.View.DragShaft.X2}");
            Assert.Equal(90d, rig.View.DragShaft.Y2, 6);
            Assert.Empty(rig.View.DragTarget.Children.OfType<System.Windows.Shapes.Ellipse>());
            var rotate = Assert.IsType<RotateTransform>(rig.View.DragTarget.RenderTransform);
            Assert.Equal(0d, rotate.Angle, 6);
            var tip = rig.View.DragTarget.RenderTransform.Transform(new Point(rotate.CenterX, rotate.CenterY));
            Assert.Equal(260d, Canvas.GetLeft(rig.View.DragTarget) + tip.X, 6);   // 촉 끝 = 지금 마우스 자리
            Assert.Equal(90d, Canvas.GetTop(rig.View.DragTarget) + tip.Y, 6);

            DragTo(rig, 100, 170);   // 아래로 — 촉이 돈다
            Assert.Equal(90d, rotate.Angle, 6);
            Assert.Empty(rig.Calls);   // 누르고 있는 동안 카메라는 움직이지 않는다

            DragEnd(rig);
            Assert.Equal(new[] { "drag:0,0.444" }, rig.Calls);   // 80/180 — 놓는 순간 한 건
            Assert.Equal(Visibility.Collapsed, rig.View.PtzDragOverlay.Visibility);
        });

    [Fact]
    public void should_not_start_ptz_drag_when_press_begins_on_reorder_handle()
        => _sta.Invoke(() =>
        {
            var rig = NewRig();

            rig.Pointer = new Point(10, 10);
            rig.View.DragHandle.RaiseEvent(Button(Mouse.MouseDownEvent));   // 손잡이 = 순서 바꾸기(창 뷰 담당)
            DragTo(rig, 200, 90);
            DragEnd(rig);

            Assert.Empty(rig.Calls);
        });
}
