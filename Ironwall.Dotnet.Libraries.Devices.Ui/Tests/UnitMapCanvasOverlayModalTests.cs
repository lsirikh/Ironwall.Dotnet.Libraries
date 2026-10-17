using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using H = Ironwall.Dotnet.Libraries.Devices.Ui.Tests.UnitMapCanvasHarness;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-72 — 확인 오버레이의 캔버스 쪽 모달 · 계측 id(STA 헤드리스).
/// </summary>
/// <remarks>
/// <para>안전 계약(UA · FR-42 · ISSUE-37): 오버레이의 <c>Enter</c> 확정과 서버 되돌리기 <c>Ctrl+Z</c> 는 <b>단추가 아니라 키</b>라
/// 호스트 <c>DestructiveGuard</c>(AutomationId 기반)가 막을 수 없다 — 헤드 시험(TEST-41 · 43)은 가짜 포트 · 서버 스왑에서만 돈다.</para>
/// 시나리오: SIM-O001~024(캔버스 쪽) · G197~202 · K006 · K012 · … · K095~096 · K107~108 · K157 · K163 · A021~024 · A028~029 · A032 · T054.
/// ISSUE-18 · 36 · 37 · 49.
/// </remarks>
public class UnitMapCanvasOverlayModalTests
{
    private static readonly UnitMapConfirmPrompt Prompt = new("상위 부대 바꾸기", new[] { "7중대를 3대대 밑으로 옮깁니다." }, "옮기기");

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Middle)]
    [InlineData(MouseButton.Right)]
    public void should_ignore_every_canvas_press_while_the_prompt_is_open(MouseButton button)   // ① SIM-G197~202
    {
        var (gesture, calls, panned) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.ConfirmPrompt = Prompt;
            var at = H.ScreenOf(canvas, H.IdOf("7중대"));
            var before = canvas.PanOffset;
            canvas.OnOtherButtonPressed(at, button, H.Node(canvas, "7중대"));
            canvas.OnPointerMoved(at + new Vector(8.1, 0));
            canvas.OnRightClick(at, at, H.Node(canvas, "7중대"));
            return (canvas.IsGestureActive, fake.Calls.ToList(), canvas.PanOffset - before);
        });

        Assert.False(gesture);
        Assert.Empty(calls);
        Assert.Equal(new Vector(0, 0), panned);
    }

    [Theory]
    [InlineData(Key.Up, Key.None, ModifierKeys.None)]
    [InlineData(Key.M, Key.None, ModifierKeys.None)]
    [InlineData(Key.OemPlus, Key.None, ModifierKeys.None)]
    [InlineData(Key.Z, Key.None, ModifierKeys.Control)]
    [InlineData(Key.System, Key.Up, ModifierKeys.Alt)]
    public void should_swallow_other_keys_while_the_prompt_is_open(Key key, Key systemKey, ModifierKeys modifiers)   // ① SIM-K006 …
    {
        var (handled, calls, scale) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.ConfirmPrompt = Prompt;
            var s = canvas.Scale;
            var h = canvas.HandleKeyDown(key, systemKey, Key.None, modifiers, canvas);
            return (h, fake.Calls.ToList(), canvas.Scale - s);
        });

        Assert.True(handled);                                          // 소비 후 무동작
        Assert.Empty(calls);
        Assert.Equal(0, scale);
    }

    [Fact]
    public void should_let_l_through_to_the_view_model_while_the_prompt_is_open()             // ① L 만 통과
    {
        var calls = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.ConfirmPrompt = Prompt;
            canvas.HandleKeyDown(Key.L, Key.None, Key.None, ModifierKeys.None, canvas);
            canvas.HandleKeyDown(Key.ImeProcessed, Key.None, Key.L, ModifierKeys.None, canvas);
            return fake.Calls.ToList();
        });

        Assert.Equal(new[] { "key:LocateOnMap:False", "key:LocateOnMap:False" }, calls);
    }

    [Fact]
    public void should_return_focus_to_the_dragged_node_when_the_prompt_closes()               // ② SIM-K157 · K163
    {
        var focused = H.Run(canvas =>
        {
            H.Attach(canvas);
            canvas.SelectedUnitId = H.IdOf("5중대");
            canvas.SetView(0.5, H.WorldOf(canvas, "7중대"));
            var at = H.ScreenOf(canvas, H.IdOf("7중대"));
            canvas.OnPointerPressed(at, H.Node(canvas, "7중대"), spaceHeld: false);
            canvas.OnPointerMoved(at + new Vector(0, -80));
            canvas.OnPointerReleased(at + new Vector(0, -80));
            canvas.ConfirmPrompt = Prompt;
            H.Pump();
            canvas.ConfirmPrompt = null;
            H.Pump();
            return (FocusManager.GetFocusedElement(FocusManager.GetFocusScope(canvas)) as UnitMapNode)?.UnitId;
        });

        Assert.Equal(H.IdOf("7중대"), focused);                       // 끈 노드(없으면 선택 노드)
    }

    [Fact]
    public void should_keep_the_prompt_when_the_window_is_deactivated()                         // ③
    {
        var visible = H.Run(canvas =>
        {
            canvas.ConfirmPrompt = Prompt;
            canvas.OnHostDeactivated(null, EventArgs.Empty);
            H.Pump();
            return H.ById<UnitMapOverlayPanel>(canvas, UnitMapCanvas.ID_CONFIRM)!.IsVisible;
        });

        Assert.True(visible);
    }

    [Fact]
    public void should_carry_the_v1_3_automation_ids_on_elements_with_peers()                  // ④ FR-40 v1.3 · ISSUE-36
    {
        var result = H.Run(canvas =>
        {
            canvas.ConfirmPrompt = Prompt;
            canvas.Bar = new UnitMapBar("‘6중대’ 위치를 옮겼습니다", false, true);
            canvas.MoveModeText = "위치 이동";
            H.Pump();
            return new[] { "Units.Map.UndoText", "Units.Map.Confirm", "Units.Map.Confirm.Text", "Units.Map.MoveMode" }
                .Select(id => (id, element: H.Descendants(canvas).OfType<UIElement>().FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id)))
                .Select(x => (x.id, Found: x.element is not null, Peer: x.element is not null && UIElementAutomationPeer.CreatePeerForElement(x.element) is not null,
                              IsText: x.element is ConsoleText || x.element is UnitMapOverlayPanel))
                .ToList();
        });

        Assert.All(result, r => Assert.True(r.Found && r.Peer && r.IsText, $"{r.id}: 찾음={r.Found} peer={r.Peer}"));
        Assert.Equal("Units.Map.UndoText", UnitMapCanvas.ID_UNDO_TEXT);
        Assert.Equal("Units.Map.Confirm.Text", UnitMapCanvas.ID_CONFIRM_TEXT);
    }

    [Fact]
    public void should_offer_the_bar_action_button_and_run_it_when_the_bar_carries_an_action()   // FR-26 v1.3 · ISSUE-55 [인접선 켜기]
    {
        var (visible, calls) = H.Run(canvas =>
        {
            var fake = H.Attach(canvas);
            canvas.Bar = new UnitMapBar("인접선이 꺼져 있어 보이지 않습니다", false, false, ActionText: "인접선 켜기");
            H.Pump();
            var button = H.ById<Button>(canvas, UnitMapCanvas.ID_BAR_ACTION)!;
            var v = button.IsVisible && (string)button.Content == "인접선 켜기";
            ((System.Windows.Automation.Provider.IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button)!.GetPattern(PatternInterface.Invoke)).Invoke();
            H.Pump();
            return (v, fake.Calls.ToList());
        });

        Assert.True(visible);
        Assert.Equal(new[] { "bar-action" }, calls);                  // 레이어를 몰래 켜지 않는다 — 뷰모델이 판단
    }
}
