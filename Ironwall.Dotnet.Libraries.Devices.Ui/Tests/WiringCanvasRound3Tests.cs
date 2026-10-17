using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 3D 펜스 · 개념도 · 펜스 보기 도구줄 3회차(2026-10-11 · 카탈로그 §6.2 · 6.3 WIR-100 ~ 167 · 205 ~ 220).
/// <b>창을 띄우지 않는다</b> — 부모 없는 <see cref="FenceCanvas"/> · <see cref="FenceConceptView"/> · 컴파일된 <see cref="FenceView"/> 를 Measure/Arrange 하고,
/// 마우스는 캔버스의 시험 진입점(누름 · 움직임 · 뗌 · Esc 취소)으로, 도구줄 단추는 Click 이벤트로(코드비하인드 처리기를 그대로 지난다) 민다.
/// </summary>
[Collection("CaliburnIoC")]      // 단추 Click → 뷰모델이 Caliburn 정적 PlatformProvider 로 UI 스레드에 넘긴다 — 그것을 잠시 바꾸는 클래스와 나란히 돌면 다른 스레드 디스패처를 잡는다(WIR-100 · WIR-158 순서 의존 실패)
public class WiringCanvasRound3Tests
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

    private static ResourceDictionary Theme()
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml")));

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    /// <summary>
    /// 부모 없는 펜스 캔버스 — 끄는 동안의 덧그림(빨강 점 · 띠 · 고스트)은 어도너 층에 그려지므로 창 대신 <see cref="System.Windows.Documents.AdornerDecorator"/> 로 감싼다
    /// (창 안에서는 창의 어도너 층이 그 몫을 한다).
    /// </summary>
    private static FenceCanvas Canvas(WiringViewModel vm, double width = 1200, double height = 520)
    {
        var canvas = new FenceCanvas { SuppressMenuPopup = true };
        canvas.Resources.MergedDictionaries.Add(Theme());
        var decorator = new System.Windows.Documents.AdornerDecorator { Child = canvas };
        canvas.ViewModel = vm;
        Layout(decorator, width, height);
        canvas.Rebuild();
        Layout(decorator, width, height);
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

    /// <summary>컴파일된 펜스 보기(도구줄 · 캔버스 · 나눔 막대 · 개념도) — 부모 없이. 렌더 펌프가 없으니 Loaded 를 직접 울린다(줌 글 · 나눔 비율 배선).</summary>
    private static FenceView View(WiringViewModel vm, double width = 1200, double height = 760)
    {
        var view = new FenceView();
        view.Resources.MergedDictionaries.Add(Theme());
        view.DataContext = vm;
        Layout(view, width, height);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, view));
        Layout(view, width, height);
        return view;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static T ById<T>(DependencyObject root, string id) where T : FrameworkElement
        => Descendants<T>(root).First(e => AutomationProperties.GetAutomationId(e) == id);

    private static void Click(DependencyObject root, string id) => ById<ButtonBase>(root, id).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static bool Shown(UIElement e, DependencyObject root)
    {
        for (DependencyObject? d = e; d is not null && !ReferenceEquals(d, root); d = VisualTreeHelper.GetParent(d))
            if (d is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    /// <summary>스마트 4대(기둥 0 · 1 · 2 · 3 = 격자 gx 0 · 4 · 8 · 12)의 제안 펜스 — 끌기의 격자 점은 칸 이름으로 찾는다.</summary>
    private static WiringViewModel Smart4() => WiringFenceHeightTests.Build("SSSS");

    private static Point ScreenOf(FenceCanvas canvas, FenceGridCell cell) => canvas.WorldToScreen(canvas.SnapPoints.Single(p => p.Cell == cell).At);

    private const ModifierKeys None = ModifierKeys.None;
    #endregion

    #region - 입체 · 평면 (WIR-100 · 101) -
    [Fact]
    [Trait("Scenario", "WIR-100,WIR-101")]
    public void should_start_tilted_and_keep_chip_order_and_names_while_showing_the_flat_note_until_tilt_is_chosen_again_WIR100_WIR101()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSSS");
            var view = View(vm);
            var canvas = view.Canvas!;
            var note = ById<FrameworkElement>(view, "Devices.Wiring.Fence.FlatNote");
            List<int> Order() => canvas.SensorChips.OrderBy(p => canvas.ScreenCenterOf(p.Value).X).Select(p => p.Key).ToList();
            List<string> Names() => canvas.SensorChips.OrderBy(p => p.Key).Select(p => AutomationProperties.GetName(p.Value)).ToList();
            var open = (canvas.Projector.K, vm.IsTilt, NoteShown: Shown(note, view), Order: Order(), Names: Names());

            Click(view, "Devices.Wiring.Fence.Flat");                                           // [평면]
            Layout(view, 1200, 760);
            var flat = (canvas.Projector.K, vm.FlatNoteText, NoteShown: Shown(note, view), Order: Order(), Names: Names());
            Click(view, "Devices.Wiring.Fence.Tilt");                                           // [입체]
            Layout(view, 1200, 760);
            return (open, flat, Back: (canvas.Projector.K, NoteShown: Shown(note, view), Order: Order()));
        });

        Assert.Equal((1.0, true, false), (result.open.K, result.open.IsTilt, result.open.NoteShown));       // WIR-100 입체로 시작 · 평면 안내 없음
        Assert.Equal(0.0, result.flat.K);
        Assert.Equal("평면 표시 중 — [입체 보기]로 되돌립니다", result.flat.FlatNoteText);                    // `.FlatNote`
        Assert.True(result.flat.NoteShown);
        Assert.Equal(result.open.Order, result.flat.Order);                                    // 순서 그대로
        Assert.Equal(result.open.Names, result.flat.Names);                                    // 글 그대로
        Assert.Equal((1.0, false), (result.Back.K, result.Back.NoteShown));
        Assert.Equal(result.open.Order, result.Back.Order);
    }
    #endregion

    #region - 선택 (WIR-113 · 115) -
    [Fact]
    [Trait("Scenario", "WIR-113")]
    public void should_keep_the_selection_and_say_so_when_a_rubber_band_is_cancelled_with_esc_WIR113()
    {
        var result = OnSta(() =>
        {
            var vm = Smart4();
            var canvas = Canvas(vm);
            vm.FenceSelect(102);
            var start = new Point(8, 8);                                                       // 빈 하늘
            canvas.OnPointerPressed(start, null);
            canvas.OnPointerMoved(start + new Vector(600, 400));                                // 센서를 덮는 띠
            var during = (canvas.DragAction, Band: canvas.OverlayScreenShapes.Count);
            var esc = canvas.HandleKeyDown(Key.Escape, Key.None, None, canvas);
            return (during, esc, vm.StatusText, Selected: vm.FenceSelectedKeys.ToList(), canvas.IsDragging, After: canvas.OverlayScreenShapes.Count);
        });

        Assert.Equal(FenceGestureAction.RubberSensors, result.during.DragAction);
        Assert.True(result.during.Band > 0, "띠가 그려지지 않았다");
        Assert.True(result.esc);
        Assert.Equal("취소 — 선택을 바꾸지 않았습니다", result.StatusText);                   // `Devices.Wiring.Status`
        Assert.Equal(new[] { 102 }, result.Selected);                                           // 선택 그대로
        Assert.False(result.IsDragging);
        Assert.Equal(0, result.After);
    }

    [Fact]
    [Trait("Scenario", "WIR-115")]
    public void should_title_the_pane_with_two_sensors_after_ctrl_clicking_two_chips_WIR115()
    {
        var result = OnSta(() =>
        {
            var vm = Smart4();
            var canvas = Canvas(vm);
            foreach (var key in new[] { 101, 103 })
            {
                var chip = canvas.SensorChips[key];
                var at = canvas.ScreenCenterOf(chip);
                canvas.OnPointerPressed(at, chip, ctrl: true);
                canvas.OnPointerReleased(at);                                                   // 데드존 안에서 뗌 = Ctrl+클릭
            }
            return (vm.SelectedTitle, Keys: vm.FenceSelectedKeys.OrderBy(k => k).ToList(), vm.MountPlaceText);
        });

        Assert.Equal("센서 2대 선택", result.SelectedTitle);                                  // `.Pane.Title` 「…2대」
        Assert.Equal(new[] { 101, 103 }, result.Keys);
        Assert.Equal("센서 2대", result.MountPlaceText);
    }
    #endregion

    #region - 격자 끌기 (WIR-122 · 123 · 124 · 125 · 126) -
    [Fact]
    [Trait("Scenario", "WIR-122")]
    public void should_show_red_snap_dots_while_dragging_a_post_sensor_hide_them_on_release_and_seat_it_on_a_far_panel_centre_WIR122()
    {
        var result = OnSta(() =>
        {
            var vm = Smart4();
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[101];
            var start = canvas.ScreenCenterOf(chip);
            var before = vm.FenceLayout.MountOf(101)!;
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));                                   // 데드존을 넘는다 → 격자 끌기
            var target = new FenceGridCell(10, 1);                                              // 망 2(먼 망)의 가운데(gx 4·2+2 · gy 1)
            var at = ScreenOf(canvas, target);
            canvas.OnPointerMoved(at);
            var during = (canvas.DragAction, Points: canvas.SnapPoints.Count, Red: canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapDot),
                          canvas.SnapTarget, Ring: canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapRing), vm.StatusText);
            canvas.OnPointerReleased(at);
            var after = (Red: canvas.OverlayShapes.Count(s => s.Ink is FenceInk.SnapDot or FenceInk.SnapRing or FenceInk.SnapDotDim), canvas.SnapPoints.Count);
            return (before, during, after, Mount: vm.FenceLayout.MountOf(101)!, Cell: vm.FenceGridCellOf(101), target);
        });

        Assert.Equal(FenceMountSpot.PostTop, result.before.Spot);                              // 기둥 센서
        Assert.Equal(FenceGestureAction.SnapMove, result.during.DragAction);
        Assert.True(result.during.Points >= 9, $"격자 점 {result.during.Points}");
        Assert.True(result.during.Red > 0, "끄는 중 빨간 점이 없다");                         // 빨강(Critical) 점
        Assert.Equal(result.target, result.during.SnapTarget);
        Assert.True(result.during.Ring > 0, "포인터 아래 점 고리가 없다");
        Assert.EndsWith("— 놓으면 확정 · Esc 취소", result.during.StatusText);
        Assert.Equal((0, 0), result.after);                                                    // 놓으면 사라짐
        Assert.Equal(result.target, result.Cell);                                              // 붙음
        Assert.Equal((2, FenceMountSpot.PanelCenter), (result.Mount.Panel, result.Mount.Spot)); // `.Mount.Place` = 망 3 가운데
    }

    [Fact]
    [Trait("Scenario", "WIR-123")]
    public void should_put_the_sensor_back_and_say_drop_near_a_red_dot_when_released_away_from_every_dot_WIR123()
    {
        var result = OnSta(() =>
        {
            var vm = Smart4();
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            var before = vm.FenceLayout.MountOf(102)!;
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var away = new Point(6, 6);                                                         // 캔버스 안 · 모든 점에서 멀다
            canvas.OnPointerMoved(away);
            var target = canvas.SnapTarget;
            canvas.OnPointerReleased(away);
            return (before, target, vm.StatusText, After: vm.FenceLayout.MountOf(102)!, vm.CanUndo, vm.HasChanges);
        });

        Assert.Null(result.target);
        Assert.Equal("제자리 — 빨강 점 가까이에서 놓아야 옮겨집니다", result.StatusText);
        Assert.Equal(result.before, result.After);
        Assert.False(result.CanUndo);
        Assert.False(result.HasChanges);
    }

    [Fact]
    [Trait("Scenario", "WIR-124")]
    public void should_refuse_a_group_drop_and_count_the_sensors_without_a_point_WIR124()
    {
        var result = OnSta(() =>
        {
            var vm = Smart4();
            var canvas = Canvas(vm);
            vm.FenceSelectSensors(new[] { 103, 104 });
            canvas.Rebuild();
            var chip = canvas.SensorChips[103];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var at = ScreenOf(canvas, new FenceGridCell(11, 2));                                // 103: gx 8 → 11 · 104: gx 12 → 15(점 없음)
            canvas.OnPointerMoved(at);
            var label = canvas.SnapLabel;
            var during = vm.StatusText;
            var blocked = canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapBlocked);
            canvas.OnPointerReleased(at);
            return (label, during, blocked, vm.StatusText, M103: vm.FenceLayout.MountOf(103)!.Spot, M104: vm.FenceLayout.MountOf(104)!.Spot, vm.CanUndo);
        });

        Assert.Equal("놓을 수 없음 — 1대 갈 점 없음", result.label);                         // 알약
        Assert.StartsWith("놓을 수 없음 — 1대 갈 점 없음", result.during);                    // 끄는 동안 상태 줄
        Assert.Equal(1, result.blocked);                                                       // 경고 모양
        Assert.Equal("놓지 않았습니다 — 1대가 갈 점이 없거나 이미 센서가 있습니다(모두 갈 수 있을 때만 함께 옮깁니다).", result.StatusText);
        Assert.Equal((FenceMountSpot.PostTop, FenceMountSpot.PostTop), (result.M103, result.M104));
        Assert.False(result.CanUndo);
    }

    [Fact]
    [Trait("Scenario", "WIR-125")]
    public void should_move_both_selected_sensors_and_keep_their_spacing_when_one_of_them_is_dragged_WIR125()
    {
        var result = OnSta(() =>
        {
            var vm = Smart4();
            var canvas = Canvas(vm);
            foreach (var key in new[] { 102, 103 })                                             // 2대 — Ctrl+클릭
            {
                var c = canvas.SensorChips[key];
                var p = canvas.ScreenCenterOf(c);
                canvas.OnPointerPressed(p, c, ctrl: true);
                canvas.OnPointerReleased(p);
            }
            var cells = (A: vm.FenceGridCellOf(102)!.Value, B: vm.FenceGridCellOf(103)!.Value);
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var at = ScreenOf(canvas, new FenceGridCell(5, 2));                                 // 102: gx 4 → 5 · 103: gx 8 → 9
            canvas.OnPointerMoved(at);
            var label = canvas.SnapLabel;
            canvas.OnPointerReleased(at);
            return (cells, label, After: (A: vm.FenceGridCellOf(102)!.Value, B: vm.FenceGridCellOf(103)!.Value), vm.StatusText);
        });

        Assert.EndsWith("· 2대", result.label);
        Assert.NotEqual(result.cells.A, result.After.A);                                       // 둘 다 옮겼다
        Assert.NotEqual(result.cells.B, result.After.B);
        Assert.Equal(result.cells.B.Gx - result.cells.A.Gx, result.After.B.Gx - result.After.A.Gx);   // 간격 유지
        Assert.Equal(result.cells.B.Gy - result.cells.A.Gy, result.After.B.Gy - result.After.A.Gy);
    }

    [Fact]
    [Trait("Scenario", "WIR-126")]
    public void should_put_everything_back_and_say_no_server_call_when_a_drag_is_cancelled_with_esc_WIR126()
    {
        var result = OnSta(() =>
        {
            var vm = Smart4();
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            var before = vm.FenceLayout.MountOf(102)!;
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            canvas.OnPointerMoved(ScreenOf(canvas, new FenceGridCell(6, 2)));
            var dragging = canvas.IsDragging;
            var esc = canvas.HandleKeyDown(Key.Escape, Key.None, None, canvas);
            return (dragging, esc, vm.StatusText, before, After: vm.FenceLayout.MountOf(102)!, vm.CanUndo, vm.HasChanges, Dots: canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapDot));
        });

        Assert.True(result.dragging);
        Assert.True(result.esc);
        Assert.Equal("취소 — 제자리로 돌렸습니다(서버 호출 없음)", result.StatusText);
        Assert.Equal(result.before, result.After);                                             // `.Mount.Place` 그대로
        Assert.False(result.CanUndo);
        Assert.False(result.HasChanges);
        Assert.Equal(0, result.Dots);
    }
    #endregion

    #region - 망 단계 단추 · 코일 한계 · 벽돌 3대 (WIR-130 · 134 · 138) -
    [Fact]
    [Trait("Scenario", "WIR-130")]
    public void should_step_the_selected_sensor_to_the_next_previous_and_typed_panel_with_the_mount_buttons_WIR130()
    {
        var (vm, _, _, _) = WiringGapRig.Build("FFFF");
        var key = vm.FenceLayout.Mounts.First(p => !p.Value.IsPostSpot).Key;
        vm.FenceSelect(key);
        var start = vm.FenceLayout.MountOf(key)!.Panel;

        var next = vm.FenceMoveSelectedToNextPanel();                                          // `.Mount.PanelNext`
        var afterNext = vm.FenceLayout.MountOf(key)!.Panel;
        var prev = vm.FenceMoveSelectedToPreviousPanel();                                      // `.Mount.PanelPrev`
        var afterPrev = vm.FenceLayout.MountOf(key)!.Panel;
        vm.MountPanelText = "3";                                                               // `.Mount.PanelNumber`
        var typed = vm.ApplyMountPanel();
        var afterTyped = vm.FenceLayout.MountOf(key)!.Panel;
        vm.MountPanelText = "99";
        var refused = vm.ApplyMountPanel();

        Assert.True(next);
        Assert.Equal(start + 1, afterNext);
        Assert.True(prev);
        Assert.Equal(start, afterPrev);
        Assert.True(typed);
        Assert.Equal(2, afterTyped);                                                           // 망 3(0부터 2)
        Assert.Equal("3", vm.MountPanelText);
        Assert.False(refused);
        Assert.StartsWith("망 번호는 1~", vm.StatusText);
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "WiringView.xaml"));
        foreach (var id in new[] { "Devices.Wiring.Fence.Mount.PanelNext", "Devices.Wiring.Fence.Mount.PanelPrev", "Devices.Wiring.Fence.Mount.PanelNumber" })
            Assert.Contains(id, xaml);
    }

    [Fact]
    [Trait("Scenario", "WIR-134")]
    public void should_stop_a_smart_multisensor_2_on_a_razor_panel_at_the_top_of_the_panel_and_never_reach_the_coil_WIR134()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelectAllPanels();
        vm.ChoosePanelStyle(EnumFenceStyle.ChainLinkRazor);
        Assert.True(vm.ApplyPanelEdit());
        vm.FenceSelect(102);
        Assert.True(vm.ChooseMountSpot(FenceMountSpot.PanelCenter));                          // 망 위 센서로
        var stops = vm.FenceHeightStops(102);

        var steps = new List<(bool Ok, FenceMountSpot Spot)>();
        for (var i = 0; i < 4; i++) steps.Add((vm.FenceRaiseSelected(), vm.FenceLayout.MountOf(102)!.Spot));   // `.Mount.Raise` ▲

        Assert.DoesNotContain(FenceMountSpot.RazorCoil, stops);                                // 코일은 펜스센서만
        Assert.Equal(FenceMountSpot.PanelTop, stops[^1]);
        Assert.Equal((true, FenceMountSpot.PanelTop), steps[0]);
        Assert.All(steps.Skip(1), s => Assert.Equal((false, FenceMountSpot.PanelTop), s));    // 「망 위」 에서 멈춤
        Assert.Equal("더 올릴 단계가 없습니다 — 맨 위입니다.", vm.StatusText);
        Assert.EndsWith("망 위", vm.MountPlaceText);
        Assert.Equal(FenceLane.Lower, vm.FenceLayout.LaneOf(102));
    }

    [Fact]
    [Trait("Scenario", "WIR-138")]
    public void should_draw_three_sensors_on_a_brick_panel_without_overlapping_chips_WIR138()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("FFFFF");                                    // 펜스센서(망 자리)
            WiringGapRig.Style(vm, 2, EnumFenceStyle.Brick);                                     // 망 3 = 벽돌
            foreach (var key in new[] { 102, 103, 104 })
            {
                vm.FenceSelect(key);
                vm.MountPanelText = "3";
                vm.ApplyMountPanel();
                vm.FenceSelect(key);
                vm.ChooseMountSpot(FenceMountSpot.WallTop);                                    // 셋 다 담 위 — 같은 자리 종류
            }
            var canvas = Canvas(vm);
            var rects = new[] { 102, 103, 104 }.Select(k => (k, Mount: vm.FenceLayout.MountOf(k)!,
                R: new Rect(System.Windows.Controls.Canvas.GetLeft(canvas.SensorChips[k]), System.Windows.Controls.Canvas.GetTop(canvas.SensorChips[k]),
                            canvas.SensorChips[k].Width, canvas.SensorChips[k].Height))).ToList();
            return rects;
        });

        Assert.All(result, r => Assert.Equal(2, r.Mount.Panel));                              // 셋 다 망 3
        Assert.All(result, r => Assert.True(r.Mount.Spot is FenceMountSpot.WallTop or FenceMountSpot.WallFace, r.Mount.Spot.ToString()));
        for (var i = 0; i < result.Count; i++)
            for (var j = i + 1; j < result.Count; j++)
                Assert.False(result[i].R.IntersectsWith(result[j].R) && Rect.Intersect(result[i].R, result[j].R) is { Width: > 0.5, Height: > 0.5 },
                    $"칩 {result[i].k} {result[i].R} 과 {result[j].k} {result[j].R} 이 겹친다");
    }
    #endregion

    #region - 메뉴 · 오른쪽 끌기 · 줌 (WIR-143 · 150 · 152 · 153) -
    [Fact]
    [Trait("Scenario", "WIR-143")]
    public void should_offer_apply_this_mount_to_every_sensor_on_right_click_and_change_nothing_when_the_menu_is_dismissed_WIR143()
    {
        var result = OnSta(() =>
        {
            var (vm, dialogs, _, _) = WiringGapRig.Build("SSSS");
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[102];
            var at = canvas.ScreenCenterOf(chip);
            var before = vm.FenceLayout.Mounts.ToDictionary(p => p.Key, p => p.Value);
            canvas.OnPointerPressed(at, chip, button: FencePointerButton.Right);
            canvas.OnPointerReleased(at);                                                        // 데드존 안 = 메뉴
            var menu = canvas.LastMenu!.Where(e => !e.IsSeparator).ToList();
            // Esc — 메뉴가 닫힐 뿐 아무 항목도 돌지 않는다(창 메뉴는 커널 ContextMenu)
            return (Items: menu.Select(e => (e.AutomationId, e.Text, e.IsEnabled)).ToList(), Selected: vm.FenceSelectedKeys.ToList(),
                    dialogs.ConfirmCount, Same: before.All(p => vm.FenceLayout.MountOf(p.Key) == p.Value), vm.HasChanges);
        });

        var applyAll = Assert.Single(result.Items, i => i.AutomationId.EndsWith(".ApplyAll", StringComparison.Ordinal));
        Assert.StartsWith("Devices.Wiring.Fence.Menu.", applyAll.AutomationId);                 // `.Fence.Menu.ApplyAll`
        Assert.Equal("이 설치 방식을 이 제어기 모든 센서에 적용", applyAll.Text);
        Assert.True(applyAll.IsEnabled);
        Assert.Equal(new[] { 102 }, result.Selected);                                           // 누른 센서가 골라진다
        Assert.Equal(0, result.ConfirmCount);
        Assert.True(result.Same);
        Assert.False(result.HasChanges);
    }

    [Fact]
    [Trait("Scenario", "WIR-150")]
    public void should_pan_without_opening_a_menu_when_the_canvas_is_dragged_with_the_right_button_WIR150()
    {
        var result = OnSta(() =>
        {
            var vm = Smart4();
            var canvas = Canvas(vm);
            var offset = canvas.Offset;
            var chipX = canvas.ScreenCenterOf(canvas.SensorChips[101]).X;
            var start = new Point(600, 260);
            canvas.OnPointerPressed(start, null, button: FencePointerButton.Right);
            canvas.OnPointerMoved(start + new Vector(-90, 0));
            var action = canvas.DragAction;
            canvas.OnPointerReleased(start + new Vector(-90, 0));
            return (action, Moved: canvas.Offset - offset, Chip: canvas.ScreenCenterOf(canvas.SensorChips[101]).X - chipX, canvas.LastMenu);
        });

        Assert.Equal(FenceGestureAction.Pan, result.action);
        Assert.Equal(-90, result.Moved.X, 1);                                                   // 팬
        Assert.Equal(-90, result.Chip, 1);                                                      // 칩 X 가 함께 움직인다
        Assert.Null(result.LastMenu);                                                           // 메뉴 없음
    }

    [Fact]
    [Trait("Scenario", "WIR-152")]
    public void should_zoom_around_the_pointer_and_update_the_zoom_text_on_wheel_steps_WIR152()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build(new string('S', 12));                         // 맞춘 배율이 상한(1.6) 아래가 되게 12대
            var view = View(vm);
            var canvas = view.Canvas!;
            var zoom = ById<TextBlock>(view, "Devices.Wiring.Fence.ZoomText");
            var cursor = canvas.ScreenCenterOf(canvas.SensorChips[104]);
            var world = canvas.ScreenToWorld(cursor);
            var start = (canvas.Scale, zoom.Text);
            canvas.ZoomAt(cursor, GraphViewportStep);                                           // 휠 위(OnMouseWheel 이 부르는 길)
            var up = (canvas.Scale, zoom.Text, Back: canvas.WorldToScreen(world));
            canvas.ZoomAt(cursor, 1 / GraphViewportStep);                                       // 휠 아래
            return (start, up, Down: (canvas.Scale, zoom.Text), cursor);
        });

        Assert.Equal(result.start.Scale * GraphViewportStep, result.up.Scale, 6);
        Assert.Equal(result.cursor.X, result.up.Back.X, 3);                                     // 포인터 아래 점이 그대로 — 포인터 기준
        Assert.Equal(result.cursor.Y, result.up.Back.Y, 3);
        Assert.Equal($"{result.start.Scale * 100:0}%", result.start.Text);                      // `.ZoomText`
        Assert.Equal($"{result.up.Scale * 100:0}%", result.up.Text);
        Assert.NotEqual(result.start.Text, result.up.Text);
        Assert.Equal(result.start.Scale, result.Down.Scale, 6);
        Assert.Equal(result.start.Text, result.Down.Text);
    }

    private static double GraphViewportStep => Ironwall.Dotnet.Libraries.Utils.Consoles.Graph.GraphViewport.WheelStep;

    [Fact]
    [Trait("Scenario", "WIR-153")]
    public void should_change_the_percent_with_the_zoom_buttons_and_come_back_to_the_fitted_view_with_fit_WIR153()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build(new string('S', 12));
            var view = View(vm);
            var canvas = view.Canvas!;
            var zoom = ById<TextBlock>(view, "Devices.Wiring.Fence.ZoomText");
            var fitted = (canvas.Scale, zoom.Text, canvas.Offset);
            Click(view, "Devices.Wiring.Fence.ZoomIn");
            var zin = (canvas.Scale, zoom.Text);
            Click(view, "Devices.Wiring.Fence.ZoomOut");
            Click(view, "Devices.Wiring.Fence.ZoomOut");
            var zout = (canvas.Scale, zoom.Text);
            Click(view, "Devices.Wiring.Fence.Fit");
            return (fitted, zin, zout, Fit: (canvas.Scale, zoom.Text, canvas.Offset));
        });

        Assert.Equal(result.fitted.Scale * GraphViewportStep, result.zin.Scale, 6);           // [+]
        Assert.Equal($"{result.zin.Scale * 100:0}%", result.zin.Text);
        Assert.Equal(result.fitted.Scale / GraphViewportStep, result.zout.Scale, 6);          // [−] ×2
        Assert.Equal($"{result.zout.Scale * 100:0}%", result.zout.Text);
        Assert.Equal(result.fitted.Scale, result.Fit.Scale, 6);                               // [맞춤] — 처음 맞춘 보기로
        Assert.Equal(result.fitted.Text, result.Fit.Text);
        Assert.Equal(result.fitted.Offset.X, result.Fit.Offset.X, 3);
    }
    #endregion

    #region - 나눔 막대 (WIR-158) -
    [Theory]
    [InlineData(80)]
    [InlineData(-80)]
    [Trait("Scenario", "WIR-158")]
    public void should_grow_or_shrink_the_canvas_by_the_dragged_distance_and_remember_the_ratio_WIR158(double delta)
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSSS");
            var view = View(vm, 1200, 860);
            var splitter = ById<GridSplitter>(view, "Devices.Wiring.Fence.Split");
            var canvas = view.Canvas!;
            var before = (canvas.ActualHeight, vm.FenceSplitRatio);
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0));
            splitter.RaiseEvent(new DragDeltaEventArgs(0, delta));                              // `.Fence.Split` ±80
            Layout(view, 1200, 860);                                                            // 실창에서는 끄는 동안 배치가 돈다
            splitter.RaiseEvent(new DragCompletedEventArgs(0, delta, false));                   // 놓음 → 비율 기억(OnSplitDragCompleted)
            Layout(view, 1200, 860);
            return (before, After: (canvas.ActualHeight, vm.FenceSplitRatio));
        });

        Assert.Equal(result.before.ActualHeight + delta, result.After.ActualHeight, 0);          // 캔버스 높이
        Assert.Equal(Math.Sign(delta), Math.Sign(result.After.FenceSplitRatio - result.before.FenceSplitRatio));
    }
    #endregion

    #region - 개념도 (WIR-167 · 205 · 206 · 213 · 214) -
    [Fact]
    [Trait("Scenario", "WIR-167")]
    public void should_name_the_concept_cell_upper_lane_after_the_lane_upper_button_WIR167()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            var before = AutomationProperties.GetName(view.NodeChips[103]);
            vm.FenceSelect(103);
            var ok = vm.FenceSetLaneUpper();                                                     // `.Lane.Upper`
            Layout(view, 1100, 300);
            return (before, ok, After: AutomationProperties.GetName(view.NodeChips[103]), Upper: view.Geometry!.Nodes.Single(n => n.Key == 103).Lane, vm.IsLaneUpper);
        });

        Assert.DoesNotContain("위 줄", result.before);
        Assert.True(result.ok);
        Assert.Contains("위 줄", result.After);                                                // 개념도 칸 「위 줄」
        Assert.Equal(FenceLane.Upper, result.Upper);
        Assert.True(result.IsLaneUpper);
    }

    private static Point ControllerCenter(FenceConceptView view)
    {
        var c = view.Geometry!.Controller;
        return new Point(c.X + c.Width / 2, c.Y + c.Height / 2);
    }

    [Fact]
    [Trait("Scenario", "WIR-205")]
    public void should_say_where_the_controller_goes_while_dragging_it_past_the_middle_and_flip_it_on_release_WIR205()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            var start = ControllerCenter(view);
            view.OnPointerPressed(start, view.ControllerChip);
            view.OnPointerMoved(start + new Vector(12, 0));
            var far = new Point(view.ActualWidth - 20, start.Y);
            view.OnPointerMoved(far);
            var during = vm.StatusText;
            view.OnPointerReleased(far);
            return (during, End: vm.FenceControllerEnd, First: vm.FenceChain.Keys[0], vm.ConceptTitle);
        });

        Assert.Equal("개념도 — 놓으면 제어기를 오른쪽 끝으로 · Esc 취소", result.during);      // 「놓으면 제어기를 …」
        Assert.Equal(FenceControllerEnd.Right, result.End);                                     // 뒤집힘
        Assert.Equal(104, result.First);                                                        // 순번 방향도
        Assert.EndsWith("제어기 오른쪽 끝", result.ConceptTitle);
    }

    [Fact]
    [Trait("Scenario", "WIR-206,WIR-213")]
    public void should_leave_the_concept_as_it_was_when_a_controller_or_cell_drag_is_cancelled_with_esc_WIR206_WIR213()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            var chain = vm.FenceChain.Keys.ToList();

            var start = ControllerCenter(view);                                                 // C 끌기 → Esc
            view.OnPointerPressed(start, view.ControllerChip);
            view.OnPointerMoved(start + new Vector(12, 0));
            view.OnPointerMoved(new Point(view.ActualWidth - 20, start.Y));
            var controllerEsc = view.HandleKeyDown(Key.Escape, Key.None, None, view.ControllerChip);
            var controller = (vm.StatusText, vm.FenceControllerEnd);

            var node = view.NodeChips[102];                                                     // 칸 끌기 → Esc
            var at = view.ScreenCenterOf(102);
            view.OnPointerPressed(at, node);
            view.OnPointerMoved(at + new Vector(12, 0));
            view.OnPointerMoved(view.ScreenCenterOf(104) + new Vector(20, 0));
            var cellEsc = view.HandleKeyDown(Key.Escape, Key.None, None, node);
            return (controllerEsc, controller, cellEsc, Cell: vm.StatusText, chain, After: vm.FenceChain.Keys.ToList(), vm.CanUndo, view.IsDragging);
        });

        Assert.True(result.controllerEsc);
        Assert.Equal("취소 — 개념도를 그대로 두었습니다(서버 호출 없음)", result.controller.StatusText);   // WIR-206
        Assert.Equal(FenceControllerEnd.Left, result.controller.FenceControllerEnd);
        Assert.True(result.cellEsc);
        Assert.Equal("취소 — 개념도를 그대로 두었습니다(서버 호출 없음)", result.Cell);                   // WIR-213
        Assert.Equal(result.chain, result.After);                                               // 그대로
        Assert.False(result.CanUndo);
        Assert.False(result.IsDragging);
    }

    [Fact]
    [Trait("Scenario", "WIR-214")]
    public void should_move_a_focused_concept_cell_between_lanes_with_alt_up_and_alt_down_WIR214()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _, _) = WiringGapRig.Build("SSSS");
            var view = Concept(vm);
            var up = view.HandleKeyDown(Key.System, Key.Up, ModifierKeys.Alt, view.NodeChips[102]);
            var afterUp = (vm.FenceLayout.LaneOf(102), vm.ConceptTitle);
            var down = view.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt, view.NodeChips[102]);
            return (up, afterUp, down, Lane: vm.FenceLayout.LaneOf(102), vm.ConceptTitle);
        });

        Assert.True(result.up);
        Assert.Equal(FenceLane.Upper, result.afterUp.Item1);                                    // 줄 바뀜
        Assert.Contains("위 줄 1", result.afterUp.ConceptTitle);
        Assert.True(result.down);
        Assert.Equal(FenceLane.Lower, result.Lane);
        Assert.Contains("리턴선", result.ConceptTitle);
    }
    #endregion

    #region - 신호등 (WIR-219 · 220) -
    [Fact]
    [Trait("Scenario", "WIR-219")]
    public void should_say_normal_on_the_concept_signal_line_when_the_controller_at_127_0_0_1_answers_WIR219()
    {
        var result = OnSta(() =>
        {
            var ping = new FakePing();
            var (vm, _, _, _) = WiringGapRig.Build("SSS", ping: ping, address: "127.0.0.1");
            var view = View(vm);
            var line = ById<TextBlock>(view, "Devices.Wiring.Fence.Concept.ControllerSignal");
            var before = line.Text;
            vm.PingControllerOnceAsync().GetAwaiter().GetResult();
            return (before, After: line.Text, Hosts: ping.Hosts.ToList(), vm.ControllerSignal);
        });

        Assert.StartsWith("제어기 통신: 모름", result.before);
        Assert.Equal(new[] { "127.0.0.1" }, result.Hosts);
        Assert.Equal(SignalLevel.Ok, result.ControllerSignal);
        Assert.StartsWith("제어기 통신: ", result.After);                                       // `.Concept.ControllerSignal`
        Assert.Contains("정상", result.After);
    }

    [Fact]
    [Trait("Scenario", "WIR-220")]
    public void should_say_no_response_after_the_down_streak_of_failed_pings_to_192_0_2_10_within_15_to_40_seconds_WIR220()
    {
        var result = OnSta(() =>
        {
            var ping = new FakePing();
            for (var i = 0; i < SignalMath.DOWN_STREAK; i++) ping.Next.Enqueue(new PingSample(false, 0));
            var (vm, _, _, _) = WiringGapRig.Build("SSS", ping: ping, address: "192.0.2.10");
            var view = View(vm);
            var line = ById<TextBlock>(view, "Devices.Wiring.Fence.Concept.ControllerSignal");
            var texts = new List<string>();
            for (var i = 0; i < SignalMath.DOWN_STREAK; i++)
            {
                vm.PingControllerOnceAsync().GetAwaiter().GetResult();
                texts.Add(line.Text);
            }
            return (texts, Hosts: ping.Hosts.Distinct().ToList(), vm.ControllerSignal);
        });

        Assert.Equal(new[] { "192.0.2.10" }, result.Hosts);
        // 연속 실패가 차기 전에는 「응답 없음」 상태가 아니다(앞말은 「느림 · 일부 손실」 — 뒤 요약의 '응답 없음'은 평균을 낼 표본이 없다는 뜻)
        Assert.All(result.texts.Take(SignalMath.DOWN_STREAK - 1), t => Assert.StartsWith("제어기 통신: 느림 · 일부 손실", t));
        Assert.StartsWith("제어기 통신: 응답 없음", result.texts[^1]);                          // 「제어기 통신: 응답 없음」
        Assert.Equal(SignalLevel.Down, result.ControllerSignal);
        // 15~40초 — 5초마다 ping · 시간 제한 1초 · 연속 3회 실패 = 첫 판정이 15초에서 18초 사이(여유 40초 안)
        var earliest = SignalMath.DOWN_STREAK * ControllerPingMonitor.INTERVAL.TotalSeconds;
        var latest = SignalMath.DOWN_STREAK * (ControllerPingMonitor.INTERVAL + ControllerPingMonitor.TIMEOUT).TotalSeconds;
        Assert.InRange(earliest, 15, 40);
        Assert.InRange(latest, 15, 40);
    }
    #endregion
}
