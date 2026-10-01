using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 펜스 보기 캔버스의 높이 단계 — 세로 끌기(축 잠금 · 단계 맞추기 · 안내선 · Esc · 여러 대) · Alt+↑/↓ · Shift+Alt+↑/↓ · 수정키 누른 순서(헤디드 r21).
/// 끌기 제스처 자체는 UIA 로 단언할 수 없어 캔버스의 시험 길(OnPointer* · HandleKeyDown)로 부른다(drag-first-ux).
/// </summary>
[Collection(WiringFenceWindowCollection.NAME)]
public class WiringFenceHeightViewTests
{
    private static (FenceMountSpot Spot, FenceLane Lane) SpotOf(WiringViewModel vm, int key) => (vm.FenceLayout.MountOf(key)!.Spot, vm.FenceLayout.LaneOf(key));

    #region - Vertical drag -
    [Fact]
    public void should_lock_a_vertical_drag_to_height_snap_to_the_coil_and_redraw_the_guide_only_when_the_stop_changes()
    {
        var result = OnWindow(WiringFenceHeightTests.Razor(WiringFenceHeightTests.Build("SFSFF")), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            var x0 = FenceLayoutMath.PointOf(vm.FenceLayout.MountOf(102)!, vm.FenceLayout.Geometry).XM;
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(1, -12));                // 데드존을 넘는 순간 세로가 우세
            var action = canvas.DragAction;
            canvas.OnPointerMoved(start + new Vector(1, -600));
            var level = canvas.HeightLevel;
            var label = canvas.HeightGuide?.Label;
            var pill = canvas.OverlayShapes.Any(s => s.Ink == FenceInk.PillInsert && s.Text == label);
            var updates = canvas.OverlayUpdates;
            canvas.OnPointerMoved(start + new Vector(1, -620));                // 같은 단계
            var sameStopUpdates = canvas.OverlayUpdates - updates;
            canvas.OnPointerReleased(start + new Vector(1, -620));
            Pump();
            return (action, level, label, pill, sameStopUpdates, Spot: SpotOf(vm, 102), Guide: canvas.HeightGuide, vm.CanUndo, x0, X1: FenceLayoutMath.PointOf(vm.FenceLayout.MountOf(102)!, vm.FenceLayout.Geometry).XM,
                    Drawn: canvas.Scene!.X[102] / canvas.Scene.Upm);
        });

        Assert.Equal(FenceGestureAction.ChangeHeight, result.action);
        Assert.Equal(2, result.level);
        Assert.Equal(result.x0, result.X1, 9);                                 // 코일로 들어가도 설치 가로 자리는 그대로(망 가운데)
        Assert.Equal(result.X1, result.Drawn, 6);                              // 그림도 그 망 가운데(아래 줄 이웃과 벌리던 몫이 없어진 것뿐)
        Assert.Equal("높이: 윤형 코일 · 위 줄로", result.label);
        Assert.True(result.pill);
        Assert.Equal(0, result.sameStopUpdates);                              // 후보 단계가 그대로면 다시 그리지 않는다
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), result.Spot);
        Assert.Null(result.Guide);
        Assert.True(result.CanUndo);
    }

    [Fact]
    public void should_snap_to_the_nearest_stop_by_height_when_dropped_between_stops()
    {
        var result = OnWindow(WiringFenceHeightTests.Razor(WiringFenceHeightTests.Build("SFSFF")), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(0, -12));
            var heights = canvas.StopHeights.ToList();
            // 망 아래 단계 쪽으로, 두 단계 사이를 조금 넘게(가장 가까운 단계는 망 아래)
            var dy = (heights[1] - heights[0]) * canvas.Scale * canvas.Projector.Cy * 0.6;
            canvas.OnPointerMoved(start + new Vector(0, dy));
            var level = canvas.HeightLevel;
            canvas.OnPointerReleased(start + new Vector(0, dy));
            Pump();
            return (heights, level, Spot: SpotOf(vm, 102));
        });

        Assert.True(result.heights.SequenceEqual(result.heights.OrderBy(h => h)));     // 아래 → 위
        Assert.Equal(3, result.heights.Count);                                // 망 아래 · 망 가운데 · 윤형 코일(기둥 위는 기둥 센서만)
        Assert.Equal(0, result.level);
        Assert.Equal((FenceMountSpot.PanelBottom, FenceLane.Lower), result.Spot);
    }

    [Fact]
    public void should_put_everything_back_when_escape_cancels_a_height_drag()
    {
        var result = OnWindow(WiringFenceHeightTests.Razor(WiringFenceHeightTests.Build("SFSFF")), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(0, -400));
            var during = canvas.HeightLevel;
            var handled = canvas.HandleKeyDown(Key.Escape, Key.None, ModifierKeys.None, chip);
            Pump();
            return (during, handled, canvas.IsDragging, Spot: SpotOf(vm, 102), vm.CanUndo, vm.HasChanges, Guide: canvas.HeightGuide,
                    Pill: canvas.OverlayShapes.Any(s => s.Ink == FenceInk.PillInsert), vm.StatusText, Opacity: chip.Opacity);
        });

        Assert.Equal(2, result.during);
        Assert.True(result.handled);
        Assert.False(result.IsDragging);
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), result.Spot);
        Assert.False(result.CanUndo);                                         // 되돌리기 장면도 쌓지 않는다
        Assert.Null(result.Guide);
        Assert.False(result.Pill);
        Assert.Contains("높이를 바꾸지 않았습니다", result.StatusText);
        Assert.Equal(1, result.Opacity);
    }

    [Fact]
    public void should_keep_a_horizontal_drag_as_a_panel_move_when_the_horizontal_motion_dominates()
    {
        var result = OnWindow(WiringFenceHeightTests.Razor(WiringFenceHeightTests.Build("SFSFF")), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, -5));
            var action = canvas.DragAction;
            canvas.OnPointerMoved(start + new Vector(30, -300));             // 잠근 뒤에는 세로로 가도 가로 끌기
            var height = canvas.HeightLevel;
            canvas.HandleKeyDown(Key.Escape, Key.None, ModifierKeys.None, chip);
            Pump();
            return (action, height);
        });

        Assert.Equal(FenceGestureAction.MoveSensors, result.action);
        Assert.Null(result.height);
    }

    [Fact]
    public void should_raise_every_selected_sensor_together_when_one_of_them_is_dragged_up()
    {
        var result = OnWindow(WiringFenceHeightTests.Razor(WiringFenceHeightTests.Build("SFSFF")), (vm, canvas) =>
        {
            vm.FenceSelectSensors(new[] { 102, 104 });
            Pump();
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(0, -12));
            canvas.OnPointerMoved(start + new Vector(0, -600));
            var label = canvas.HeightGuide?.Label;
            canvas.OnPointerReleased(start + new Vector(0, -600));
            Pump();
            var both = (SpotOf(vm, 102), SpotOf(vm, 104));
            vm.Undo();
            return (label, both, After: (SpotOf(vm, 102), SpotOf(vm, 104)));
        });

        Assert.EndsWith("· 2대", result.label);
        Assert.Equal(((FenceMountSpot.RazorCoil, FenceLane.Upper), (FenceMountSpot.RazorCoil, FenceLane.Upper)), result.both);
        Assert.Equal(((FenceMountSpot.PanelCenter, FenceLane.Lower), (FenceMountSpot.PanelCenter, FenceLane.Lower)), result.After);   // 되돌리기 한 번
    }
    #endregion

    #region - Keyboard -
    [Fact]
    public void should_step_one_stop_with_alt_arrows_via_the_system_key_and_nudge_with_shift_alt()
    {
        var result = OnWindow(WiringFenceHeightTests.Razor(WiringFenceHeightTests.Build("SFSFF")), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            chip.Focus();
            var plainUp = canvas.HandleKeyDown(Key.Up, Key.None, ModifierKeys.Alt, chip);          // Alt 화살표는 Key.System 으로만 온다
            var afterPlain = SpotOf(vm, 102);
            var up = canvas.HandleKeyDown(Key.System, Key.Up, ModifierKeys.Alt, canvas.SensorChips[102]);
            Pump();
            var afterUp = SpotOf(vm, 102);
            var nudge = canvas.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift, canvas.SensorChips[102]);
            Pump();
            var offset = vm.FenceLayout.MountOf(102)!.HeightOffsetM;
            var afterNudge = SpotOf(vm, 102);
            canvas.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt, canvas.SensorChips[102]);
            Pump();
            return (plainUp, afterPlain, up, afterUp, nudge, offset, afterNudge, Down: SpotOf(vm, 102));
        });

        Assert.False(result.plainUp);
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), result.afterPlain);
        Assert.True(result.up);
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), result.afterUp);
        Assert.True(result.nudge);
        Assert.Equal(-0.1, result.offset, 6);
        Assert.Equal((FenceMountSpot.RazorCoil, FenceLane.Upper), result.afterNudge);      // 미세 높이는 단계를 바꾸지 않는다
        Assert.Equal((FenceMountSpot.PanelCenter, FenceLane.Lower), result.Down);
    }

    [Theory]
    [InlineData(true)]     // Shift 먼저 — WPF 가 Alt|Shift 를 알려 준다
    [InlineData(false)]    // Alt 먼저 — WPF 가 Shift 를 빠뜨려도(Alt 만) Shift 의 실제 상태로 Alt+Shift 가 된다(헤디드 r21)
    public void should_move_to_the_next_panel_with_alt_shift_right_whichever_modifier_was_pressed_first(bool shiftFirst)
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            chip.Focus();
            var before = vm.FenceLayout.MountOf(102)!;
            var reported = shiftFirst ? ModifierKeys.Alt | ModifierKeys.Shift : ModifierKeys.Alt;
            var modifiers = FenceKeyModifiers.Combine(reported, shiftActuallyDown: true);
            var handled = canvas.HandleKeyDown(Key.System, Key.Right, modifiers, chip);
            Pump();
            return (handled, Before: before.Panel, After: vm.FenceLayout.MountOf(102)!.Panel, vm.StatusText);
        });

        Assert.True(result.handled);
        Assert.Equal(result.Before + 1, result.After);                       // 다른 망(기둥)으로 한 칸 — 줄 안 한 칸이 아니다
        Assert.StartsWith("옮김 — ", result.StatusText);
        Assert.Contains($"기둥 {result.After + 1}", result.StatusText);
    }

    [Fact]
    public void should_combine_the_reported_modifiers_with_the_actual_shift_state()
    {
        Assert.Equal(ModifierKeys.Alt | ModifierKeys.Shift, FenceKeyModifiers.Combine(ModifierKeys.Alt, true));
        Assert.Equal(ModifierKeys.Alt, FenceKeyModifiers.Combine(ModifierKeys.Alt, false));
        Assert.Equal(ModifierKeys.Alt | ModifierKeys.Shift, FenceKeyModifiers.Combine(ModifierKeys.Alt | ModifierKeys.Shift, false));
    }
    #endregion

    #region - Fixtures -
    private static T OnWindow<T>(WiringViewModel vm, Func<WiringViewModel, FenceCanvas, T> body)
        => OnSta(() =>
        {
            var window = new Window
            {
                Content = new FenceView { DataContext = vm },
                Width = 1100,
                Height = 620,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };
            window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(File.ReadAllText(
                Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml"))));
            window.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");
            window.Show();
            Pump();
            vm.IsSoftwareRendering = false;
            vm.IsFlatChosen = false;
            Pump();
            try { return body(vm, Descendants<FenceCanvas>(window).Single()); }
            finally { window.Close(); }
        });

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
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
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }
    #endregion
}
