using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : 지도 오버레이 팝업의 PTZ 제스처 헤드리스 시험 — 영상 위 드래그(떼는 순간 1건) · 누르는 동안만 이동(뗌 = 정지 1건)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>드래그 인자 → 호스트로 보낼 비율 · PTZ 못 하는 이유 · 호스트 재시작 때 빚진 정지(순수 · VM).</summary>
public class CameraPopupPtzRequestTests
{
    private static readonly VideoProviderInfo Provider = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80 };

    [Fact]
    public void should_convert_pixels_to_view_fraction_when_drag_is_released()
    {
        var e = new PtzDragEventArgs(96, -25.8, 384, 258);

        Assert.True(e.TryGetViewFraction(out var x, out var y, out var aspect));

        Assert.Equal(0.25, x, 6);
        Assert.Equal(-0.1, y, 6);
        Assert.Equal(384d / 258d, aspect, 6);
    }

    [Fact]
    public void should_clamp_fraction_to_one_view_when_drag_leaves_the_box()
    {
        var e = new PtzDragEventArgs(900, -900, 384, 258);

        Assert.True(e.TryGetViewFraction(out var x, out var y, out _));

        Assert.Equal((1d, -1d), (x, y));
    }

    [Theory]
    [InlineData(10, 5, 0, 258)]
    [InlineData(10, 5, 384, 0)]
    [InlineData(double.NaN, 5, 384, 258)]
    [InlineData(0, 0, 384, 258)]
    public void should_refuse_fraction_when_box_is_empty_or_delta_is_unusable(double dx, double dy, double w, double h)
        => Assert.False(new PtzDragEventArgs(dx, dy, w, h).TryGetViewFraction(out _, out _, out _));

    [Theory]
    [InlineData(false, true, true, CameraStreamPopupViewModel.PtzReasonConnectFailed)]
    [InlineData(true, false, true, CameraStreamPopupViewModel.PtzReasonNotSupported)]
    [InlineData(true, true, false, CameraStreamPopupViewModel.PtzReasonNoPermission)]
    [InlineData(true, false, false, CameraStreamPopupViewModel.PtzReasonNotSupported)]
    [InlineData(true, true, true, null)]
    public void should_give_ptz_unavailable_reason_in_priority_order(bool connected, bool capable, bool permitted, string? expected)
        => Assert.Equal(expected, CameraStreamPopupViewModel.PtzReason(connected, capable, permitted));

    [Fact]
    public void should_send_owed_stop_once_when_host_returns_after_dying_while_button_was_held()
    {
        // Arrange
        var host = new OverlayTestHost();
        var vm = new CameraStreamPopupViewModel(7, "정문", new PointLatLng(37, 127), host, Provider);
        int stops = 0;
        vm.PtzStopRequested += (_, _) => stops++;
        vm.RaisePadPress(1, 0);   // 누르고 있는 중

        // Act — 호스트가 죽었다가 다시 뜬다(그 사이 뗌의 정지는 갈 곳이 없었다)
        host.ChangeState(CameraPopupHostState.Restarting);
        Assert.Equal(0, stops);
        host.ChangeState(CameraPopupHostState.Running);
        host.ChangeState(CameraPopupHostState.Restarting);
        host.ChangeState(CameraPopupHostState.Running);

        // Assert — 다시 떴을 때 정지 한 번(두 번째 재시작에서는 빚이 없다)
        Assert.Equal(1, stops);
    }

    [Fact]
    public void should_not_send_stop_when_host_restarts_and_nothing_was_held()
    {
        var host = new OverlayTestHost();
        var vm = new CameraStreamPopupViewModel(7, "정문", new PointLatLng(37, 127), host, Provider);
        int stops = 0;
        vm.PtzStopRequested += (_, _) => stops++;
        vm.RaiseZoomHold(1);
        vm.RaisePtzStop();   // 정상 뗌
        Assert.Equal(1, stops);

        host.ChangeState(CameraPopupHostState.Restarting);
        host.ChangeState(CameraPopupHostState.Running);

        Assert.Equal(1, stops);
    }
}

/// <summary>
/// 실제 <see cref="CameraStreamPopupControl"/> 에 시험용 템플릿(영상 상자 + PTZ 단추)을 입혀 입력 이벤트를 넣는다.
/// 화면에 없는 요소는 마우스 위치를 읽지 못하므로 위치는 <see cref="CameraStreamPopupControl.PointerPosition"/> 로 넣는다.
/// </summary>
public class CameraPopupPtzGestureTests
{
    private static readonly VideoProviderInfo Provider = new() { Kind = VideoProviderKind.Onvif, Host = "10.0.0.5", Port = 80 };

    private sealed class Rig
    {
        public required CameraStreamPopupControl Control { get; init; }
        public required CameraStreamPopupViewModel Vm { get; init; }
        public required Dictionary<string, Button> Buttons { get; init; }
        public List<string> Log { get; } = new();
        public Point Pointer;
    }

    private static void OnSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA thread hung");
        failure?.Throw();
    }

    private static Rig NewRig(bool ptzCapable = true, bool imagingCapable = true)
    {
        var vm = new CameraStreamPopupViewModel(7, "정문", new PointLatLng(37, 127), new OverlayTestHost(), Provider)
        {
            IsPtzCapable = ptzCapable,
            IsImagingCapable = imagingCapable,
        };
        // 시험용 템플릿: 헤더 자리(42) 아래 영상 상자(PART_VideoRegion 384×258) + 표시 겹(PART_PtzOverlay) + 단추들
        var root = new FrameworkElementFactory(typeof(Grid));
        var video = new FrameworkElementFactory(typeof(Border), "PART_VideoRegion");
        video.SetValue(FrameworkElement.WidthProperty, 384d);
        video.SetValue(FrameworkElement.HeightProperty, 258d);
        video.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        video.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        video.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 42, 0, 0));
        root.AppendChild(video);
        root.AppendChild(new FrameworkElementFactory(typeof(Canvas), "PART_PtzOverlay"));
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        foreach (var tag in new[] { "0,-1", "1,0", "stop", "zoom:1", "focus:-1" })
        {
            var button = new FrameworkElementFactory(typeof(Button), "B" + Array.IndexOf(new[] { "0,-1", "1,0", "stop", "zoom:1", "focus:-1" }, tag));
            button.SetValue(FrameworkElement.TagProperty, tag);
            panel.AppendChild(button);
        }
        root.AppendChild(panel);
        var control = new CameraStreamPopupControl
        {
            Template = new ControlTemplate(typeof(CameraStreamPopupControl)) { VisualTree = root },
            DataContext = vm,
            Width = 384,
            Height = 600,
        };
        control.ApplyTemplate();
        control.Measure(new Size(384, 600));
        control.Arrange(new Rect(0, 0, 384, 600));
        control.UpdateLayout();

        var names = new[] { "0,-1", "1,0", "stop", "zoom:1", "focus:-1" };
        var rig = new Rig
        {
            Control = control,
            Vm = vm,
            Buttons = names.Select((t, i) => (t, (Button)control.Template.FindName("B" + i, control))).ToDictionary(x => x.t, x => x.Item2),
        };
        control.PointerPosition = (_, _) => rig.Pointer;
        vm.PtzNudgeRequested += (_, e) => rig.Log.Add($"move:{e.Dx},{e.Dy}");
        vm.ZoomHoldRequested += (_, d) => rig.Log.Add($"zoom:{d}");
        vm.FocusHoldRequested += (_, d) => rig.Log.Add($"focus:{d}");
        vm.PtzStopRequested += (_, _) => rig.Log.Add("stop");
        vm.FocusStopRequested += (_, _) => rig.Log.Add("focus-stop");
        vm.PtzDragRequested += (_, e) => rig.Log.Add(e.TryGetViewFraction(out double x, out double y, out double _)
            ? FormattableString.Invariant($"drag:{x:0.###},{y:0.###}") : "drag:?");
        return rig;
    }

    private static MouseButtonEventArgs Mouse_(RoutedEvent routed) => new(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = routed };
    private static void Press(Button b) => b.RaiseEvent(Mouse_(Mouse.PreviewMouseDownEvent));
    private static void Release(UIElement e) => e.RaiseEvent(Mouse_(Mouse.PreviewMouseUpEvent));
    private static void LoseCapture(UIElement e) => e.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });

    /// <summary>KeyEventArgs 가 요구하는 입력 원본 자리(창 · HWND 를 만들지 않는다).</summary>
    private sealed class FakePresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static void KeyEvent(UIElement target, RoutedEvent routed, Key key)
        => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, new FakePresentationSource(), 0, key) { RoutedEvent = routed });

    // ───────── 누르는 동안만 이동 ─────────

    [Fact]
    public void should_move_once_on_press_and_stop_once_on_release_when_direction_button_is_held()
        => OnSta(() =>
        {
            var rig = NewRig();

            Press(rig.Buttons["1,0"]);
            Assert.Equal(new[] { "move:1,0" }, rig.Log);

            Release(rig.Buttons["1,0"]);
            LoseCapture(rig.Control);   // 뗌 뒤에 따라오는 캡처 해제 — 정지를 또 보내지 않는다

            Assert.Equal(new[] { "move:1,0", "stop" }, rig.Log);
        });

    [Fact]
    public void should_stop_when_released_outside_the_button()
        => OnSta(() =>
        {
            var rig = NewRig();
            Press(rig.Buttons["0,-1"]);

            Release(rig.Control);   // 단추 밖(컨트롤이 캡처를 쥐고 있다)에서 뗌

            Assert.Equal(new[] { "move:0,-1", "stop" }, rig.Log);
        });

    [Fact]
    public void should_stop_zoom_when_mouse_capture_is_lost_while_holding()
        => OnSta(() =>
        {
            var rig = NewRig();
            Press(rig.Buttons["zoom:1"]);

            LoseCapture(rig.Control);
            LoseCapture(rig.Control);

            Assert.Equal(new[] { "zoom:1", "stop" }, rig.Log);
        });

    [Fact]
    public void should_stop_focus_motor_when_focus_button_is_released()
        => OnSta(() =>
        {
            var rig = NewRig();

            Press(rig.Buttons["focus:-1"]);
            Release(rig.Buttons["focus:-1"]);

            Assert.Equal(new[] { "focus:-1", "focus-stop" }, rig.Log);   // 포커스는 PTZ 정지가 아니라 포커스 모터 정지
        });

    [Fact]
    public void should_stop_when_window_is_deactivated_or_popup_is_unloaded_while_holding()
        => OnSta(() =>
        {
            var rig = NewRig();
            Press(rig.Buttons["1,0"]);
            rig.Control.OnWindowDeactivated(null, EventArgs.Empty);
            Assert.Equal(new[] { "move:1,0", "stop" }, rig.Log);

            Press(rig.Buttons["0,-1"]);
            rig.Control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            rig.Control.OnWindowDeactivated(null, EventArgs.Empty);

            Assert.Equal(new[] { "move:1,0", "stop", "move:0,-1", "stop" }, rig.Log);
        });

    [Fact]
    public void should_not_send_stop_when_nothing_is_held()
        => OnSta(() =>
        {
            var rig = NewRig();

            Release(rig.Control);
            LoseCapture(rig.Control);
            rig.Control.OnWindowDeactivated(null, EventArgs.Empty);
            rig.Control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Empty(rig.Log);
        });

    [Fact]
    public void should_always_send_stop_when_stop_button_is_pressed()
        => OnSta(() =>
        {
            var rig = NewRig();

            Press(rig.Buttons["stop"]);

            Assert.Equal(new[] { "stop" }, rig.Log);
        });

    [Fact]
    public void should_move_on_key_down_and_stop_on_key_up_when_button_has_focus()
        => OnSta(() =>
        {
            var rig = NewRig();

            KeyEvent(rig.Buttons["1,0"], Keyboard.PreviewKeyDownEvent, Key.Space);
            KeyEvent(rig.Buttons["1,0"], Keyboard.PreviewKeyDownEvent, Key.Space);   // 누르고 있는 동안 다시 오는 KeyDown
            Assert.Equal(new[] { "move:1,0" }, rig.Log);

            KeyEvent(rig.Buttons["1,0"], Keyboard.PreviewKeyUpEvent, Key.Space);
            KeyEvent(rig.Buttons["1,0"], Keyboard.PreviewKeyUpEvent, Key.Space);

            Assert.Equal(new[] { "move:1,0", "stop" }, rig.Log);
        });

    [Fact]
    public void should_move_with_enter_key_when_zoom_button_has_focus()
        => OnSta(() =>
        {
            var rig = NewRig();

            KeyEvent(rig.Buttons["zoom:1"], Keyboard.PreviewKeyDownEvent, Key.Enter);
            KeyEvent(rig.Buttons["zoom:1"], Keyboard.PreviewKeyUpEvent, Key.Enter);

            Assert.Equal(new[] { "zoom:1", "stop" }, rig.Log);
        });

    [Fact]
    public void should_not_move_when_camera_is_not_ptz_capable()
        => OnSta(() =>
        {
            var rig = NewRig(ptzCapable: false);

            Press(rig.Buttons["1,0"]);
            Release(rig.Buttons["1,0"]);
            KeyEvent(rig.Buttons["1,0"], Keyboard.PreviewKeyDownEvent, Key.Space);
            KeyEvent(rig.Buttons["1,0"], Keyboard.PreviewKeyUpEvent, Key.Space);

            Assert.Empty(rig.Log);
        });

    // ───────── 영상 위 드래그 ─────────

    private static void DragStart(Rig rig, double x, double y)
    {
        rig.Pointer = new Point(x, y);
        rig.Control.RaiseEvent(Mouse_(Mouse.MouseDownEvent));
    }

    private static void DragTo(Rig rig, double x, double y)
    {
        rig.Pointer = new Point(x, y);
        rig.Control.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
    }

    private static void DragEnd(Rig rig) => rig.Control.RaiseEvent(Mouse_(Mouse.MouseUpEvent));

    [Fact]
    public void should_request_one_drag_move_on_release_when_video_is_dragged()
        => OnSta(() =>
        {
            var rig = NewRig();

            DragStart(rig, 100, 100);
            DragTo(rig, 150, 100);
            Assert.Empty(rig.Log);   // 끄는 동안에는 보내지 않는다(목표 표시만)
            DragTo(rig, 196, 74.2);
            DragEnd(rig);
            LoseCapture(rig.Control);

            Assert.Equal(new[] { "drag:0.25,-0.1" }, rig.Log);   // 96/384 · −25.8/258
        });

    [Fact]
    public void should_treat_as_click_when_drag_is_inside_dead_zone()
        => OnSta(() =>
        {
            var rig = NewRig();

            DragStart(rig, 100, 100);
            DragTo(rig, 104, 105);
            DragEnd(rig);

            Assert.Empty(rig.Log);
        });

    [Fact]
    public void should_cancel_drag_when_escape_is_pressed_or_capture_is_lost()
        => OnSta(() =>
        {
            var rig = NewRig();

            DragStart(rig, 100, 100);
            DragTo(rig, 250, 100);
            KeyEvent(rig.Control, Keyboard.PreviewKeyDownEvent, Key.Escape);
            DragEnd(rig);

            DragStart(rig, 100, 100);
            DragTo(rig, 250, 100);
            LoseCapture(rig.Control);
            DragEnd(rig);

            Assert.Empty(rig.Log);
        });

    [Fact]
    public void should_not_start_drag_when_camera_is_not_ptz_capable()
        => OnSta(() =>
        {
            var rig = NewRig(ptzCapable: false);

            DragStart(rig, 100, 100);
            DragTo(rig, 250, 100);
            DragEnd(rig);

            Assert.Empty(rig.Log);
        });
}
