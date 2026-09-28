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
