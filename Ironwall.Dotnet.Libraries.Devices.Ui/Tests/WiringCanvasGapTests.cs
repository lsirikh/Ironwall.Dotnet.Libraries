using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 3D 펜스 캔버스 · 개념도 — 키보드 판정(<c>HandleKeyDown</c>) · 그림 층 · 픽셀(카탈로그 §6 WIR-1xx · 2xx).
/// <b>창을 띄우지 않는다</b> — 부모 없는 비주얼을 Measure/Arrange 하고 <see cref="RenderTargetBitmap"/> 으로 읽는다(사용자가 PC 를 쓰는 중).
/// </summary>
public class WiringCanvasGapTests
{
    #region - Rig -
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

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static ResourceDictionary Theme(bool dark = false)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", dark ? "Tokens.Dark.xaml" : "Tokens.Light.xaml")));

    /// <summary>부모 없는 펜스 캔버스 — 테마 토큰을 제 자원으로 들고 <paramref name="width"/>×<paramref name="height"/> 로 선다.</summary>
    private static FenceCanvas Canvas(WiringViewModel vm, double width = 1200, double height = 520)
    {
        var canvas = new FenceCanvas { SuppressMenuPopup = true };
        canvas.Resources.MergedDictionaries.Add(Theme());
        canvas.ViewModel = vm;
        Layout(canvas, width, height);
        canvas.Rebuild();
        Layout(canvas, width, height);
        return canvas;
    }

    private static FenceConceptView Concept(WiringViewModel vm, double width = 1100, double height = 300)
    {
        var view = new FenceConceptView { SuppressMenuPopup = true };
        view.Resources.MergedDictionaries.Add(Theme());
        view.ViewModel = vm;
        Layout(view, width, height);
        view.Rebuild();
        Layout(view, width, height);
        return view;
    }

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static byte[] Pixels(FrameworkElement element)
    {
        var width = (int)Math.Ceiling(element.ActualWidth);
        var height = (int)Math.Ceiling(element.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    private static int Differing(byte[] a, byte[] b)
    {
        var count = 0;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i += 4)
            if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2]) count++;
        return count;
    }

    private static int DistinctColours(byte[] pixels)
    {
        var set = new HashSet<int>();
        for (var i = 0; i < pixels.Length; i += 4) set.Add(pixels[i] | pixels[i + 1] << 8 | pixels[i + 2] << 16);
        return set.Count;
    }

    private const ModifierKeys None = ModifierKeys.None;
    #endregion

    #region - 3D 캔버스 키보드 (WIR-128 · 132 · 133 · 135 · 139 · 140 · 141 · 162 · 116 · 113/114) -
    [Fact]
    public void should_step_to_the_next_grid_point_with_ctrl_arrow_and_back_and_move_a_panel_with_alt_shift_arrow_WIR128()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[102];
            var start = vm.FenceGridCellOf(102);

            var ctrlRight = canvas.HandleKeyDown(Key.Right, Key.None, ModifierKeys.Control, chip);
            var stepped = vm.FenceGridCellOf(102);
            canvas.HandleKeyDown(Key.Left, Key.None, ModifierKeys.Control, canvas.SensorChips[102]);
            var back = vm.FenceGridCellOf(102);
            var panel = vm.FenceLayout.MountOf(102)!.Panel;
            var altShift = canvas.HandleKeyDown(Key.System, Key.Right, ModifierKeys.Alt | ModifierKeys.Shift, canvas.SensorChips[102]);
            return (ctrlRight, start, stepped, back, panel, altShift, After: vm.FenceLayout.MountOf(102)!.Panel, Selected: vm.FenceSelectedKeys.ToList());
        });

        Assert.True(result.ctrlRight);
        Assert.NotEqual(result.start, result.stepped);
        Assert.Equal(result.start, result.back);
        Assert.True(result.altShift);
        Assert.Equal(result.panel + 1, result.After);
        Assert.Equal(new[] { 102 }, result.Selected);                                       // 키로 옮긴 센서가 골라진다
    }

    [Fact]
    public void should_climb_to_the_razor_coil_with_alt_up_and_nudge_the_height_with_shift_alt_WIR132_WIR133_WIR135()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("FFFF");
            vm.FenceSelectAllPanels();
            vm.ChoosePanelStyle(EnumFenceStyle.ChainLinkRazor);
            vm.ApplyPanelEdit();                                                            // 펜스센서가 코일로 따라 붙는다
            var key = vm.FenceChain.Keys.First();
            vm.FenceSetLane(new[] { key }, FenceLane.Lower);
            vm.FenceSelect(key);
            var canvas = Canvas(vm);

            var levels = new List<(FenceMountSpot, FenceLane)>();
            for (var i = 0; i < 6; i++)
            {
                canvas.HandleKeyDown(Key.System, Key.Up, ModifierKeys.Alt, canvas.SensorChips[key]);
                var m = vm.FenceLayout.MountOf(key)!;
                levels.Add((m.Spot, m.Lane));
            }
            var top = vm.StatusText;
            canvas.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt, canvas.SensorChips[key]);
            var down = vm.FenceLayout.MountOf(key)!;
            canvas.HandleKeyDown(Key.System, Key.Up, ModifierKeys.Alt | ModifierKeys.Shift, canvas.SensorChips[key]);
            var nudged = vm.FenceLayout.MountOf(key)!.HeightOffsetM;
            canvas.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift, canvas.SensorChips[key]);
            canvas.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift, canvas.SensorChips[key]);
            return (levels, top, down, nudged, Lowered: vm.FenceLayout.MountOf(key)!.HeightOffsetM);
        });

        Assert.Contains((FenceMountSpot.RazorCoil, FenceLane.Upper), result.levels);       // Alt+↑ 를 거듭하면 윤형 코일(위 줄)
        Assert.Equal("더 올릴 단계가 없습니다 — 맨 위입니다.", result.top);
        Assert.NotEqual(result.levels[^1], (result.down.Spot, result.down.Lane));
        Assert.Equal(FenceLayoutMath.NUDGE_M, result.nudged, 3);
        Assert.Equal(-FenceLayoutMath.NUDGE_M, result.Lowered, 3);
    }

    [Fact]
    public void should_turn_with_r_and_shift_r_flip_with_f_and_leave_text_boxes_alone_WIR139_WIR140_WIR141()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSS");
            var canvas = Canvas(vm);
            var yaw0 = vm.Board.YawOf(102);

            canvas.HandleKeyDown(Key.R, Key.None, None, canvas.SensorChips[102]);
            var r = vm.Board.YawOf(102);
            canvas.HandleKeyDown(Key.R, Key.None, ModifierKeys.Shift, canvas.SensorChips[102]);
            var shiftR = vm.Board.YawOf(102);
            canvas.HandleKeyDown(Key.F, Key.None, None, canvas.SensorChips[102]);
            var f = vm.Board.FacingOf(102);
            var name = AutomationProperties.GetName(canvas.SensorChips[102]);

            // 오프셋 칸(글 칸)에 포커스 — r · f 는 글자일 뿐
            var box = new TextBox();
            var inText = (canvas.HandleKeyDown(Key.R, Key.None, None, box), canvas.HandleKeyDown(Key.F, Key.None, None, box));
            return (yaw0, r, shiftR, f, name, inText, YawAfter: vm.Board.YawOf(102), FacingAfter: vm.Board.FacingOf(102));
        });

        Assert.Equal(WiringYawMath.Rotate(result.yaw0, 1), result.r);
        Assert.Equal(result.yaw0, result.shiftR);
        Assert.Equal(WiringFacing.Back, result.f);
        Assert.Contains("내부", result.name);                                               // 칩 이름에 면이 드러난다
        Assert.Equal((false, false), result.inText);
        Assert.Equal(result.yaw0, result.YawAfter);
        Assert.Equal(WiringFacing.Back, result.FacingAfter);
    }

    [Fact]
    public void should_unplace_with_delete_restore_with_ctrl_z_and_clear_with_esc_on_the_canvas_WIR162_WIR114()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var canvas = Canvas(vm);
            vm.FenceSelect(103);

            canvas.HandleKeyDown(Key.Delete, Key.None, None, canvas.SensorChips[103]);
            var removed = (vm.FenceChain.Keys.ToList(), vm.UnplacedCount);
            canvas.HandleKeyDown(Key.Z, Key.None, ModifierKeys.Control, canvas);
            var restored = vm.FenceChain.Keys.ToList();
            vm.FenceSelect(102);
            var esc = canvas.HandleKeyDown(Key.Escape, Key.None, None, canvas);
            var escAgain = canvas.HandleKeyDown(Key.Escape, Key.None, None, canvas);
            return (removed, restored, esc, escAgain, vm.HasAnySelection);
        });

        Assert.Equal(new[] { 101, 102, 104 }, result.removed.Item1);
        Assert.Equal(1, result.removed.UnplacedCount);
        Assert.Equal(new[] { 101, 102, 103, 104 }, result.restored);
        Assert.True(result.esc);
        Assert.False(result.escAgain);                                                      // 풀 것이 없으면 Esc 를 흘려보낸다
        Assert.False(result.HasAnySelection);
    }

    [Fact]
    public void should_build_a_selection_with_ctrl_space_shift_arrow_and_ctrl_a_from_the_keyboard_WIR116()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSSS");
            var canvas = Canvas(vm);

            canvas.HandleKeyDown(Key.Space, Key.None, None, canvas.SensorChips[101]);           // Enter/Space = 고르기
            canvas.HandleKeyDown(Key.Space, Key.None, ModifierKeys.Control, canvas.SensorChips[103]);
            var toggled = vm.FenceSelectedKeys.OrderBy(k => k).ToList();
            vm.FenceSelect(102);
            canvas.HandleKeyDown(Key.Right, Key.None, ModifierKeys.Shift, canvas.SensorChips[102]);
            var shift = vm.FenceSelectedKeys.OrderBy(k => k).ToList();
            canvas.HandleKeyDown(Key.A, Key.None, ModifierKeys.Control, canvas.SensorChips[102]);
            var all = vm.FenceSelectedKeys.Count;
            canvas.HandleKeyDown(Key.A, Key.None, ModifierKeys.Control, canvas.PanelChips[0]);
            return (toggled, shift, all, Panels: vm.FenceSelectedPanels.Count, PanelCount: vm.FenceLayout.Panels.Count);
        });

        Assert.Equal(new[] { 101, 103 }, result.toggled);
        Assert.Equal(new[] { 102, 103 }, result.shift);
        Assert.Equal(5, result.all);
        Assert.Equal(result.PanelCount, result.Panels);                                     // 망에 포커스면 Ctrl+A = 망 모두
    }

    [Fact]
    public void should_move_to_the_chain_ends_with_alt_home_and_alt_end_on_the_canvas_WIR164()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var canvas = Canvas(vm);
            canvas.HandleKeyDown(Key.System, Key.End, ModifierKeys.Alt, canvas.SensorChips[101]);
            var end = vm.FenceChain.Keys.ToList();
            canvas.HandleKeyDown(Key.System, Key.Home, ModifierKeys.Alt, canvas.SensorChips[101]);
            return (end, home: vm.FenceChain.Keys.ToList());
        });

        Assert.Equal(new[] { 102, 103, 104, 101 }, result.end);
        Assert.Equal(new[] { 101, 102, 103, 104 }, result.home);
    }
    #endregion

    #region - 3D 그림 층 (WIR-103 · 119 · 155 · 156) -
    [Fact]
    public void should_draw_each_of_the_five_fence_styles_differently_WIR102_WIR103()
    {
        var images = OnSta(() =>
        {
            var styles = new[] { EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.Brick, EnumFenceStyle.Concrete, EnumFenceStyle.DesignFence };
            return styles.Select(style =>
            {
                var (vm, _, _, _) = WiringGapRig.Build("SSSS");
                vm.FenceSelectAllPanels();
                vm.ChoosePanelStyle(style);
                vm.ApplyPanelEdit();
                var canvas = Canvas(vm);
                var names = canvas.PanelChips.Values.Select(AutomationProperties.GetName).ToList();
                return (style, pixels: Pixels(canvas), names);
            }).ToList();
        });

        for (var i = 0; i < images.Count; i++)
        {
            Assert.True(DistinctColours(images[i].pixels) > 8, $"{images[i].style} 색이 너무 적다");
            Assert.All(images[i].names, n => Assert.Contains(FencePanelSpec.StyleText(images[i].style), n));
            for (var j = i + 1; j < images.Count; j++)
                Assert.True(Differing(images[i].pixels, images[j].pixels) > 500, $"{images[i].style} 와 {images[j].style} 의 그림이 같다");
        }
    }

    [Fact]
    public void should_add_distance_labels_to_the_3d_view_only_while_the_distance_toggle_is_on_WIR119()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var canvas = Canvas(vm);
            var off = (canvas.StaticShapes.Count, Pixels(canvas));
            vm.ToggleDistances();
            Layout(canvas, 1200, 520);
            var on = (canvas.StaticShapes.Count, Pixels(canvas));
            vm.ToggleDistances();
            Layout(canvas, 1200, 520);
            return (off, on, back: (canvas.StaticShapes.Count, Pixels(canvas)));
        });

        Assert.True(result.on.Count > result.off.Count, $"꺼짐 {result.off.Count} · 켜짐 {result.on.Count}");
        Assert.True(Differing(result.off.Item2, result.on.Item2) > 50);
        Assert.Equal(result.off.Count, result.back.Count);
        Assert.Equal(0, Differing(result.off.Item2, result.back.Item2));                    // 끄면 처음 그림 그대로
    }

    [Fact]
    public void should_draw_range_and_cable_overlays_only_when_toggled_WIR155_WIR156()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("MMMM", controllerType: "PidsController");
            var canvas = Canvas(vm);
            var plain = canvas.StaticShapes.Count;
            vm.ToggleRange();
            var range = canvas.StaticShapes.Count;
            vm.ToggleRange();
            vm.ToggleCables();
            var cables = canvas.StaticShapes.Count;
            return (plain, range, cables);
        });

        Assert.True(result.range > result.plain, $"범위 {result.range} · 기본 {result.plain}");
        Assert.True(result.cables > result.plain, $"케이블 {result.cables} · 기본 {result.plain}");
    }
    #endregion

    #region - 개념도 (WIR-200 · 201 · 204 · 208 · 210 · 217 · 225 · 119) -
    [Fact]
    public void should_title_the_concept_with_both_lanes_or_the_return_line_WIR200_WIR201()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        var oneLane = vm.ConceptTitle;
        vm.FenceSetLane(new[] { 103, 104 }, FenceLane.Upper);
        var twoLanes = vm.ConceptTitle;
        vm.ChooseControllerRight();

        Assert.Equal("개념도 · Ch1(A) → 아래 줄 4 → 리턴선 → Ch2(B) · 제어기 왼쪽 끝", oneLane);
        Assert.Equal("개념도 · Ch1(A) → 아래 줄 2 → 위 줄 2 → Ch2(B) · 제어기 왼쪽 끝", twoLanes);
        Assert.EndsWith("제어기 오른쪽 끝", vm.ConceptTitle);
    }

    [Fact]
    public void should_draw_the_return_line_differently_from_a_second_lane_in_the_concept_WIR201_WIR202()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            var one = (Pixels(view), view.BackgroundShapes.Count, Upper: view.Geometry!.Nodes.Count(n => n.Lane == FenceLane.Upper));
            vm.FenceSetLane(new[] { 103, 104 }, FenceLane.Upper);
            Layout(view, 1100, 300);
            var two = (Pixels(view), view.BackgroundShapes.Count, Upper: view.Geometry!.Nodes.Count(n => n.Lane == FenceLane.Upper));
            return (one, two, Width: (int)view.ActualWidth);
        });

        Assert.Equal(0, result.one.Upper);
        Assert.Equal(2, result.two.Upper);
        Assert.True(Differing(result.one.Item1, result.two.Item1) > 200);
        Assert.True(DistinctColours(result.one.Item1) > 4);
    }

    [Fact]
    public void should_put_the_controller_chip_on_the_chosen_end_of_the_concept_WIR203_WIR204()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            var left = (view.Geometry!.Controller.X, AutomationProperties.GetName(view.ControllerChip!), Leftmost: view.Geometry.Nodes.OrderBy(n => n.Center.X).First().Key, First: vm.FenceChain.Keys[0]);
            vm.ChooseControllerRight();
            Layout(view, 1100, 300);
            var right = (view.Geometry!.Controller.X, AutomationProperties.GetName(view.ControllerChip!), Leftmost: view.Geometry.Nodes.OrderBy(n => n.Center.X).First().Key, First: vm.FenceChain.Keys[0]);
            return (left, right, Width: view.ActualWidth);
        });

        Assert.True(result.left.X < result.Width / 2);
        Assert.Contains("CTRL-북측-01", result.left.Item2);
        Assert.Contains("왼쪽 끝", result.left.Item2);
        Assert.True(result.right.X > result.Width / 2);
        Assert.Contains("오른쪽 끝", result.right.Item2);
        Assert.Equal(result.left.Leftmost, result.right.Leftmost);                          // 펜스 위 자리는 그대로
        Assert.Equal(101, result.left.First);                                               // 순번(사슬) 방향만 뒤집힌다 — 제어기 쪽(오른쪽)부터
        Assert.Equal(104, result.right.First);
    }

    [Fact]
    public void should_name_the_vbus_chip_by_its_gap_and_hide_it_on_mixed_rings_WIR208_WIR210()
    {
        var result = OnSta(() =>
        {
            var (smart, _, _, _) = WiringGapRig.Build("SSSSSS");
            var view = Concept(smart);
            var before = AutomationProperties.GetName(view.VbusChip!);
            smart.SetVbusGap(2);
            var after = AutomationProperties.GetName(view.VbusChip!);
            var (mixed, _, _, _) = WiringGapRig.Build("SSFS");
            var other = Concept(mixed);
            return (before, after, Shown: view.VbusChip!.Visibility, Mixed: other.VbusChip?.Visibility);
        });

        Assert.StartsWith("VBus — 3번째 센서 뒤", result.before);
        Assert.StartsWith("VBus — 2번째 센서 뒤", result.after);
        Assert.Equal(Visibility.Visible, result.Shown);
        Assert.True(result.Mixed is null or Visibility.Collapsed);
    }

    [Fact]
    public void should_unplace_with_delete_and_restore_with_ctrl_z_in_the_concept_WIR217()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            view.HandleKeyDown(Key.Delete, Key.None, None, view.NodeChips[102]);
            var removed = vm.FenceChain.Keys.ToList();
            view.HandleKeyDown(Key.Z, Key.None, ModifierKeys.Control, view);
            var restored = vm.FenceChain.Keys.ToList();
            view.HandleKeyDown(Key.System, Key.Up, ModifierKeys.Alt, view.NodeChips[104]);
            var lane = vm.FenceLayout.LaneOf(104);
            view.HandleKeyDown(Key.Right, Key.None, ModifierKeys.Control, view.NodeChips[101]);
            return (removed, restored, lane, Cell: vm.FenceGridCellOf(101));
        });

        Assert.Equal(new[] { 101, 103, 104 }, result.removed);
        Assert.Equal(new[] { 101, 102, 103, 104 }, result.restored);
        Assert.Equal(FenceLane.Upper, result.lane);
        Assert.NotNull(result.Cell);
    }

    [Fact]
    public void should_mark_the_concept_node_selected_when_the_3d_view_selects_it_WIR225()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            vm.FenceSelectSensors(new[] { 102, 104 });
            return view.NodeChips.ToDictionary(p => p.Key, p => AutomationProperties.GetItemStatus(p.Value));
        });

        Assert.Equal("선택됨", result[102]);
        Assert.Equal("선택됨", result[104]);
        Assert.Equal(string.Empty, result[101]);
    }

    [Fact]
    public void should_draw_lane_distances_in_the_concept_only_while_the_toggle_is_on_WIR119()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            var off = (view.BackgroundShapes.Count, Pixels(view));
            vm.ToggleDistances();
            Layout(view, 1100, 300);
            var on = (view.BackgroundShapes.Count, Pixels(view));
            return (off, on);
        });

        Assert.True(result.on.Count > result.off.Count, $"꺼짐 {result.off.Count} · 켜짐 {result.on.Count}");
        Assert.True(Differing(result.off.Item2, result.on.Item2) > 30, $"다른 픽셀 {Differing(result.off.Item2, result.on.Item2)}");
    }
    #endregion
}
