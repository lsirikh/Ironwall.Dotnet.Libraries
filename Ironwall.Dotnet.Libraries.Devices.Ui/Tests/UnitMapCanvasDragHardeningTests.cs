using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using H = Ironwall.Dotnet.Libraries.Devices.Ui.Tests.UnitMapCanvasHarness;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-70 — 끌기 정합 보강(STA 헤드리스). 월드 Δ(자동 팬 포함) · 콘텐츠 밖 · 오버레이 위 = 취소(Ctrl 도) · 끄는 중 줌 무시 ·
/// 좁은 뷰포트의 띠 두께 · 끄는 중 크기 변경 · 그리기 순서 = 트리 순(선택해도 z 불변).
/// 시나리오: SIM-C006~010 · C021~024 · C037~040 · C048~051 · D085~108 · X001~005 · V090 · V093 · V096 · V105. ISSUE-11 · 12 · 13 · 54 · 56.
/// </summary>
public class UnitMapCanvasDragHardeningTests
{
    [Fact]
    public void should_add_exactly_480_world_units_of_auto_pan_when_the_pointer_waits_one_second_in_the_top_band_at_half_scale()   // ① SIM-C048
    {
        var (drop, screenDy) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            var edge = new Point(at.X, 16);
            canvas.OnPointerMoved(edge);
            canvas.AutoPanTick(TimeSpan.FromSeconds(1));
            canvas.OnPointerReleased(edge);
            return (fake.Drops.Single(), edge.Y - at.Y);
        });

        Assert.Equal(-480, drop.WorldDy - screenDy / 0.5, 6);                  // 자동 팬 몫 = 240px ÷ 0.5
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_cancel_with_the_outside_notice_when_released_outside_the_content_even_with_ctrl(bool ctrl)   // ② SIM-C010 · D085~
    {
        var (calls, notice, returned) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.ReadModifiers = () => ctrl ? ModifierKeys.Control : ModifierKeys.None;
            var (node, at) = Start(canvas, "7중대", 0.5);
            var left = Canvas.GetLeft(node);
            canvas.OnPointerMoved(at + new Vector(30, 0));
            canvas.OnPointerMoved(new Point(canvas.ActualWidth + 40, at.Y));
            canvas.OnPointerReleased(new Point(canvas.ActualWidth + 40, at.Y));
            return (fake.Calls.ToList(), H.ById<Utils.Consoles.ConsoleText>(canvas, UnitMapCanvas.ID_UNDO_TEXT)?.Text, Canvas.GetLeft(node) == left);
        });

        Assert.DoesNotContain(calls, c => c.StartsWith("complete"));
        Assert.Equal($"cancel:{H.IdOf("7중대")}", calls.Last());
        Assert.Equal(UnitMapCanvas.DROP_OUTSIDE_CANCELLED, notice);
        Assert.True(returned);                                                 // 원위치 — 노드는 움직인 적이 없다
    }

    [Fact]
    public void should_cancel_when_released_over_the_status_text()                               // ③
    {
        var calls = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.LayoutStatusText = "배치: 모든 운영자 공유 · 보기 전용";
            H.Pump();
            var status = H.ById<Utils.Consoles.ConsoleText>(canvas, UnitMapCanvas.ID_LAYOUT_STATUS)!;
            var over = status.TranslatePoint(new Point(status.ActualWidth / 2, status.ActualHeight / 2), canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(30, 0));
            canvas.OnPointerMoved(over);
            canvas.OnPointerReleased(over);
            return fake.Calls.ToList();
        });

        Assert.Equal($"cancel:{H.IdOf("7중대")}", calls.Last());
    }

    [Fact]
    public void should_ignore_zero_fit_and_every_zoom_input_while_dragging()                     // ⑤ SIM-C006~008
    {
        var (scale, swaps, dragging) = H.Run(canvas =>
        {
            H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.7);
            canvas.OnPointerMoved(at + new Vector(30, 0));
            var s = canvas.Scale;
            var templates = canvas.Nodes.Sum(n => n.TemplateApplyCount);
            canvas.HandleKeyDown(Key.D0, Key.None, Key.None, ModifierKeys.None, canvas);
            canvas.HandleKeyDown(Key.Subtract, Key.None, Key.None, ModifierKeys.None, canvas);
            ((IUnitMapSurface)canvas).Fit();
            return (canvas.Scale - s, canvas.Nodes.Sum(n => n.TemplateApplyCount) - templates, canvas.IsDragging);
        });

        Assert.Equal(0, scale);
        Assert.Equal(0, swaps);
        Assert.True(dragging);
    }

    [Theory]
    [InlineData(0, 0)]          // 폭 0 — 자동 팬 없음(SIM-V090)
    [InlineData(40, 10)]        // 64 밑 — 띠 = 폭/4
    [InlineData(63, 15.75)]
    [InlineData(600, 32)]
    public void should_shrink_the_auto_pan_band_to_a_quarter_of_the_length_below_64(double length, double band)   // ⑥ ISSUE-54 · SIM-V093
    {
        Assert.Equal(band, UnitMapCanvas.AutoPanBand(length), 9);
    }

    [Fact]
    public void should_keep_the_world_delta_when_the_canvas_is_resized_mid_drag()                // ⑥
    {
        var (before, after, dragging) = H.Run(canvas =>
        {
            H.Attach(canvas);
            var (_, at) = Start(canvas, "7중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(60, 20));
            var d = canvas.DragDelta;
            canvas.Width = 640;
            H.Pump();
            canvas.OnPointerMoved(at + new Vector(60, 20));
            return (d, canvas.DragDelta, canvas.IsDragging);
        });

        Assert.True(dragging);
        Assert.True((after - before).Length < 1e-9, $"크기 변경으로 Δ 가 {before} → {after}");
    }

    [Fact]
    public void should_keep_tree_order_as_draw_order_and_not_lift_the_selected_node()           // ⑦ ISSUE-56 · SIM-X001~003
    {
        var result = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.SelectedUnitId = H.IdOf("7중대");                  // 7중대는 트리에서 9중대보다 앞(아래에 그림)
            var zs = canvas.Nodes.Select(n => Panel.GetZIndex(n)).Distinct().ToList();
            var order = canvas.NodesInDrawOrder().Select(n => n.UnitId).SequenceEqual(canvas.Scene.Tree.Ordered.Select(n => n.Id));

            var (_, at) = Start(canvas, "10중대", 0.5);
            canvas.OnPointerMoved(at + new Vector(0, 20));
            canvas.OnPointerMoved(H.ScreenOf(canvas, H.IdOf("9중대")));
            var hover = canvas.HoverUnitId;
            canvas.OnPointerReleased(H.ScreenOf(canvas, H.IdOf("9중대")) + new Vector(0, 200));
            return (zs, order, hover, zAfter: canvas.Nodes.Select(n => Panel.GetZIndex(n)).Distinct().ToList());
        }, Overlapped());

        Assert.Equal(new[] { 0 }, result.zs);
        Assert.True(result.order);
        Assert.Equal(H.IdOf("9중대"), result.hover);                  // 겹친 곳 = 나중에(위에) 그린 노드 — 선택과 무관
        Assert.Equal(new[] { 0 }, result.zAfter);                     // 끌어 옮겨도 z 불변
    }

    private static UnitMapScene Overlapped()
    {
        var scene = H.Scene();
        var positions = scene.Positions.ToDictionary(p => p.Key, p => p.Value);
        positions[H.IdOf("7중대")] = positions[H.IdOf("9중대")];
        return scene with { Positions = positions };
    }

    private static (UnitMapNode Node, Point At) Start(UnitMapCanvas canvas, string name, double scale)
    {
        canvas.SetView(scale, H.WorldOf(canvas, name));
        var node = H.Node(canvas, name);
        var at = H.ScreenOf(canvas, node.UnitId);
        canvas.OnPointerPressed(at, node, spaceHeld: false);
        return (node, at);
    }
}
