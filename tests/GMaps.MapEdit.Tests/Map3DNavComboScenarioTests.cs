using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;
using Rig = GMaps.MapEdit.Tests.Map3DEntryScenarioTests.Rig;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 방향 링 · 이동 단추(2026-10-19) 경계 · 취소 · 연타 · 다른 기능과 조합 시나리오 — Map3DNavScenarioTests 의 빈 칸.
/// 실제 MapViewModel · 실제 Map3DNavControl(이벤트를 직접 올린다). 창 · WebView2 는 만들지 않는다.
/// </summary>
public class Map3DNavComboScenarioTests
{
    private const string Area = "area-nav-combo";
    private static readonly DateTime T0 = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    private static void Enter(Rig rig)
    {
        if (!rig.Vm.Map3DAreas.Any()) rig.Areas(rig.AreaAround(Area));
        rig.W.Map.Position = rig.Center;
        rig.HostReady(Area);
        rig.Gate();
        rig.PressEnter();
        Map3DEntryScenarioTests.SettleEntry(rig);
        Assert.True(rig.Shown);
    }

    private static void Camera(Rig rig, double bearing)
        => Scenario.Call(rig.Vm, "OnMap3DMessage", Map3DMath.ParseMessage(
               new JObject { ["type"] = "camera", ["reason"] = "user", ["lon"] = rig.Center.Lng, ["lat"] = rig.Center.Lat, ["zoom"] = 18, ["bearing"] = bearing, ["pitch"] = 60 }.ToString()));

    private static DateTime _now;
    private static void UseClock(Rig rig, DateTime start)
    {
        _now = start;
        Scenario.SetField(rig.Vm, "_map3DNavClock", (Func<DateTime>)(() => _now));
    }

    private static bool Moving(Rig rig) => Scenario.GetField<Map3DNavMover?>(rig.Vm, "_map3DNavMover")?.IsActive == true;
    private static bool TimerOn(Rig rig) => Scenario.GetField<DispatcherTimer?>(rig.Vm, "_map3DNavTimer")?.IsEnabled == true;

    private static (Map3DNavControl ctrl, HeadlessSource source) Control(Rig rig)
    {
        var ctrl = new Map3DNavControl { DataContext = rig.Vm };
        return (ctrl, new HeadlessSource { RootVisual = ctrl });
    }

    private static bool PressKey(UIElement target, PresentationSource source, Key key, bool down)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent };
        target.RaiseEvent(e);
        return e.Handled;
    }

    /// <summary>이동 단추(AutomationId GMaps.Map3D.Nav.{id}) — 논리 트리에서 찾는다(x:Name 없음).</summary>
    private static Button MoveButton(Map3DNavControl ctrl, string id)
    {
        Button? Find(object node)
        {
            if (node is Button b && AutomationProperties.GetAutomationId(b) == $"GMaps.Map3D.Nav.{id}") return b;
            if (node is not DependencyObject d) return null;
            foreach (var c in LogicalTreeHelper.GetChildren(d)) if (Find(c) is { } hit) return hit;
            return null;
        }
        return Find(ctrl) ?? throw new InvalidOperationException($"단추 {id} 없음");
    }

    private static void MouseDown(Button b)
        => b.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });

    private static void MouseUp(Button b)
        => b.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });

    private static void LoseCapture(Button b)
        => b.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.LostMouseCaptureEvent });

    /// <summary>판에서 키보드 초점이 빠졌다(창 비활성 · 다른 곳 클릭) — IsKeyboardFocusWithinChanged 처리기.</summary>
    private static void LoseFocus(Map3DNavControl ctrl)
        => Scenario.Call(ctrl, "OnKeyboardFocusWithinChanged", ctrl, new DependencyPropertyChangedEventArgs(UIElement.IsKeyboardFocusWithinProperty, true, false));

    // ─────────────── 취소 · 창 잃음 ───────────────

    [Fact]
    public void should_stop_held_move_when_move_button_loses_mouse_capture()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            UseClock(rig, T0);
            var (ctrl, _) = Control(rig);
            var forward = MoveButton(ctrl, "Forward");

            MouseDown(forward);
            Assert.True(Moving(rig), "누르면 계속 이동");
            Assert.True(TimerOn(rig));
            LoseCapture(forward);                     // 누른 채 창을 잃음(Alt+Tab · 다른 창 클릭) → 캡처 잃음
            Assert.False(Moving(rig), "캡처를 잃으면 멈춘다");
            Assert.False(TimerOn(rig));
            MouseUp(forward);                         // 늦게 온 뗌 — 아무 일 없음
            Assert.False(Moving(rig));
        });

    [Fact]
    public void should_stop_key_held_move_when_panel_loses_keyboard_focus_and_ignore_late_key_up()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            UseClock(rig, T0);
            var (ctrl, source) = Control(rig);

            Assert.True(PressKey(ctrl, source, Key.W, down: true));
            Assert.True(PressKey(ctrl, source, Key.D, down: true));
            Assert.True(Moving(rig));
            LoseFocus(ctrl);                          // 키를 누른 채 창을 잃음 — 뗌을 못 받는다
            Assert.False(Moving(rig), "초점을 잃으면 모두 멈춘다");
            Assert.False(TimerOn(rig));
            Assert.True(PressKey(ctrl, source, Key.W, down: false));   // 돌아와 늦게 받은 뗌
            Assert.False(Moving(rig));
        });

    [Fact]
    public void should_stop_moves_and_ignore_late_drag_when_3d_exits_while_ring_is_dragged()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            UseClock(rig, T0);
            Camera(rig, 0);
            var drag = new Map3DNavDrag(ring: true, deadZone: 8);
            drag.Press(50, 50, 50, 10);                              // 링 위(N 자리)를 잡고
            Assert.Null(drag.Move(52, 11));                          // 데드존 안 = 아직 클릭
            rig.Vm.NudgeMap3D(drag.Move(90, 50)!.Value);             // 시계로 90° 끌기 = 방위 −90
            Assert.Equal("방위 270°", rig.Vm.Map3DNavBearingText);
            rig.Vm.StartMap3DMove(Map3DNavMove.Forward);              // 다른 손으로 전진도 누르고 있다

            rig.PressExit();                                         // 끄는 중 「나가기」
            Assert.False(rig.Vm.IsMap3DShown);
            Assert.False(Moving(rig), "3D 를 나가면 이동이 멈춘다");
            if (drag.Move(50, 90) is { } late) rig.Vm.NudgeMap3D(late);   // 접힌 판에 늦게 온 끌기 — 2D 에서는 보내지 않는다
            Assert.Equal("방위 270°", rig.Vm.Map3DNavBearingText);
            Assert.False(drag.Finish(commit: false), "캡처 잃음 = 끌기 완료 아님");
            Assert.False(drag.IsPressed);

            Enter(rig);                                              // 다시 3D — 링 · 이동이 처음부터 다시 된다
            Camera(rig, 0);
            rig.Vm.NudgeMap3D(new Map3DNudge(30, 0, 0, 0, 0));
            Assert.Equal("방위 30°", rig.Vm.Map3DNavBearingText);
            rig.Vm.StartMap3DMove(Map3DNavMove.Back);
            Assert.True(Moving(rig));
            rig.Vm.StopMap3DMove(Map3DNavMove.Back);
        });

    [Fact]
    public void should_ignore_ring_and_moves_while_3d_entry_is_still_pending()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            rig.Areas(rig.AreaAround(Area));
            rig.W.Map.Position = rig.Center;
            rig.HostReady(Area);
            rig.Gate();
            rig.PressEnter();
            Assert.Equal("Pending", rig.State);                     // 안내 막대를 채우는 중(아직 3D 아님)
            UseClock(rig, T0);
            rig.Vm.NudgeMap3D(new Map3DNudge(45, 0, 0, 0, 0));
            rig.Vm.Map3DNorthCommand.Execute(null);
            rig.Vm.StartMap3DMove(Map3DNavMove.ZoomIn);
            Assert.Equal("방위 0°", rig.Vm.Map3DNavBearingText);
            Assert.False(Moving(rig));
            Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.True(rig.Shown);
        });

    // ─────────────── 연타 ───────────────

    [Fact]
    public void should_keep_single_hold_on_key_repeat_and_stop_cleanly_after_rapid_press_release()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            UseClock(rig, T0);
            var (ctrl, source) = Control(rig);
            for (int i = 0; i < 10; i++) Assert.True(PressKey(ctrl, source, Key.A, down: true));   // 키 반복
            Assert.True(Moving(rig));
            Assert.True(PressKey(ctrl, source, Key.A, down: false));   // 한 번 떼면 멈춘다(반복 누름이 쌓이지 않는다)
            Assert.False(Moving(rig));
            Assert.False(TimerOn(rig));

            var zoomIn = MoveButton(ctrl, "ZoomIn");
            for (int i = 0; i < 20; i++) { MouseDown(zoomIn); MouseUp(zoomIn); }   // 단추 연타
            Assert.False(Moving(rig));
            Assert.False(TimerOn(rig));

            for (int i = 0; i < 12; i++) Assert.True(PressKey(ctrl, source, Key.Right, down: true));   // 링 → 12번 = 180°
            Assert.Equal("방위 180°", rig.Vm.Map3DNavBearingText);
            for (int i = 0; i < 5; i++) Assert.True(PressKey(ctrl, source, Key.N, down: true));       // N 연타 = 정북 그대로
            Assert.Equal("방위 0°", rig.Vm.Map3DNavBearingText);
        });

    // ─────────────── 마우스 + 키보드 같은 방향 ───────────────

    [Fact]
    public void should_keep_moving_while_mouse_still_holds_when_same_direction_key_is_tapped()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            UseClock(rig, T0);
            var (ctrl, source) = Control(rig);
            var forward = MoveButton(ctrl, "Forward");

            MouseDown(forward);                                     // 마우스로 전진을 누르고 있는 채
            Assert.True(PressKey(ctrl, source, Key.W, down: true)); // 같은 방향 키를 톡
            Assert.True(PressKey(ctrl, source, Key.W, down: false));
            Assert.True(Moving(rig), "마우스는 아직 누르고 있다 — 계속 간다");
            MouseUp(forward);
            Assert.False(Moving(rig));

            Assert.True(PressKey(ctrl, source, Key.S, down: true)); // 반대로 — 키를 누르고 있는 채
            var back = MoveButton(ctrl, "Back");
            MouseDown(back);
            MouseUp(back);                                          // 같은 방향 단추를 톡
            Assert.True(Moving(rig), "키는 아직 누르고 있다 — 계속 간다");
            Assert.True(PressKey(ctrl, source, Key.S, down: false));
            Assert.False(Moving(rig));
        });

    // ─────────────── 다른 기능과 조합 ───────────────

    [Fact]
    public void should_stop_held_move_and_ignore_ring_when_edit_mode_starts_in_native_window_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            UseClock(rig, T0);
            Camera(rig, 20);
            Scenario.SetField(rig.Vm, "_isCompassVisible", true);   // 생성자 없이 만든 VM — 보기 메뉴 기본값(나침반 켬)
            rig.Vm.StartMap3DMove(Map3DNavMove.Right);
            Assert.True(Moving(rig));
            try
            {
                Map3DEntryScenarioTests.UseNativeWindowHost(rig, true);   // 2026-10-21 — 편집 모드가 3D 를 접는 것은 네이티브 창 3D 뿐
                rig.Vm.IsEditModeEnabled = true;                    // 3D 중 편집 모드 — 네이티브 창 3D 는 3D 를 먼저 접는다
                Assert.False(rig.Shown);
                Assert.False(Moving(rig), "3D 를 접으면 누르고 있던 이동도 멈춘다");
                rig.Vm.NudgeMap3D(new Map3DNudge(40, 0, 0, 0, 0));   // 편집 중 링 = 무시
                Assert.Equal("방위 20°", rig.Vm.Map3DNavBearingText);
                Assert.True(rig.Vm.IsCompassShown, "2D 편집 중에는 나침반");
            }
            finally
            {
                rig.Vm.IsEditModeEnabled = false;
                Map3DEntryScenarioTests.UseNativeWindowHost(rig, false);
                Mouse.OverrideCursor = null;
            }
        });

    [Fact]
    public void should_leave_group_rotation_keys_alone_and_let_ring_arrows_through_window_hook_in_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Enter(rig);
            Camera(rig, 0);
            var (ctrl, source) = Control(rig);
            // 창 PreviewKeyDown(묶음 이동 · 회전 후킹)이 먼저 받는다 — 3D(운영 모드)에서는 묶음 회전 금지 · 방향키를 먹지 않아야 링에 닿는다
            foreach (var key in new[] { Key.OemCloseBrackets, Key.OemOpenBrackets, Key.Left, Key.Right, Key.Up, Key.Down })
            {
                var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.W.Map, e);
                Assert.False(e.Handled, $"창 후킹이 3D 에서 {key} 를 먹었다");
            }
            Assert.DoesNotContain("회전", rig.Status);
            Assert.True(PressKey(ctrl, source, Key.Left, down: true));
            Assert.Equal("방위 345°", rig.Vm.Map3DNavBearingText);
            Assert.False(PressKey(ctrl, source, Key.OemCloseBrackets, down: true), "] 는 판도 먹지 않는다");
        });

    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
