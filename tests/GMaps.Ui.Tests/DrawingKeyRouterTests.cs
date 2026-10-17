using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 드로잉 키 라우팅(<see cref="DrawingKeyRouter"/>) 회귀 — C8(ESC 취소) · C9(포커스 무관 터널: ESC=취소, Enter=완료) ·
/// C10(드로잉 중 Ctrl+Z=마지막 점 제거, Delete/방향키=무시). 실코드 소스링크.
/// </summary>
public class DrawingKeyRouterTests
{
    private const DrawingKeyModifiers None = DrawingKeyModifiers.None;
    private const DrawingKeyModifiers Ctrl = DrawingKeyModifiers.Control;
    private const DrawingKeyModifiers CtrlShift = DrawingKeyModifiers.Control | DrawingKeyModifiers.Shift;

    // ── 비드로잉: 라우터 불개입(기존 단축키 100% 유지) ──
    [Theory]
    [InlineData(DrawingKey.Escape, None)]
    [InlineData(DrawingKey.Enter, None)]
    [InlineData(DrawingKey.Back, None)]
    [InlineData(DrawingKey.Z, Ctrl)]
    [InlineData(DrawingKey.Y, Ctrl)]
    [InlineData(DrawingKey.Delete, None)]
    [InlineData(DrawingKey.Left, None)]
    [InlineData(DrawingKey.Up, CtrlShift)]
    public void should_pass_through_every_key_when_not_drawing(DrawingKey key, DrawingKeyModifiers mods)
    {
        // Arrange / Act
        var action = DrawingKeyRouter.Route(key, mods, isDrawing: false, isStrokePressed: false);

        // Assert
        Assert.Equal(DrawingKeyAction.PassThrough, action);
        Assert.False(DrawingKeyRouter.IsConsumed(action));
    }

    // ── C8: ESC ──
    [Fact]
    public void should_cancel_drawing_when_escape_pressed_without_stroke()
    {
        var action = DrawingKeyRouter.Route(DrawingKey.Escape, None, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.CancelDrawing, action);
    }

    [Fact]
    public void should_cancel_only_stroke_when_escape_pressed_during_stroke()
    {
        var action = DrawingKeyRouter.Route(DrawingKey.Escape, None, isDrawing: true, isStrokePressed: true);
        Assert.Equal(DrawingKeyAction.CancelStroke, action);
    }

    [Fact]
    public void should_cancel_stroke_when_press_lingers_after_drawing_ended()
    {
        // 드로잉이 이미 끝났는데 눌림(캡처)만 남은 경계 상태 — ESC 는 스트로크 정리로 이어져야 한다.
        var action = DrawingKeyRouter.Route(DrawingKey.Escape, None, isDrawing: false, isStrokePressed: true);
        Assert.Equal(DrawingKeyAction.CancelStroke, action);
    }

    // ── C9: Enter=완료, Backspace=되돌리기 (포커스 무관 터널이 같은 판정을 쓴다) ──
    [Fact]
    public void should_complete_drawing_when_enter_pressed()
    {
        var action = DrawingKeyRouter.Route(DrawingKey.Enter, None, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.CompleteDrawing, action);
    }

    [Fact]
    public void should_ignore_enter_when_stroke_still_pressed()
    {
        var action = DrawingKeyRouter.Route(DrawingKey.Enter, None, isDrawing: true, isStrokePressed: true);
        Assert.Equal(DrawingKeyAction.Ignore, action);
    }

    [Fact]
    public void should_undo_last_point_when_backspace_pressed()
    {
        var action = DrawingKeyRouter.Route(DrawingKey.Back, None, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.UndoLastPoint, action);
    }

    // ── C10: Ctrl+Z / Redo / Delete / 방향키 ──
    [Fact]
    public void should_undo_last_point_when_ctrl_z_pressed_while_drawing()
    {
        var action = DrawingKeyRouter.Route(DrawingKey.Z, Ctrl, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.UndoLastPoint, action);
        Assert.True(DrawingKeyRouter.IsConsumed(action));   // 전역 Undo 로 새지 않는다
    }

    [Fact]
    public void should_pass_through_plain_z_when_drawing()
    {
        var action = DrawingKeyRouter.Route(DrawingKey.Z, None, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.PassThrough, action);
    }

    [Theory]
    [InlineData(DrawingKey.Z, CtrlShift)]
    [InlineData(DrawingKey.Y, Ctrl)]
    public void should_swallow_redo_shortcuts_when_drawing(DrawingKey key, DrawingKeyModifiers mods)
    {
        var action = DrawingKeyRouter.Route(key, mods, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.Ignore, action);
    }

    [Fact]
    public void should_ignore_delete_when_drawing_started_with_lingering_selection()
    {
        // C10 정정: 클릭으로 점을 찍으면 선택이 걷히므로 증상은 '드로잉 시작 직후(점 0개/스트로크-only)'에 한정 — 그 구간에서도 Delete 는 소비·무시.
        var action = DrawingKeyRouter.Route(DrawingKey.Delete, None, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.Ignore, action);
    }

    [Theory]
    [InlineData(DrawingKey.Left, None)]
    [InlineData(DrawingKey.Right, DrawingKeyModifiers.Shift)]
    [InlineData(DrawingKey.Up, Ctrl)]
    [InlineData(DrawingKey.Down, CtrlShift)]
    public void should_ignore_arrow_keys_when_drawing(DrawingKey key, DrawingKeyModifiers mods)
    {
        var action = DrawingKeyRouter.Route(key, mods, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.Ignore, action);
    }

    [Fact]
    public void should_pass_through_unrelated_key_when_drawing()
    {
        var action = DrawingKeyRouter.Route(DrawingKey.Other, Ctrl, isDrawing: true, isStrokePressed: false);
        Assert.Equal(DrawingKeyAction.PassThrough, action);
    }

    [Fact]
    public void should_treat_every_non_passthrough_action_as_consumed()
    {
        foreach (DrawingKeyAction a in Enum.GetValues(typeof(DrawingKeyAction)))
            Assert.Equal(a != DrawingKeyAction.PassThrough, DrawingKeyRouter.IsConsumed(a));
    }
}
