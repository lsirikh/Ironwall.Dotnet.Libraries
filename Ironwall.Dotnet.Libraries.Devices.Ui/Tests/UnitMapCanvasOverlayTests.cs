using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using H = Ironwall.Dotnet.Libraries.Devices.Ui.Tests.UnitMapCanvasHarness;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-23 — 캔버스 오버레이(STA 헤드리스, FR-11 · FR-14 · FR-32 · FR-35 · FR-40). HUD 문구 · 확인 오버레이(Enter/Esc · 포커스 ·
/// 확정 불가 중) · 되돌리기 막대(타이머 없음 · 오류 = 왼쪽 막대) · 배치 문구 · [다시 시도] · AutomationId 전부 peer 있는 요소 ·
/// 한 Tab 정지점 · 포커스 표시 · 닫히면 포커스 복원. 시나리오: A(계측) · K156~163(포커스 수명) · O(확인 대기) — ISSUE-18 · 23 · 36 · 52.
/// </summary>
public class UnitMapCanvasOverlayTests
{
    private static readonly UnitMapConfirmPrompt Prompt =
        new("상위 부대 바꾸기", new[] { "7중대를 3대대 밑으로 옮깁니다.", "예하 5 함께 옮겨집니다." }, "옮기기");

    [Fact]
    public void should_say_level_and_percent_in_the_hud_when_the_scale_changes()
    {
        var (half, detail) = H.Run(canvas =>
        {
            canvas.SetView(0.5, H.WorldOf(canvas, "7중대"));
            var label = H.ById<ConsoleText>(canvas, UnitMapCanvas.ID_ZOOM_LEVEL)!;
            var first = label.Text;
            Invoke(H.ById<Button>(canvas, UnitMapCanvas.ID_ZOOM_IN)!);            // HUD [+]
            Invoke(H.ById<Button>(canvas, UnitMapCanvas.ID_ZOOM_IN)!);
            return (first, label.Text);
        });

        Assert.Equal("부대 · 50%", half);
        Assert.Equal("부대 · 72%", detail);            // 0.5 × 1.2² = 0.72 — 올라갈 때 L2 는 ≥ 0.80 부터(히스테리시스)
    }

    [Fact]
    public void should_confirm_with_enter_and_cancel_with_escape_while_the_prompt_is_open()   // FR-32 · SIM-O
    {
        var (enter, escape, other, calls) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.ConfirmPrompt = Prompt;
            H.Pump();
            var e1 = canvas.HandleKeyDown(Key.Enter, Key.None, Key.None, ModifierKeys.None, canvas);
            var e2 = canvas.HandleKeyDown(Key.Escape, Key.None, Key.None, ModifierKeys.None, canvas);
            var e3 = canvas.HandleKeyDown(Key.M, Key.None, Key.None, ModifierKeys.None, canvas);     // 오버레이가 막는다(SIM-K095)
            return (e1, e2, e3, fake.Calls.ToList());
        });

        Assert.True(enter);
        Assert.True(escape);
        Assert.True(other);                                         // 소비 후 무동작(PRD v1.3 — TEST-72)
        Assert.Equal(new[] { "confirm:True", "confirm:False" }, calls);
    }

    [Fact]
    public void should_leave_enter_to_the_cancel_button_when_it_has_focus()
    {
        var calls = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.ConfirmPrompt = Prompt;
            var cancel = H.ById<Button>(canvas, UnitMapCanvas.ID_CONFIRM_CANCEL)!;
            canvas.HandleKeyDown(Key.Enter, Key.None, Key.None, ModifierKeys.None, cancel);
            return fake.Calls.ToList();
        });

        Assert.Empty(calls);
    }

    [Fact]
    public void should_disable_ok_and_ignore_enter_while_an_earlier_write_is_running()     // ISSUE-23
    {
        var (enabled, busy, calls) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.ConfirmPrompt = Prompt with { CanConfirm = false, BusyText = "앞선 작업을 마치는 중입니다" };
            canvas.HandleKeyDown(Key.Enter, Key.None, Key.None, ModifierKeys.None, canvas);
            var busyText = H.Descendants(canvas).OfType<ConsoleText>().Any(t => t.Text == "앞선 작업을 마치는 중입니다" && t.IsVisible);
            return (H.ById<Button>(canvas, UnitMapCanvas.ID_CONFIRM_OK)!.IsEnabled, busyText, fake.Calls.ToList());
        });

        Assert.False(enabled);
        Assert.True(busy);
        Assert.Empty(calls);                                       // 자동 전송 금지
    }

    [Fact]
    public void should_move_focus_to_ok_when_the_prompt_opens_and_back_to_the_canvas_when_it_closes()   // SIM-K157 · K163 · ISSUE-52
    {
        var (onOpen, onClose) = H.Run(canvas =>
        {
            canvas.RestoreFocus();                                  // 캔버스가 포커스였다
            canvas.ConfirmPrompt = Prompt;
            H.Pump();
            var opened = Focused(canvas) is DependencyObject o ? AutomationProperties.GetAutomationId(o) : null;
            canvas.ConfirmPrompt = null;
            H.Pump();
            return (opened, Focused(canvas)?.GetType());
        });

        Assert.Equal(UnitMapCanvas.ID_CONFIRM_OK, onOpen);
        Assert.Equal(typeof(UnitMapCanvas), onClose);
    }

    [Fact]
    public void should_return_focus_to_the_canvas_when_the_move_mode_or_the_bar_closes()   // ISSUE-52
    {
        var (afterMode, afterBar) = H.Run(canvas =>
        {
            canvas.RestoreFocus();
            canvas.MoveModeText = "위치 이동 — 화살표 20 · Shift 5 · Enter 확정 · Esc 취소";
            var bar = H.ById<Button>(canvas, UnitMapCanvas.ID_UNDO)!;
            canvas.Bar = new UnitMapBar("‘6중대’ 위치를 옮겼습니다", false, true);
            System.Windows.Input.FocusManager.SetFocusedElement(System.Windows.Input.FocusManager.GetFocusScope(canvas), canvas);
            canvas.MoveModeText = null;
            var m = Focused(canvas);
            canvas.Bar = null;
            return (m, Focused(canvas));
        });

        Assert.IsType<UnitMapCanvas>(afterMode);
        Assert.IsType<UnitMapCanvas>(afterBar);
    }

    [Fact]
    public void should_block_canvas_presses_while_the_prompt_is_open()                     // ISSUE-18
    {
        var gesture = H.Run(canvas =>
        {
            canvas.ConfirmPrompt = Prompt;
            canvas.OnPointerPressed(new Point(30, 30), H.Node(canvas, "7중대"), spaceHeld: false);
            return canvas.IsGestureActive;
        });

        Assert.False(gesture);
    }

    [Fact]
    public void should_keep_the_undo_bar_until_the_next_operation_without_a_timer()       // FR-35 · SB S9
    {
        var (visibleLater, undo, dismiss) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.Bar = new UnitMapBar("‘6중대’ 위치를 옮겼습니다 — 모든 운영자에게 보입니다", false, true);
            ((H.FakeClock)canvas.Clock).Advance(60_000);
            H.Pump();
            var message = H.ById<ConsoleText>(canvas, UnitMapCanvas.ID_UNDO_TEXT)!;
            Invoke(H.ById<Button>(canvas, UnitMapCanvas.ID_UNDO)!);
            Invoke(H.ById<Button>(canvas, UnitMapCanvas.ID_UNDO_DISMISS)!);
            return (message.IsVisible && message.Text.StartsWith("‘6중대’"), fake.Calls.Contains("undo"), fake.Calls.Contains("dismiss"));
        });

        Assert.True(visibleLater);
        Assert.True(undo);
        Assert.True(dismiss);
    }

    [Fact]
    public void should_draw_the_failure_bar_with_a_left_vertical_mark_and_no_undo_button()   // FR-34 · NFR-06(형태)
    {
        var (markShown, undoShown, okMark) = H.Run(canvas =>
        {
            canvas.Bar = new UnitMapBar("‘6중대’를 옮기지 못했습니다 — 권한이 없습니다", IsError: true, CanUndo: false);
            H.Pump();
            var mark = ErrorMark(canvas);
            var shown = mark?.IsVisible ?? false;
            var undoVisible = H.ById<Button>(canvas, UnitMapCanvas.ID_UNDO)!.IsVisible;
            var width = mark?.ActualWidth ?? -1;
            canvas.Bar = new UnitMapBar("‘6중대’ 위치를 옮겼습니다", IsError: false, CanUndo: true);
            H.Pump();
            return (shown && width == 4, undoVisible, ErrorMark(canvas)?.IsVisible ?? false);
        });

        Assert.True(markShown);
        Assert.False(undoShown);
        Assert.False(okMark);
    }

    [Fact]
    public void should_show_the_layout_status_and_the_retry_button_only_when_retry_is_possible()   // FR-11
    {
        var (text, retryHidden, retryShown, calls) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.LayoutStatusText = "배치를 불러오지 못했습니다 — 자동 배치로 보입니다";
            H.Pump();
            var retry = H.ById<Button>(canvas, UnitMapCanvas.ID_LAYOUT_RETRY)!;
            var hidden = !retry.IsVisible;
            canvas.CanRetryLayout = true;
            H.Pump();
            var shown = retry.IsVisible;
            Invoke(retry);
            return (H.ById<ConsoleText>(canvas, UnitMapCanvas.ID_LAYOUT_STATUS)!.Text, hidden, shown, fake.Calls.ToList());
        });

        Assert.Equal("배치를 불러오지 못했습니다 — 자동 배치로 보입니다", text);
        Assert.True(retryHidden);
        Assert.True(retryShown);
        Assert.Equal(new[] { "retry" }, calls);
    }

    /// <summary>보이는 노드 요소 사각형 중 가장 위 · 가장 아래(캔버스 좌표).</summary>
    private static (double Top, double Bottom) NodeExtent(UnitMapCanvas canvas)
    {
        var rects = canvas.Nodes.Where(n => n.IsVisible)
                          .Select(n => new Rect(n.TranslatePoint(new Point(0, 0), canvas), new Size(n.ActualWidth, n.ActualHeight)))
                          .ToList();
        return (rects.Min(r => r.Top), rects.Max(r => r.Bottom));
    }

    /// <summary>오버레이 틀(문구 · HUD 글이 든 Border)의 캔버스 사각형.</summary>
    private static Rect BoxOf(UnitMapCanvas canvas, string id)
    {
        var text = H.ById<FrameworkElement>(canvas, id)!;
        DependencyObject current = text;
        while (current is not Border { Parent: Grid }) current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        var box = (Border)current;
        return new Rect(box.TranslatePoint(new Point(0, 0), canvas), new Size(box.ActualWidth, box.ActualHeight));
    }

    [Fact]
    public void should_keep_every_node_out_from_under_the_status_note_and_the_hud_when_fit_runs_with_the_note_shown()
    {
        // 2026-09-28 실앱 — [전체 보기] 뒤 뿌리 노드(제○○사단)가 위 가운데 배치 문구 밑에 깔렸다.
        var (extent, note, hud) = H.Run(canvas =>
        {
            canvas.LayoutStatusText = "이 서버는 배치 저장을 지원하지 않습니다 — 옮긴 위치는 창을 닫으면 자동 배치로 돌아갑니다";
            H.Pump();
            ((IUnitMapSurface)canvas).Fit();
            H.Pump();
            return (NodeExtent(canvas), BoxOf(canvas, UnitMapCanvas.ID_LAYOUT_STATUS), BoxOf(canvas, UnitMapCanvas.ID_ZOOM_LEVEL));
        });

        Assert.True(extent.Top >= note.Bottom, $"맨 위 노드 {extent.Top} 가 배치 문구 아래 끝 {note.Bottom} 보다 위");
        Assert.True(extent.Bottom <= hud.Top, $"맨 아래 노드 {extent.Bottom} 가 HUD 위 끝 {hud.Top} 보다 아래");
    }

    [Fact]
    public void should_refit_once_when_the_status_note_appears_after_the_first_fit_and_the_view_was_not_touched()
    {
        // 배치 GET 응답(→ 배치 문구)은 첫 그림 뒤에 온다 — 그대로 둔 전체 보기는 문구가 뜨면 한 번 다시 맞춘다.
        var (extent, note, touchedKept) = H.Run(canvas =>
        {
            ((IUnitMapSurface)canvas).Fit();
            H.Pump();
            canvas.LayoutStatusText = "이 서버는 배치 저장을 지원하지 않습니다 — 옮긴 위치는 창을 닫으면 자동 배치로 돌아갑니다";
            H.Pump();
            H.Pump();
            var fitted = (NodeExtent(canvas), BoxOf(canvas, UnitMapCanvas.ID_LAYOUT_STATUS));

            // 사용자가 팬한 뒤에는 문구가 바뀌어도 뷰를 옮기지 않는다.
            canvas.PanBy(0, -40);
            var offset = canvas.PanOffset;
            canvas.LayoutStatusText = "배치를 불러오지 못했습니다 — 자동 배치로 보입니다 · 실시간 반영 꺼짐";
            H.Pump();
            H.Pump();
            return (fitted.Item1, fitted.Item2, canvas.PanOffset == offset);
        });

        Assert.True(extent.Top >= note.Bottom, $"맨 위 노드 {extent.Top} 가 배치 문구 아래 끝 {note.Bottom} 보다 위");
        Assert.True(touchedKept);
    }

    [Fact]
    public void should_keep_a_small_org_out_from_under_the_status_note_when_the_first_view_centers_my_unit_at_50_percent()
    {
        // 실앱 첫 화면 규칙(FR-15): 내 부대 가운데 50%. 작은 편제(사단 → 연대 → 대대 → 중대 · 소초)는 띠 사이에 들어가는데
        // 가운데 두기만 하면 뿌리가 위 배치 문구 밑에 깔렸다(preview units-dark-09-map, 2026-09-28).
        var (extent, note, hud) = H.Run(canvas =>
        {
            canvas.LayoutStatusText = "이 서버는 배치 저장을 지원하지 않습니다 — 옮긴 위치는 창을 닫으면 자동 배치로 돌아갑니다";
            var mine = canvas.Scene.Tree.Ordered.First(n => n.Depth == 4).Id;         // 소초(맨 아래) — 가운데 두면 뿌리가 화면 위로 밀린다
            ((IUnitMapSurface)canvas).CenterOn(mine, 0.5);
            H.Pump();
            H.Pump();
            return (NodeExtent(canvas), BoxOf(canvas, UnitMapCanvas.ID_LAYOUT_STATUS), BoxOf(canvas, UnitMapCanvas.ID_ZOOM_LEVEL));
        }, SmallOrgScene());

        Assert.True(extent.Top >= note.Bottom, $"맨 위 노드 {extent.Top} 가 배치 문구 아래 끝 {note.Bottom} 보다 위");
        Assert.True(extent.Bottom <= hud.Top, $"맨 아래 노드 {extent.Bottom} 가 HUD 위 끝 {hud.Top} 보다 아래");
    }

    /// <summary>미리보기 부대 콘솔과 같은 크기의 작은 편제 — 사단 1 · 연대 1 · 대대 2 · 중대 3 · 소초 2.</summary>
    private static UnitMapScene SmallOrgScene()
    {
        var nodes = new System.Collections.Generic.List<Ironwall.Dotnet.Libraries.Messages.Dto.Units.UnitListDto>
        {
            UnitMapTestData.Node(1, "d01", "제○○사단", "Division"),
            UnitMapTestData.Node(2, "r01", "1연대", "Regiment", 1),
            UnitMapTestData.Node(3, "b01", "2대대", "Battalion", 2),
            UnitMapTestData.Node(4, "b02", "3대대", "Battalion", 2),
            UnitMapTestData.Node(5, "c01", "5중대", "Company", 3),
            UnitMapTestData.Node(6, "c02", "6중대", "Company", 3),
            UnitMapTestData.Node(7, "c03", "7중대", "Company", 3),
            UnitMapTestData.Node(8, "p01", "4소초", "Outpost", 6),
            UnitMapTestData.Node(9, "p02", "3소초", "Outpost", 7),
        };
        var tree = UnitMapTestData.Tree(UnitMapTestData.Graph(nodes));
        var layout = Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model.UnitMapLayout.Compute(tree);
        return new UnitMapScene(tree, layout.Positions, new System.Collections.Generic.Dictionary<int, UnitMapNodeFacts>(), UnitMapLayers.All);
    }

    [Fact]
    public void should_put_every_canvas_automation_id_on_an_element_with_a_peer()          // FR-40 · ISSUE-36 · UA
    {
        var missing = H.Run(canvas =>
        {
            canvas.ConfirmPrompt = Prompt;
            canvas.Bar = new UnitMapBar("‘6중대’ 위치를 옮겼습니다", false, true);
            canvas.LayoutStatusText = "배치: 모든 운영자 공유 · 보기 전용";
            canvas.CanRetryLayout = true;
            canvas.MoveModeText = "위치 이동";
            H.Pump();
            var ids = new[]
            {
                UnitMapCanvas.AUTOMATION_ID, UnitMapCanvas.ID_ZOOM_IN, UnitMapCanvas.ID_ZOOM_OUT, UnitMapCanvas.ID_FIT, UnitMapCanvas.ID_ZOOM_LEVEL,
                UnitMapCanvas.ID_LAYOUT_STATUS, UnitMapCanvas.ID_LAYOUT_RETRY, UnitMapCanvas.ID_CONFIRM, UnitMapCanvas.ID_CONFIRM_TEXT,
                UnitMapCanvas.ID_CONFIRM_OK, UnitMapCanvas.ID_CONFIRM_CANCEL, UnitMapCanvas.ID_UNDO, UnitMapCanvas.ID_UNDO_DISMISS,
                UnitMapCanvas.ID_UNDO_TEXT, UnitMapCanvas.ID_MOVE_MODE,
            };
            var elements = H.Descendants(canvas).Prepend(canvas).OfType<UIElement>().ToList();
            var viaCanvasPeer = Flatten(UIElementAutomationPeer.CreatePeerForElement(canvas)!).Select(p => p.GetAutomationId()).ToHashSet();
            return ids.Where(id => elements.FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id) is not UIElement e
                                   || UIElementAutomationPeer.CreatePeerForElement(e) is null
                                   || (id != UnitMapCanvas.AUTOMATION_ID && !viaCanvasPeer.Contains(id)))
                      .ToList();
        });

        Assert.Empty(missing);
    }

    [Fact]
    public void should_be_one_tab_stop_with_hud_and_bar_buttons_out_of_the_tab_order()     // SIM-K160 · K161 · ISSUE-52
    {
        var (mode, hudFocusable, okFocusable, confirmMode) = H.Run(canvas =>
        {
            var confirm = H.Descendants(canvas).OfType<UnitMapOverlayPanel>().Single();
            return (KeyboardNavigation.GetTabNavigation(canvas),
                    new[] { UnitMapCanvas.ID_ZOOM_IN, UnitMapCanvas.ID_ZOOM_OUT, UnitMapCanvas.ID_FIT, UnitMapCanvas.ID_UNDO, UnitMapCanvas.ID_UNDO_DISMISS, UnitMapCanvas.ID_LAYOUT_RETRY }
                        .Any(id => H.ById<Button>(canvas, id)!.Focusable),
                    H.ById<Button>(canvas, UnitMapCanvas.ID_CONFIRM_OK)!.Focusable,
                    KeyboardNavigation.GetTabNavigation(confirm));
        });

        Assert.Equal(KeyboardNavigationMode.Once, mode);
        Assert.False(hudFocusable);                                // 키 짝: + − 0 · Ctrl+Z
        Assert.True(okFocusable);
        Assert.Equal(KeyboardNavigationMode.Cycle, confirmMode);   // 확인 안에서만 두 단추를 돈다
    }

    [Fact]
    public void should_declare_no_x_name_in_the_canvas_sources()                           // UA — x:Name 은 CM 바인딩 지시자
    {
        foreach (var file in H.CanvasSources())
            Assert.DoesNotContain("x:Name=", File.ReadAllText(file));
    }

    #region - Fixtures -
    private static void Invoke(Button button)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button)!.GetPattern(PatternInterface.Invoke)).Invoke();
        H.Pump();                                                   // 단추 peer 의 Invoke 는 클릭을 디스패처에 걸어 둔다
    }

    private static DependencyObject? Focused(UnitMapCanvas canvas)
        => canvas.IsKeyboardFocused ? canvas : FocusManager.GetFocusedElement(FocusManager.GetFocusScope(canvas)) as DependencyObject;

    private static Border? ErrorMark(UnitMapCanvas canvas)
    {
        var message = H.ById<ConsoleText>(canvas, UnitMapCanvas.ID_UNDO_TEXT)!;
        return (message.Parent as StackPanel)?.Children.OfType<Border>().FirstOrDefault();
    }

    private static System.Collections.Generic.IEnumerable<AutomationPeer> Flatten(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren() ?? new System.Collections.Generic.List<AutomationPeer>())
        {
            yield return child;
            foreach (var nested in Flatten(child)) yield return nested;
        }
    }
    #endregion
}
