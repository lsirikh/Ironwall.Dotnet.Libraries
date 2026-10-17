using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
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

    #region - 9-point grid drag (2026-10-01) -
    /// <summary>격자 칸의 화면 점(끄는 중 · 캔버스 좌표).</summary>
    private static Point ScreenOf(FenceCanvas canvas, FenceGridCell cell) => canvas.WorldToScreen(canvas.SnapPoints.Single(p => p.Cell == cell).At);

    [Fact]
    public void should_show_red_snap_points_and_drop_a_post_sensor_on_the_panel_point_under_the_pointer()
    {
        // 스마트 넷 = 기둥 0 … 3 · 망 3 칸 — 망 1 의 가운데 위(gx 4·1+2 = 6 · gy 2)로
        var target = new FenceGridCell(6, 2);
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var action = canvas.DragAction;
            var points = canvas.SnapPoints.Count;
            var occupied = canvas.SnapPoints.Count(p => p.Occupied);
            var at = ScreenOf(canvas, target);
            canvas.OnPointerMoved(at + new Vector(6, -5));                                   // 반경(24px) 안
            var snapped = canvas.SnapTarget;
            var updates = canvas.OverlayUpdates;
            canvas.OnPointerMoved(at + new Vector(4, -3));                                   // 같은 점
            var sameUpdates = canvas.OverlayUpdates - updates;
            var hot = canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapRing);
            var dim = canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapDotDim);
            var label = canvas.SnapLabel;
            canvas.OnPointerReleased(at + new Vector(4, -3));
            Pump();
            return (action, points, occupied, snapped, sameUpdates, hot, dim, label, Mount: vm.FenceLayout.MountOf(102)!, vm.StatusText,
                    After: canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapDot));
        });

        Assert.Equal(FenceGestureAction.SnapMove, result.action);
        Assert.Equal(3 * 9 + 4 * 2, result.points);                                           // 망 9점 × 3 + 기둥 2점 × 4
        Assert.Equal(3, result.occupied);                                                     // 다른 센서 셋(기둥 위)
        Assert.Equal(target, result.snapped);
        Assert.Equal(0, result.sameUpdates);                                                  // 후보 점이 그대로면 다시 그리지 않는다
        Assert.True(result.hot >= 1);
        Assert.True(result.dim >= 1);
        Assert.Equal("망 2 · 망 위", result.label);
        Assert.Equal((1, FenceMountSpot.PanelTop, FenceColumn.Center), (result.Mount.Panel, result.Mount.Spot, result.Mount.Column));
        Assert.StartsWith("옮김 — ", result.StatusText);
        Assert.Equal(0, result.After);                                                        // 놓으면 빨강 점은 사라진다
    }

    [Fact]
    public void should_leave_the_sensor_where_it_was_when_released_away_from_every_point()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var before = vm.FenceLayout.MountOf(102)!;
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var far = new Point(canvas.ActualWidth / 2, 6);                                    // 하늘 — 점에서 멀다
            canvas.OnPointerMoved(far);
            var snapped = canvas.SnapTarget;
            canvas.OnPointerReleased(far);
            Pump();
            return (before, snapped, After: vm.FenceLayout.MountOf(102)!, vm.CanUndo, vm.StatusText);
        });

        Assert.Null(result.snapped);
        Assert.Equal(result.before, result.After);
        Assert.False(result.CanUndo);
        Assert.Contains("빨강 점", result.StatusText);
    }

    [Fact]
    public void should_put_everything_back_when_escape_cancels_a_grid_drag()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var before = vm.FenceLayout.MountOf(102)!;
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            canvas.OnPointerMoved(ScreenOf(canvas, new FenceGridCell(6, 0)));
            var during = canvas.SnapTarget;
            var handled = canvas.HandleKeyDown(Key.Escape, Key.None, ModifierKeys.None, chip);
            Pump();
            return (before, during, handled, canvas.IsDragging, After: vm.FenceLayout.MountOf(102)!, vm.CanUndo,
                    Dots: canvas.OverlayShapes.Count(s => s.Ink is FenceInk.SnapDot or FenceInk.SnapDotDim or FenceInk.SnapRing), vm.StatusText, Opacity: chip.Opacity);
        });

        Assert.Equal(new FenceGridCell(6, 0), result.during);
        Assert.True(result.handled);
        Assert.False(result.IsDragging);
        Assert.Equal(result.before, result.After);
        Assert.False(result.CanUndo);                                                         // 되돌리기 장면도 쌓지 않는다
        Assert.Equal(0, result.Dots);
        Assert.Contains("취소", result.StatusText);
        Assert.Equal(1, result.Opacity);
    }

    [Fact]
    public void should_move_the_whole_selection_by_the_same_grid_offset_in_one_undo_step()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            vm.FenceSelectSensors(new[] { 102, 103 });
            Pump();
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var at = ScreenOf(canvas, new FenceGridCell(5, 2));                                // 기둥 1(gx 4) → 망 1 왼쪽 위(gx 5) · Δ(+1, 0)
            canvas.OnPointerMoved(at);
            var members = canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapRing);
            var label = canvas.SnapLabel;
            canvas.OnPointerReleased(at);
            Pump();
            var moved = (vm.FenceLayout.MountOf(102)!, vm.FenceLayout.MountOf(103)!);
            vm.Undo();
            return (members, label, moved, Back: (vm.FenceLayout.MountOf(102)!.Spot, vm.FenceLayout.MountOf(103)!.Spot));
        });

        Assert.True(result.members >= 2);                                                     // 포인터 아래 + 함께 끈 센서의 점
        Assert.EndsWith("· 2대", result.label);
        Assert.Equal((1, FenceMountSpot.PanelTop, FenceColumn.Left), (result.moved.Item1.Panel, result.moved.Item1.Spot, result.moved.Item1.Column));
        Assert.Equal((2, FenceMountSpot.PanelTop, FenceColumn.Left), (result.moved.Item2.Panel, result.moved.Item2.Spot, result.moved.Item2.Column));
        Assert.Equal((FenceMountSpot.PostTop, FenceMountSpot.PostTop), result.Back);           // 되돌리기 한 번
    }

    [Fact]
    public void should_refuse_a_group_move_when_any_member_would_land_off_the_grid()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            vm.FenceSelectSensors(new[] { 103, 104 });
            Pump();
            var chip = canvas.SensorChips[103];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var at = ScreenOf(canvas, new FenceGridCell(11, 2));                               // 103: 기둥 2(gx 8) → gx 11 · 104: 기둥 3(gx 12) → gx 15(없음)
            canvas.OnPointerMoved(at);
            var label = canvas.SnapLabel;
            var blocked = canvas.OverlayShapes.Count(s => s.Ink == FenceInk.SnapBlocked);
            canvas.OnPointerReleased(at);
            Pump();
            return (label, blocked, M103: vm.FenceLayout.MountOf(103)!.Spot, M104: vm.FenceLayout.MountOf(104)!.Spot, vm.CanUndo, vm.StatusText);
        });

        Assert.Equal("놓을 수 없음 — 1대 갈 점 없음", result.label);
        Assert.Equal(1, result.blocked);                                                      // 경고 모양(빈 마름모)
        Assert.Equal((FenceMountSpot.PostTop, FenceMountSpot.PostTop), (result.M103, result.M104));
        Assert.False(result.CanUndo);
        Assert.Contains("놓지 않았습니다", result.StatusText);
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
        Assert.Equal((FenceMountSpot.PanelTop, FenceLane.Lower), result.afterUp);             // 한 줄 위(가운데 → 위)
        Assert.True(result.nudge);
        Assert.Equal(-0.1, result.offset, 6);
        Assert.Equal((FenceMountSpot.PanelTop, FenceLane.Lower), result.afterNudge);       // 미세 높이는 단계를 바꾸지 않는다
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

    #region - R · Shift+R · F (센서 방향 2026-10-01) -
    [Fact]
    public void should_rotate_with_r_and_shift_r_and_flip_the_mount_side_with_f_on_the_focused_sensor()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            chip.Focus();
            var r = canvas.HandleKeyDown(Key.R, Key.None, ModifierKeys.None, chip);
            Pump();
            var afterR = vm.Board.YawOf(102);
            canvas.HandleKeyDown(Key.R, Key.None, ModifierKeys.Shift, canvas.SensorChips[102]);
            canvas.HandleKeyDown(Key.R, Key.None, ModifierKeys.Shift, canvas.SensorChips[102]);
            Pump();
            var afterShiftR = vm.Board.YawOf(102);
            var ctrlR = canvas.HandleKeyDown(Key.R, Key.None, ModifierKeys.Control, canvas.SensorChips[102]);
            var f = canvas.HandleKeyDown(Key.F, Key.None, ModifierKeys.None, canvas.SensorChips[102]);
            Pump();
            return (r, afterR, afterShiftR, ctrlR, f, Side: vm.Board.FacingOf(102), Others: vm.Board.YawOf(101));
        });

        Assert.True(result.r);
        Assert.Equal(WiringYaw.Along, result.afterR);
        Assert.Equal(WiringYaw.Against, result.afterShiftR);                                 // 90 → 0 → 270
        Assert.False(result.ctrlR);                                                           // Ctrl+R 은 흘려보낸다
        Assert.True(result.f);
        Assert.Equal(WiringFacing.Back, result.Side);
        Assert.Equal(WiringYaw.Away, result.Others);
    }
    #endregion

    #region - Ctrl+←/→ (헤디드 r22) -
    [Fact]
    public void should_move_to_the_next_grid_point_with_ctrl_right_in_the_fence_view_and_the_concept()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            chip.Focus();
            var handled = canvas.HandleKeyDown(Key.Right, Key.None, ModifierKeys.Control, chip);
            Pump();
            var afterCanvas = vm.FenceLayout.MountOf(102)!;
            var status = vm.StatusText;

            var concept = Descendants<Consoles.Wiring.Concept.FenceConceptView>(Window.GetWindow(canvas)!).Single();
            var node = concept.NodeChips[103];
            node.Focus();
            var conceptHandled = concept.HandleKeyDown(Key.Right, Key.None, ModifierKeys.Control, node);
            Pump();
            return (handled, afterCanvas, status, conceptHandled, After103: vm.FenceLayout.MountOf(103)!);
        });

        // 기둥 1 위 → 같은 줄(위)의 다음 점 = 망 1(번호 2) 왼쪽 위 · 103 은 기둥 2 위 → 망 2 왼쪽 위
        Assert.True(result.handled);
        Assert.Equal((1, FenceMountSpot.PanelTop, FenceColumn.Left), (result.afterCanvas.Panel, result.afterCanvas.Spot, result.afterCanvas.Column));
        Assert.StartsWith("옮김 — ", result.status);
        Assert.True(result.conceptHandled);
        Assert.Equal((2, FenceMountSpot.PanelTop, FenceColumn.Left), (result.After103.Panel, result.After103.Spot, result.After103.Column));
    }

    [Fact]
    public void should_keep_alt_right_without_shift_as_the_lane_step_in_the_concept()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var concept = Descendants<Consoles.Wiring.Concept.FenceConceptView>(Window.GetWindow(canvas)!).Single();
            var node = concept.NodeChips[101];
            node.Focus();
            concept.HandleKeyDown(Key.System, Key.Right, ModifierKeys.Alt, node);
            Pump();
            var laneStep = vm.FenceChain.Keys.ToList();
            vm.Undo();
            Pump();
            node = concept.NodeChips[101];
            concept.HandleKeyDown(Key.System, Key.Right, ModifierKeys.Alt | ModifierKeys.Shift, node);
            Pump();
            return (laneStep, vm.StatusText, Panel: vm.FenceLayout.MountOf(101)!.Panel);
        });

        Assert.Equal(new[] { 102, 101, 103, 104 }, result.laneStep);                      // Alt+→ = 줄 안 한 칸
        Assert.Equal(1, result.Panel);                                                     // Alt+Shift+→ = 다른 망(보조 길)
        Assert.StartsWith("옮김 — ", result.StatusText);
    }
    #endregion

    #region - Chips on one panel never overlap on screen (헤디드 r22) -
    /// <summary>스마트 <paramref name="count"/> + 2 대 · 망 1 을 벽돌로 바꾸고 101 … 을 그 담 위에(다섯 자리 번호).</summary>
    private static WiringViewModel OnBrick(int count)
    {
        var total = count + 2;
        var seeds = Enumerable.Range(0, total).Select(i => new WiringSensorSeed(101 + i, i + 1, new SensorFacts(83024 + i, $"LRT-{83024 + i}", "SmartSensor2", "북측"),
            new WiringPlacement(1, i + 1))).ToList();
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL", "10.99.7.1", "SmartController"), seeds, new[] { "SmartSensor2" },
            null, new WiringFakeDialogs(), fence: new WiringFenceContext(null, new FakeFenceStore(), null));
        vm.Board.ApplyFenceEdit(l =>
        {
            var panels = l.Panels.Select((p, i) => i == 1 ? FencePanelSpec.Default(Ironwall.Dotnet.Libraries.Enums.EnumFenceStyle.Brick, 4.5) : p).ToList();
            var mounts = l.Mounts.ToDictionary(p => p.Key, p => p.Key < 101 + count ? new SensorMountSpec(1, FenceMountSpot.WallFace) : p.Value);
            return l.With(panels, mounts);
        });
        return vm;
    }

    [Theory]
    [InlineData(3, 0.6)]
    [InlineData(3, 0.75)]
    [InlineData(3, 0.9)]
    [InlineData(5, 0.6)]
    [InlineData(5, 0.9)]
    [InlineData(5, 0.35)]
    public void should_never_overlap_sensor_chips_on_one_brick_panel_at_the_headed_zoom(int count, double zoom)
    {
        var rects = OnWindow(OnBrick(count), (vm, canvas) =>
        {
            canvas.SetView(zoom, new Vector(40, 300));
            Pump();
            return canvas.SensorChips.Values.Where(c => c.IsVisible)
                .Select(c => (c.Key, Rect: new Rect(canvas.WorldToScreen(new Point(Canvas.GetLeft(c), Canvas.GetTop(c))), new Size(c.Width * canvas.Scale, c.Height * canvas.Scale))))
                .OrderBy(t => t.Rect.X).ToList();
        });

        Assert.True(rects.Count >= count, $"보이는 칩 {rects.Count}");
        for (var i = 0; i < rects.Count; i++)
            for (var j = i + 1; j < rects.Count; j++)
            {
                var overlap = Rect.Intersect(rects[i].Rect, rects[j].Rect);
                Assert.True(overlap.IsEmpty || overlap.Width < 0.5 || overlap.Height < 0.5,
                    $"칩 {rects[i].Key} · {rects[j].Key} 이 겹친다 — {rects[i].Rect} / {rects[j].Rect} (배율 {zoom})");
            }
    }

    [Fact]
    public void should_spread_chip_rects_by_the_widest_chip_plus_the_gap_and_leave_apart_chips_alone()
    {
        var xs = FenceChipSpacing.Spread(new[] { (10.0, -20.0, 40.0), (12.0, -20.0, 40.0), (14.0, -18.0, 36.0), (400.0, -20.0, 40.0) }, 4);

        Assert.True(xs[1] - 20 >= xs[0] - 20 + 44 - 1e-9);
        Assert.True(xs[2] - 18 >= xs[1] - 20 + 44 - 1e-9);
        Assert.Equal(400.0, xs[3]);                                                        // 떨어진 칩은 그대로
        Assert.Equal(12.0, (xs[0] + xs[1] + xs[2]) / 3, 6);                                // 무리 가운데는 제자리 평균
    }
    #endregion

    #region - Number plate is not a handle (2026-10-01) -
    [Fact]
    public void should_not_select_or_grab_a_sensor_when_its_number_plate_is_pressed()
    {
        // 사용자: "번호표 … 아이콘 하단으로 내려라 그리고 그건 adorner에 안잡히게 해라" — 번호판은 누르는 자리가 아니다
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var chip = canvas.SensorChips[102];
            var picture = chip.Picture!;
            var plate = picture.Shapes.Single(s => s.Ink == FenceInk.Plate);
            var anchor = new Point(Canvas.GetLeft(chip) - picture.Hit.X, Canvas.GetTop(chip) - picture.Hit.Y);
            var plateCentre = new Point(anchor.X + (plate.Points[0].X + plate.Points[1].X) / 2, anchor.Y + (plate.Points[0].Y + plate.Points[1].Y) / 2);
            var screen = canvas.WorldToScreen(plateCentre);
            var hit = System.Windows.Media.VisualTreeHelper.HitTest(canvas, screen)?.VisualHit;
            FenceChip? under = null;
            for (DependencyObject? d = hit; d is not null; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
                if (d is FenceChip c) { under = c; break; }
            var bodyHit = System.Windows.Media.VisualTreeHelper.HitTest(canvas, canvas.ScreenCenterOf(chip))?.VisualHit;
            canvas.OnPointerPressed(screen, under);
            canvas.OnPointerReleased(screen);
            Pump();
            return (Under: under?.Kind, UnderKey: under?.Key, BodyIsChip: ReferenceEquals(bodyHit, chip), Selected: vm.IsFenceSelected(102),
                    PlateInside: new Rect(Canvas.GetLeft(chip), Canvas.GetTop(chip), chip.Width, chip.Height).Contains(plateCentre));
        });

        Assert.False(result.Under == FenceChipKind.Sensor && result.UnderKey == 102, $"번호판 아래 {result.Under} {result.UnderKey}");
        Assert.True(result.BodyIsChip);                                                       // 몸은 그대로 누르는 자리
        Assert.False(result.Selected);
        Assert.False(result.PlateInside);                                                     // 칩 요소(UIA 사각형)는 몸만
    }
    #endregion

    #region - Red snap points on screen (헤디드 r23 SC-FEN-025) -
    /// <summary>창 안의 어도너 층까지 그린 그림에서 StatusCritical(#C62121 · 라이트) 빛깔 픽셀 수.</summary>
    private static int CriticalPixels(Window window)
    {
        var root = Descendants<System.Windows.Documents.AdornerDecorator>(window).First();
        var w = (int)Math.Ceiling(root.ActualWidth);
        var h = (int)Math.Ceiling(root.ActualHeight);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(root);
        var pixels = new byte[w * h * 4];
        bitmap.CopyPixels(pixels, w * 4, 0);
        var count = 0;
        for (var i = 0; i < pixels.Length; i += 4)
            if (Math.Abs(pixels[i + 2] - 0xC6) <= 20 && Math.Abs(pixels[i + 1] - 0x21) <= 20 && Math.Abs(pixels[i] - 0x21) <= 20 && pixels[i + 3] > 200) count++;
        return count;
    }

    private static (int Before, int During, int After) DragAndCountRed(FenceCanvas canvas)
    {
        var window = Window.GetWindow(canvas)!;
        var before = CriticalPixels(window);
        var chip = canvas.SensorChips[102];
        var start = canvas.ScreenCenterOf(chip);
        canvas.OnPointerPressed(start, chip);
        canvas.OnPointerMoved(start + new Vector(12, 0));
        canvas.OnPointerMoved(start + new Vector(30, 40));                                    // 점에서 먼 곳 — 고리 없이 점만
        Pump();
        var during = CriticalPixels(window);
        canvas.HandleKeyDown(Key.Escape, Key.None, ModifierKeys.None, chip);
        Pump();
        return (before, during, CriticalPixels(window));
    }

    [Fact]
    public void should_paint_the_red_snap_points_on_screen_while_a_sensor_is_dragged()
    {
        var (before, during, after) = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) => DragAndCountRed(canvas));

        Assert.True(during > before + 300, $"빨강 픽셀 전 {before} · 끄는 중 {during}");          // 점 35개(반지름 4px)
        Assert.True(during > after + 300, $"놓은 뒤 {after}");
    }

    [Fact]
    public void should_still_paint_the_red_snap_points_after_the_fence_view_left_the_tree_and_came_back()
    {
        // 헤디드 r23: 보기 전환 · 칸 접기로 펜스 보기가 트리에서 잠깐 빠지면 어도너 층이 덧그림 어도너를 스스로 떼어 낸다 —
        // 캔버스는 예전 어도너를 쥐고 있어 다시 붙이지 않았다(끄는 동안 고스트만 보이고 빨강 점 · 알약이 안 보였다).
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var window = Window.GetWindow(canvas)!;
            var view = window.Content;
            window.Content = null;
            Pump();
            window.Content = view;
            Pump();
            return DragAndCountRed(canvas);
        });

        Assert.True(result.During > result.Before + 300, $"빨강 픽셀 전 {result.Before} · 끄는 중 {result.During}");
    }
    #endregion

    #region - Multi-select drag (헤디드 r23 SC-FEN-026) -
    [Fact]
    public void should_drag_both_sensors_when_the_second_was_added_with_ctrl_click()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var a = canvas.ScreenCenterOf(canvas.SensorChips[102]);
            canvas.OnPointerPressed(a, canvas.SensorChips[102]);
            canvas.OnPointerReleased(a);
            Pump();
            var b = canvas.ScreenCenterOf(canvas.SensorChips[103]);
            canvas.OnPointerPressed(b, canvas.SensorChips[103], ctrl: true);
            canvas.OnPointerReleased(b);
            Pump();
            var selected = vm.FenceSelectedKeys.OrderBy(k => k).ToList();
            var moved = DragSelectionOf102(vm, canvas);
            return (selected, moved);
        });

        Assert.Equal(new[] { 102, 103 }, result.selected);
        AssertMovedTogether(result.moved);
    }

    [Fact]
    public void should_drag_both_sensors_when_the_second_was_added_with_ctrl_space_after_moving_focus()
    {
        // 헤디드 r23 SC-FEN-026: A 를 고르고 B 로 포커스를 옮겨(포커스 = 선택 → B 하나) Ctrl+Space — 예전에는 B 를 빼 선택이 비었고 A 만 끌렸다.
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            var a = canvas.ScreenCenterOf(canvas.SensorChips[102]);
            canvas.OnPointerPressed(a, canvas.SensorChips[102]);
            canvas.OnPointerReleased(a);
            Pump();
            canvas.SensorChips[103].Focus();
            Pump();
            var focused = vm.FenceSelectedKeys.ToList();
            var handled = canvas.HandleKeyDown(Key.Space, Key.None, ModifierKeys.Control, canvas.SensorChips[103]);
            Pump();
            var selected = vm.FenceSelectedKeys.OrderBy(k => k).ToList();
            canvas.SensorChips[102].Focus();                                                   // 고른 것 사이를 다녀도 풀리지 않는다
            Pump();
            var afterTab = vm.FenceSelectedKeys.OrderBy(k => k).ToList();
            var moved = DragSelectionOf102(vm, canvas);
            return (focused, handled, selected, afterTab, moved);
        });

        Assert.Equal(new[] { 103 }, result.focused);
        Assert.True(result.handled);
        Assert.Equal(new[] { 102, 103 }, result.selected);
        Assert.Equal(new[] { 102, 103 }, result.afterTab);
        AssertMovedTogether(result.moved);
    }

    [Fact]
    public void should_toggle_the_focused_sensor_off_with_ctrl_space_when_focus_did_not_just_replace_the_selection()
    {
        var result = OnWindow(WiringFenceHeightTests.Build("SSSS"), (vm, canvas) =>
        {
            vm.FenceSelectSensors(new[] { 102, 103 });
            Pump();
            canvas.SensorChips[103].Focus();
            Pump();
            canvas.HandleKeyDown(Key.Space, Key.None, ModifierKeys.Control, canvas.SensorChips[103]);
            Pump();
            return vm.FenceSelectedKeys.ToList();
        });

        Assert.Equal(new[] { 102 }, result);
    }

    private static (SensorMountSpec A, SensorMountSpec B) DragSelectionOf102(WiringViewModel vm, FenceCanvas canvas)
    {
        var chip = canvas.SensorChips[102];
        var start = canvas.ScreenCenterOf(chip);
        canvas.OnPointerPressed(start, chip);
        canvas.OnPointerMoved(start + new Vector(12, 0));
        var at = ScreenOf(canvas, new FenceGridCell(5, 2));                                    // 기둥 1(gx 4) → 망 1 왼쪽 위(gx 5) · Δ(+1, 0)
        canvas.OnPointerMoved(at);
        canvas.OnPointerReleased(at);
        Pump();
        return (vm.FenceLayout.MountOf(102)!, vm.FenceLayout.MountOf(103)!);
    }

    private static void AssertMovedTogether((SensorMountSpec A, SensorMountSpec B) moved)
    {
        Assert.Equal((1, FenceMountSpot.PanelTop, FenceColumn.Left), (moved.A.Panel, moved.A.Spot, moved.A.Column));
        Assert.Equal((2, FenceMountSpot.PanelTop, FenceColumn.Left), (moved.B.Panel, moved.B.Spot, moved.B.Column));
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
