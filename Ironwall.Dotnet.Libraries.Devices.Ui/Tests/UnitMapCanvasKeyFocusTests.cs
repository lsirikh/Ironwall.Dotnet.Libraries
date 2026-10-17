using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using H = Ironwall.Dotnet.Libraries.Devices.Ui.Tests.UnitMapCanvasHarness;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-71 — 키 해석 · 포커스 · 제스처 보강(STA 헤드리스). IME · 단축키 범위 · Tab 정지점 · 포커스 복원 · 오른쪽 = 선택만 ·
/// 끌 수 없는 노드 = 팬 · 더블클릭 무동작 · 끄는 중 가운데 · Space 무시 · Esc 한 층 · DPI.
/// 시나리오: SIM-K001~163 · G033~036 · G053~056 · G061~064 · G081~084 · G109~112 · G116 · G124 · G128 · G132 · G204 · G209~211 · Z546~552 · V106.
/// ISSUE-33 · 18 · 19 · 35 · 52 · 59.
/// </summary>
public class UnitMapCanvasKeyFocusTests
{
    [Theory]
    [InlineData(Key.M, UnitMapKeyCommand.MoveMode)]
    [InlineData(Key.L, UnitMapKeyCommand.LocateOnMap)]
    public void should_run_m_and_l_when_the_korean_ime_processed_them_with_focus_on_a_node(Key letter, UnitMapKeyCommand command)   // ① SIM-K086 · K088 · K098 · K100
    {
        var calls = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var node = H.Node(canvas, "7중대");
            canvas.HandleKeyDown(Key.ImeProcessed, Key.None, letter, ModifierKeys.None, node);
            canvas.HandleKeyDown(Key.ImeProcessed, Key.None, letter, ModifierKeys.None, canvas);
            return fake.Calls.ToList();
        });

        Assert.Equal(new[] { $"key:{command}:False", $"key:{command}:False" }, calls);
    }

    [Theory]
    [InlineData(Key.Z, ModifierKeys.Control)]
    [InlineData(Key.M, ModifierKeys.None)]
    [InlineData(Key.L, ModifierKeys.None)]
    [InlineData(Key.OemPlus, ModifierKeys.None)]
    [InlineData(Key.D0, ModifierKeys.None)]
    [InlineData(Key.Left, ModifierKeys.None)]
    public void should_leave_shortcuts_to_a_text_box_when_the_focus_is_in_it(Key key, ModifierKeys modifiers)   // ② ISSUE-35 · SIM-K090~092
    {
        var (handled, calls, scale) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            var s = canvas.Scale;
            var h = canvas.HandleKeyDown(key, Key.None, Key.None, modifiers, new TextBox());
            return (h, fake.Calls.ToList(), canvas.Scale - s);
        });

        Assert.False(handled);
        Assert.Empty(calls);
        Assert.Equal(0, scale);
    }

    [Fact]
    public void should_be_one_tab_stop_that_lands_on_the_selected_node()                       // ③ SIM-K160 · K161
    {
        var (mode, nodeTabStop, landed) = H.Run(canvas =>
        {
            canvas.SelectedUnitId = H.IdOf("7중대");
            canvas.RestoreFocus();
            return (KeyboardNavigation.GetTabNavigation(canvas), KeyboardNavigation.GetIsTabStop(H.Node(canvas, "7중대")),
                    (Focused(canvas) as UnitMapNode)?.UnitId);
        });

        Assert.Equal(KeyboardNavigationMode.Once, mode);
        Assert.False(nodeTabStop);                                    // 노드 200개를 Tab 으로 돌지 않는다
        Assert.Equal(H.IdOf("7중대"), landed);
    }

    [Fact]
    public void should_follow_the_selection_and_survive_a_level_swap_and_a_new_scene_with_focus()   // ④ SIM-K156 · K158 · K159
    {
        var (afterSelect, afterSwap, afterScene) = H.Run(canvas =>
        {
            canvas.SetView(0.5, H.WorldOf(canvas, "7중대"));
            canvas.SelectedUnitId = H.IdOf("7중대");
            canvas.RestoreFocus();
            canvas.SelectedUnitId = H.IdOf("8중대");
            var a = (Focused(canvas) as UnitMapNode)?.UnitId;
            canvas.SetView(1.0, H.WorldOf(canvas, "8중대"));        // L1 → L2 템플릿 교체
            H.Pump();
            var b = (Focused(canvas) as UnitMapNode)?.UnitId;
            canvas.Scene = canvas.Scene with { Facts = new System.Collections.Generic.Dictionary<int, UnitMapNodeFacts>() };   // 재조회
            H.Pump();
            return (a, b, (Focused(canvas) as UnitMapNode)?.UnitId);
        });

        Assert.Equal(H.IdOf("8중대"), afterSelect);
        Assert.Equal(H.IdOf("8중대"), afterSwap);
        Assert.Equal(H.IdOf("8중대"), afterScene);
    }

    [Theory]
    [InlineData(0.2, 0)]
    [InlineData(0.5, 30)]            // 이동 거리와 무관 — 메뉴 · 끌기 · 팬 없음
    [InlineData(1.0, 120)]
    public void should_only_select_on_right_click_at_every_level_regardless_of_distance(double scale, double distance)   // ⑤ D-2026-09-27-6615ba
    {
        var (calls, panned) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.SetView(scale, H.WorldOf(canvas, "7중대"));
            var before = canvas.PanOffset;
            canvas.OnRightClick(new Point(100, 100), new Point(100 + distance, 100), H.Node(canvas, "7중대"));
            return (fake.Calls.ToList(), canvas.PanOffset - before);
        });

        Assert.Equal(new[] { $"select:{H.IdOf("7중대")}" }, calls);
        Assert.Equal(new Vector(0, 0), panned);
    }

    [Fact]
    public void should_start_the_drag_and_paint_every_target_blocked_when_view_only_on_a_supported_server()   // ⑥ FR-05
    {
        var states = H.Run(canvas =>
        {
            H.Attach(canvas, new Consoles.Units.Map.Model.UnitMapDropPolicy(true, false, Consoles.Units.Map.Model.UnitMapLayoutState.Shared));
            canvas.SetView(0.5, H.WorldOf(canvas, "7중대"));
            var at = H.ScreenOf(canvas, H.IdOf("7중대"));
            canvas.OnPointerPressed(at, H.Node(canvas, "7중대"), spaceHeld: false);
            canvas.OnPointerMoved(at + new Vector(20, 0));
            return (canvas.IsDragging, canvas.Nodes.Where(n => n.DropState != UnitMapNodeDropState.Origin).Select(n => n.DropState).Distinct().ToList(),
                    canvas.HoverChipText);
        });

        Assert.True(states.IsDragging);
        Assert.Equal(new[] { UnitMapNodeDropState.Blocked }, states.Item2);
    }

    [Fact]
    public void should_do_nothing_on_a_node_double_click_and_on_an_L2_empty_double_click()      // ⑦ SIM-Z546~552
    {
        var (calls, gesture, scale) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.SetView(1.0, H.WorldOf(canvas, "7중대"));
            canvas.OnDoubleClick(H.ScreenOf(canvas, H.IdOf("7중대")), H.Node(canvas, "7중대"));
            canvas.OnDoubleClick(new Point(5, 5), null);
            return (fake.Calls.ToList(), canvas.IsGestureActive, canvas.Scale);
        });

        Assert.Empty(calls);
        Assert.False(gesture);
        Assert.Equal(1.0, scale, 9);
    }

    [Fact]
    public void should_ignore_the_middle_button_and_space_while_dragging()                     // ⑨
    {
        var (dragging, calls) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.SetView(0.5, H.WorldOf(canvas, "7중대"));
            var at = H.ScreenOf(canvas, H.IdOf("7중대"));
            canvas.OnPointerPressed(at, H.Node(canvas, "7중대"), spaceHeld: false);
            canvas.OnPointerMoved(at + new Vector(30, 0));
            canvas.OnOtherButtonPressed(at + new Vector(30, 0), MouseButton.Middle, H.Node(canvas, "8중대"));
            canvas.HandleKeyDown(Key.Space, Key.None, Key.None, ModifierKeys.None, canvas);
            return (canvas.IsDragging, fake.Calls.ToList());
        });

        Assert.True(dragging);
        Assert.DoesNotContain(calls, c => c.StartsWith("cancel") || c.StartsWith("complete"));
    }

    [Fact]
    public void should_peel_one_layer_per_escape_overlay_then_drag_then_view_model()           // ⑩ ISSUE-18
    {
        var calls = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            fake.HandleKeyResult = _ => true;                          // 뷰모델에 M 모드 · 선택이 있다
            canvas.ConfirmPrompt = new UnitMapConfirmPrompt("확인", new[] { "줄" }, "옮기기");
            canvas.HandleKeyDown(Key.Escape, Key.None, Key.None, ModifierKeys.None, canvas);   // ① 오버레이만
            canvas.ConfirmPrompt = null;

            canvas.SetView(0.5, H.WorldOf(canvas, "7중대"));
            var at = H.ScreenOf(canvas, H.IdOf("7중대"));
            canvas.OnPointerPressed(at, H.Node(canvas, "7중대"), spaceHeld: false);
            canvas.OnPointerMoved(at + new Vector(30, 0));
            canvas.HandleKeyDown(Key.Escape, Key.None, Key.None, ModifierKeys.None, canvas);   // ② 끌기만
            canvas.HandleKeyDown(Key.Escape, Key.None, Key.None, ModifierKeys.None, canvas);   // ③ 뷰모델(M → 선택 해제)
            return fake.Calls.ToList();
        });

        var id = H.IdOf("7중대");
        Assert.Equal(new[] { "confirm:False", $"begin:{id}", $"cancel:{id}", "key:Escape:False" }, calls);
    }

    [Fact]
    public void should_redraw_the_line_layer_once_when_the_dpi_changes()                       // ⑪ ISSUE-59 · SIM-V106
    {
        var redraws = H.Run(canvas =>
        {
            var before = canvas.LineLayer.RenderCount;
            canvas.OnDpiChangedForLayers();
            return canvas.LineLayer.RenderCount - before;
        });

        Assert.Equal(1, redraws);
    }

    private static DependencyObject? Focused(UnitMapCanvas canvas)
        => Keyboard.FocusedElement as DependencyObject is { } k && canvas.IsKeyboardFocusWithin
            ? k
            : FocusManager.GetFocusedElement(FocusManager.GetFocusScope(canvas)) as DependencyObject;
}
