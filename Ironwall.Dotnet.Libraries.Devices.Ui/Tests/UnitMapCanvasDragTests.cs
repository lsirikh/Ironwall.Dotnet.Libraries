using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Xunit;
using H = Ironwall.Dotnet.Libraries.Devices.Ui.Tests.UnitMapCanvasHarness;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-22 — 노드 캡처 드래그 기계 · 대상 표시(STA 헤드리스, FR-17 · FR-28~30 · FR-33 · NFR-04).
/// 시나리오: C001~051(끝 · 자동 팬 · 월드 Δ) · D(드롭 판정 표시) · H(숨긴 레이어) · X(겹침 · z) · G(데드존).
/// ISSUE-11(월드 Δ) · 12(끄는 중 줌 무시, D-2026-09-27-215b6d) · 13(밖 · 오버레이 위 = 취소) · 14(자기 · 예하 제외) · 15(현 상위 = 막힘).
/// </summary>
public class UnitMapCanvasDragTests
{
    #region - 데드존 · 시작 -
    [Fact]
    public void should_only_select_when_released_at_7_9_and_start_dragging_at_8_1()   // SIM-G(데드존) · FR-28
    {
        var (select, drag) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (node, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(7.9, 0));
            var pressedNotDragging = (canvas.IsGestureActive, canvas.IsDragging);
            canvas.OnPointerReleased(at + new Vector(7.9, 0));
            var first = fake.Calls.ToList();

            fake.Calls.Clear();
            canvas.OnPointerPressed(at, node, spaceHeld: false);
            canvas.OnPointerMoved(at + new Vector(8.1, 0));
            var dragging = canvas.IsDragging;
            canvas.OnPointerReleased(at + new Vector(8.1, 0));
            return ((pressedNotDragging, first), (dragging, fake.Calls.ToList()));
        });

        var id = H.IdOf("7중대");
        Assert.Equal((true, false), select.pressedNotDragging);          // _pressed ≠ _dragging
        Assert.Equal(new[] { $"select:{id}" }, select.first);
        Assert.True(drag.dragging);
        Assert.Equal(new[] { $"begin:{id}", $"complete:{id}" }, drag.Item2);
    }

    [Fact]
    public void should_not_drag_an_L0_node_but_pan_instead()                              // FR-13 · SIM-G
    {
        var (dragging, panned, calls) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.2);
            var before = canvas.PanOffset;
            canvas.OnPointerMoved(at + new Vector(30, 0));
            var d = canvas.IsDragging;
            var p = canvas.PanOffset - before;
            canvas.OnPointerReleased(at + new Vector(30, 0));
            return (d, p, fake.Calls.ToList());
        });

        Assert.False(dragging);
        Assert.Equal(new Vector(30, 0), panned);
        Assert.Empty(calls);
    }
    #endregion

    #region - 월드 Δ (ISSUE-11) -
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.6)]
    public void should_report_world_delta_as_screen_delta_divided_by_scale_when_dropped(double scale)    // D-02 · FR-28
    {
        var drop = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", scale);
            canvas.OnPointerMoved(at + new Vector(50, 20));
            canvas.OnPointerMoved(at + new Vector(100, 40));
            canvas.OnPointerReleased(at + new Vector(100, 40));
            return fake.Drops.Single();
        });

        Assert.Equal(100 / scale, drop.WorldDx, 9);
        Assert.Equal(40 / scale, drop.WorldDy, 9);
        Assert.False(drop.Ctrl);
    }

    [Fact]
    public void should_include_auto_pan_in_the_world_delta_when_the_pointer_waits_in_the_edge_band()     // SIM-C048 · ISSUE-11
    {
        var (panned, drop) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            var edge = new Point(at.X, 16);                           // 위 띠 32 의 가운데 → 240 px/s
            canvas.OnPointerMoved(edge);
            var before = canvas.PanOffset;
            canvas.AutoPanTick(TimeSpan.FromSeconds(1));
            var p = canvas.PanOffset - before;
            canvas.OnPointerReleased(edge);
            return (p, fake.Drops.Single());
        });

        Assert.Equal(new Vector(0, 240), panned);                     // 시간 기반(프레임 무관)
        Assert.Equal(((16 - 300) - 240) / 0.5, drop.WorldDy, 6);      // 화면 Δ/배율 + 자동 팬 240px/배율 = −1048
    }

    [Theory]
    [InlineData(300, 0)]             // 가운데 — 띠 밖
    [InlineData(600 - 16, -240)]     // 아래 띠 가운데 — 그림은 위로
    [InlineData(600 - 1, -465)]      // 거의 가장자리 — 480 × 31/32
    public void should_auto_pan_at_the_time_based_kernel_velocity_when_the_pointer_is_in_the_band(double y, double expected)   // FR-17 · SIM-C048~050
    {
        var dy = H.Run(canvas =>
        {
            H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(new Point(at.X - 60, y));
            var before = canvas.PanOffset;
            canvas.AutoPanTick(TimeSpan.FromSeconds(1));
            return (canvas.PanOffset - before).Y;
        });

        Assert.Equal(expected, dy, 6);
        Assert.Equal(-DragMath.AutoScrollVelocity(y, 600), dy, 6);
    }
    #endregion

    #region - 취소 · 끝 순서 (FR-33) -
    [Fact]
    public void should_cancel_with_zero_drops_and_consume_escape_only_while_dragging()     // SIM-C001 · C015 · C016
    {
        var (dragEsc, idleEsc, calls, session) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            fake.HandleKeyResult = _ => false;
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(40, 0));
            var consumed = canvas.HandleKeyDown(Key.Escape, Key.None, Key.None, ModifierKeys.None, canvas);
            var held = canvas.HoldsDragSession;
            canvas.OnPointerReleased(at + new Vector(40, 0));

            var (_, at2) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at2 + new Vector(4, 0));            // 누르기만(4 DIU)
            var idle = canvas.HandleKeyDown(Key.Escape, Key.None, Key.None, ModifierKeys.None, canvas);
            return (consumed, idle, fake.Calls.ToList(), held);
        });

        var id = H.IdOf("7중대");
        Assert.True(dragEsc);
        Assert.False(idleEsc);                                        // 누르기만 한 Esc 는 통과(기존 선택 해제)
        Assert.False(session);
        Assert.Equal(new[] { $"begin:{id}", $"cancel:{id}", "key:Escape:False" }, calls);   // 놓기 0
    }

    [Fact]
    public void should_cancel_when_mouse_capture_is_lost_mid_drag()                      // SIM-C002
    {
        var calls = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(40, 0));
            canvas.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
            return fake.Calls.ToList();
        });

        Assert.Equal(new[] { $"begin:{H.IdOf("7중대")}", $"cancel:{H.IdOf("7중대")}" }, calls);
    }

    [Fact]
    public void should_cancel_when_the_host_window_is_deactivated_mid_drag()             // SIM-C003 · D-04
    {
        var (calls, dragging) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(40, 0));
            canvas.OnHostDeactivated(null, EventArgs.Empty);
            return (fake.Calls.ToList(), canvas.IsDragging);
        });

        Assert.False(dragging);
        Assert.Equal($"cancel:{H.IdOf("7중대")}", calls.Last());
    }

    [Fact]
    public void should_finish_in_the_contract_order_flags_visuals_unsubscribe_capture_then_notify()   // LabelAdorner 계약 · DF
    {
        var trace = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.FinishTrace = step => fake.Calls.Add("t:" + step);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(40, 0));
            canvas.OnPointerReleased(at + new Vector(40, 0));
            return fake.Calls.Where(c => !c.StartsWith("begin")).ToList();
        });

        Assert.Equal(new[] { "t:flags", "t:visuals", "t:unsubscribe", "t:capture", $"complete:{H.IdOf("7중대")}", "t:notify" }, trace);
    }

    [Fact]
    public void should_cancel_when_released_outside_the_canvas()                         // SIM-C010 · C024 · C040 · ISSUE-13
    {
        var (outside, chip, calls) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(new Point(-30, at.Y));
            var o = canvas.IsPointerOutside;
            var c = canvas.HoverChipText;
            var before = canvas.PanOffset;
            canvas.AutoPanTick(TimeSpan.FromSeconds(1));              // 밖에서는 밀지 않는다
            canvas.OnPointerReleased(new Point(-30, at.Y));
            return (o && canvas.PanOffset == before, c, fake.Calls.ToList());
        });

        Assert.True(outside);
        Assert.Equal(UnitMapCanvas.OUTSIDE_CHIP_TEXT, chip);
        Assert.Equal($"cancel:{H.IdOf("7중대")}", calls.Last());
        Assert.DoesNotContain(calls, c => c.StartsWith("complete"));
    }

    [Fact]
    public void should_cancel_on_the_hud_but_keep_auto_panning_under_it_and_accept_the_band_beside_it()   // ISSUE-13 · SIM-X004 · must-cover 1
    {
        var result = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var hud = H.ById<Button>(canvas, UnitMapCanvas.ID_FIT)!;
            var hudCenter = hud.TranslatePoint(new Point(hud.ActualWidth / 2, hud.ActualHeight / 2), canvas);

            var (node, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(20, 0));
            var overlayTransparent = !canvas.OverlayLayer.IsHitTestVisible;
            canvas.OnPointerMoved(hudCenter);
            var over = canvas.IsPointerOverOverlay;
            var before = canvas.PanOffset;
            canvas.AutoPanTick(TimeSpan.FromSeconds(0.5));            // HUD 아래도 띠 — 계속 민다
            var panned = canvas.PanOffset != before;
            canvas.OnPointerReleased(hudCenter);
            var first = fake.Calls.Last();
            var restored = canvas.OverlayLayer.IsHitTestVisible;

            // 같은 아래 띠의 가운데(오버레이 없음)에서 놓으면 위치 놓기다.
            var (_, at2) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(new Point(at2.X - 150, 600 - 10));
            canvas.AutoPanTick(TimeSpan.FromSeconds(0.2));
            canvas.OnPointerReleased(new Point(at2.X - 150, 600 - 10));
            return (overlayTransparent, over, panned, first, restored, second: fake.Calls.Last());
        });

        var id = H.IdOf("7중대");
        Assert.True(result.overlayTransparent);
        Assert.True(result.over);
        Assert.True(result.panned);
        Assert.Equal($"cancel:{id}", result.first);
        Assert.True(result.restored);
        Assert.Equal($"complete:{id}", result.second);
    }
    #endregion

    #region - 대상 표시(FR-30 · IMPL-39) -
    [Fact]
    public void should_paint_every_candidate_once_at_drag_start_and_not_reclassify_while_moving()   // NFR-04 · FR-30
    {
        var result = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(20, 0));
            var atStart = fake.ClassifyCalls;
            for (var i = 0; i < 10; i++) canvas.OnPointerMoved(at + new Vector(20 + i * 3, i));
            var states = canvas.Nodes.ToDictionary(n => n.UnitId, n => n.DropState);
            var redraws = canvas.RenderCount;
            canvas.OnPointerMoved(at + new Vector(60, 30));
            return (atStart, after: fake.ClassifyCalls, states, redraws, redrawAfter: canvas.RenderCount);
        });

        Assert.Equal(200 - 6 + 1, result.atStart);                    // 끄는 편제(7중대 + 소초 5) 빼고 + 빈 곳 1
        Assert.Equal(result.atStart, result.after);
        Assert.Equal(result.redraws, result.redrawAfter);             // 정적 층 재그림 0
        var s = result.states;
        Assert.Equal(UnitMapNodeDropState.Origin, s[H.IdOf("7중대")]);
        Assert.Equal(UnitMapNodeDropState.Origin, s[H.IdOf("71소초")]);
        Assert.Equal(UnitMapNodeDropState.Blocked, s[H.IdOf("2대대")]);            // 현 상위 = 막힘(D-2026-09-27-6615ba)
        Assert.Equal(UnitMapNodeDropState.ParentCandidate, s[H.IdOf("3대대")]);
        Assert.Equal(UnitMapNodeDropState.ParentCandidate, s[H.IdOf("1연대")]);    // 건너뛰기 허용
        Assert.Equal(UnitMapNodeDropState.AdjoinCandidate, s[H.IdOf("9중대")]);
        Assert.Equal(UnitMapNodeDropState.Blocked, s[H.IdOf("8중대")]);            // 이미 인접
        Assert.Equal(UnitMapNodeDropState.Blocked, s[H.IdOf("91소초")]);           // 하위 제대
    }

    [Fact]
    public void should_show_the_blocked_chip_with_the_reason_when_hovering_the_current_parent()    // D-2026-09-27-6615ba · ISSUE-15
    {
        var (hover, chip, blocked, state) = H.Run(canvas =>
        {
            H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(20, 0));
            canvas.OnPointerMoved(H.ScreenOf(canvas, H.IdOf("2대대")));
            return (canvas.HoverUnitId, canvas.HoverChipText, canvas.HoverChipIsBlocked, H.Node(canvas, "2대대").DropState);
        });

        Assert.Equal(H.IdOf("2대대"), hover);
        Assert.True(blocked);
        Assert.Contains("이미", chip);
        Assert.Equal(UnitMapNodeDropState.Hover, state);
    }

    [Fact]
    public void should_show_the_move_chip_and_a_solid_preview_when_hovering_a_parent_candidate()   // SB S6
    {
        var (chip, blocked, preview, dotted) = H.Run(canvas =>
        {
            H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(20, 0));
            canvas.OnPointerMoved(H.ScreenOf(canvas, H.IdOf("3대대")));
            return (canvas.HoverChipText, canvas.HoverChipIsBlocked, canvas.IsPreviewVisible, canvas.IsPreviewDotted);
        });

        Assert.Equal("3대대 밑으로 옮기기", chip);
        Assert.False(blocked);
        Assert.True(preview);
        Assert.False(dotted);
    }

    [Fact]
    public void should_keep_adjacency_classification_and_its_dotted_preview_when_the_adjacency_layer_is_hidden_and_toggled_mid_drag()  // SIM-H005 · must-cover 4
    {
        var result = H.Run(canvas =>
        {
            H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(20, 0));
            var target = H.ScreenOf(canvas, H.IdOf("9중대"));
            canvas.OnPointerMoved(target);
            var hidden = (canvas.IsPreviewVisible, canvas.IsPreviewDotted, canvas.HoverChipText, canvas.LineLayer.DrawnAdjacency);

            canvas.Scene = canvas.Scene with { Layers = new UnitMapLayers(Hierarchy: false, Adjacency: true) };   // 끄는 중 토글
            canvas.OnPointerMoved(target + new Vector(1, 0));
            return (hidden, stillDragging: canvas.IsDragging, afterToggle: canvas.IsPreviewVisible,
                    hoverState: H.Node(canvas, "9중대").DropState, parentState: H.Node(canvas, "3대대").DropState);
        }, H.Scene(new UnitMapLayers(Hierarchy: true, Adjacency: false)));

        Assert.True(result.hidden.IsPreviewVisible);
        Assert.True(result.hidden.IsPreviewDotted);
        Assert.StartsWith("9중대", result.hidden.HoverChipText);
        Assert.Equal(0, result.hidden.DrawnAdjacency);                // 인접선 층은 숨었지만 판정 · 미리보기는 산다
        Assert.True(result.stillDragging);
        Assert.True(result.afterToggle);
        Assert.Equal(UnitMapNodeDropState.Hover, result.hoverState);
        Assert.Equal(UnitMapNodeDropState.ParentCandidate, result.parentState);   // 칠한 후보가 유지된다
    }

    [Fact]
    public void should_float_one_shadowed_copy_with_the_subtree_chip_and_restore_everything_after_the_drop()   // SB S5 · NFR-05 · R-15
    {
        var result = H.Run(canvas =>
        {
            H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(40, 10));
            var shadows = H.Descendants(canvas).OfType<UIElement>().Count(e => e.Effect is DropShadowEffect);
            var chip = canvas.SubtreeChipText;
            var ghost = canvas.Ghost is not null;
            canvas.OnPointerReleased(at + new Vector(40, 10));
            return (shadows, chip, ghost, after: canvas.Nodes.All(n => n.DropState == UnitMapNodeDropState.None),
                    ghostGone: canvas.Ghost is null && canvas.DragLayer.Children.Count == 0, cursor: canvas.ReadLocalValue(FrameworkElement.CursorProperty));
        });

        Assert.Equal(1, result.shadows);                              // 그림자는 끌리는 사본 하나만
        Assert.Equal("예하 5 함께", result.chip);
        Assert.True(result.ghost);
        Assert.True(result.after);
        Assert.True(result.ghostGone);
        Assert.Equal(DependencyProperty.UnsetValue, result.cursor);   // 로컬 커서 ClearValue
    }

    [Fact]
    public void should_hold_one_drag_session_through_the_drag_and_ignore_zoom_meanwhile()       // SIM-C006~008 · D-2026-09-27-215b6d
    {
        var result = H.Run(canvas =>
        {
            H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.7);
            canvas.OnPointerMoved(at + new Vector(40, 0));
            var scale = canvas.Scale;
            canvas.OnWheel(3, at);
            ((H.FakeClock)canvas.Clock).Advance(40);
            canvas.FlushWheel();
            canvas.HandleKeyDown(Key.Add, Key.None, Key.None, ModifierKeys.None, canvas);
            canvas.ZoomStep(+1);
            return (session: canvas.HoldsDragSession && DragSession.IsActive, same: canvas.Scale == scale, canvas.IsDragging, level: canvas.Level);
        });

        Assert.True(result.session);
        Assert.True(result.same);                                     // 0.7 → 0.84 였다면 단계가 바뀌어 끌리는 노드가 갈렸다
        Assert.True(result.IsDragging);
        Assert.Equal(UnitMapLevel.L1, result.level);
    }

    [Fact]
    public void should_cancel_when_the_dragged_unit_disappears_from_a_new_scene()        // SIM-C011 · C025
    {
        var calls = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(40, 0));
            var positions = canvas.Scene.Positions.Where(p => p.Key != H.IdOf("7중대")).ToDictionary(p => p.Key, p => p.Value);
            canvas.Scene = canvas.Scene with { Positions = positions };
            return (fake.Calls.ToList(), canvas.IsDragging);
        });

        Assert.False(calls.Item2);
        Assert.Equal($"cancel:{H.IdOf("7중대")}", calls.Item1.Last());
    }
    #endregion

    #region - 겹침 · z 순서 (must-cover 2 · SIM-X001~003) -
    [Fact]
    public void should_lift_the_selected_node_and_prefer_it_under_the_pointer_when_two_nodes_overlap()
    {
        var result = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var seventh = H.IdOf("7중대");
            var ninth = H.IdOf("9중대");
            canvas.SelectedUnitId = seventh;                         // 먼저 그려진(아래) 7중대를 고른다
            var zSelected = Panel.GetZIndex(H.Node(canvas, "7중대"));

            // 10중대를 끌어 7중대 · 9중대가 겹친 자리 위에 머문다 — 선택(위)인 7중대가 잡힌다.
            var (_, at) = Start(canvas, "10중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(0, 20));
            canvas.OnPointerMoved(H.ScreenOf(canvas, ninth));
            var hover = canvas.HoverUnitId;
            canvas.OnPointerReleased(H.ScreenOf(canvas, ninth) + new Vector(0, 200));
            return (zSelected, hover, lastMoved: canvas.LastMovedUnitId, zMoved: Panel.GetZIndex(H.Node(canvas, "10중대")),
                    top: canvas.NodesInDrawOrder().Last().UnitId);
        }, Overlapped());

        Assert.Equal(2, result.zSelected);
        Assert.Equal(H.IdOf("7중대"), result.hover);                 // 가려진 노드도 고르면 위로 — 포인터 판정도 같은 순서
        Assert.Equal(H.IdOf("10중대"), result.lastMoved);
        Assert.Equal(1, result.zMoved);                               // 마지막으로 옮긴 부대가 그다음
        Assert.Equal(H.IdOf("7중대"), result.top);
    }

    /// <summary>7중대를 9중대 자리에 겹쳐 둔 장면(사용자 Δ 로 겹침 — FR-21 은 허용).</summary>
    private static UnitMapScene Overlapped()
    {
        var scene = H.Scene();
        var positions = scene.Positions.ToDictionary(p => p.Key, p => p.Value);
        positions[H.IdOf("7중대")] = positions[H.IdOf("9중대")];
        return scene with { Positions = positions };
    }
    #endregion

    /// <summary>그 부대를 가운데에 두고(배율 <paramref name="scale"/>) 누른다.</summary>
    private static (UnitMapNode Node, Point At) Start(UnitMapCanvas canvas, string name, double scale)
    {
        canvas.SetView(scale, H.WorldOf(canvas, name));                 // 가운데(378, 300)
        var node = H.Node(canvas, name);
        var at = H.ScreenOf(canvas, node.UnitId);
        canvas.OnPointerPressed(at, node, spaceHeld: false);
        return (node, at);
    }
}
