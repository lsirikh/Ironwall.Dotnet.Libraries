using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;
using H = Ironwall.Dotnet.Libraries.Devices.Ui.Tests.UnitMapCanvasHarness;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// FR-29 v1.3 ② — 캔버스 밖 · 오버레이 위에 놓아 취소하면 막대가 "관계도 밖에 놓아 취소했습니다" 를 말한다.
/// 실창 8회차(SIM-D087 · D093 · D-09): 앞 조작의 뷰모델 막대("… 위치를 되돌렸습니다.")가 남아 있으면 캔버스 알림이 그 밑에 깔려
/// 운영자는 취소됐다는 말을 못 봤다(<c>Bar ?? _localNotice</c> — 오래된 막대가 늘 이겼다). 더 새것이 보여야 한다.
/// </summary>
public class UnitMapCanvasOutsideNoticeTests
{
    private const string OlderBar = "‘6중대’ 위치를 되돌렸습니다.";

    [Fact]
    public void should_show_the_outside_notice_over_an_older_bar_when_a_drag_is_released_outside_the_content()
    {
        var (text, undoVisible) = H.Run(canvas =>
        {
            H.Attach(canvas);
            canvas.Bar = new UnitMapBar(OlderBar, false, true);
            H.Pump();
            DropOutside(canvas);
            return (Text(canvas), H.ById<Button>(canvas, UnitMapCanvas.ID_UNDO)!.Visibility == Visibility.Visible);
        });

        Assert.Equal(UnitMapCanvas.DROP_OUTSIDE_CANCELLED, text);
        Assert.False(undoVisible);                // 알림에는 [되돌리기]가 없다 — 되돌릴 것이 없는 취소다
    }

    [Fact]
    public void should_bring_back_the_older_bar_when_the_outside_notice_is_dismissed()
    {
        var (text, undoVisible) = H.Run(canvas =>
        {
            H.Attach(canvas);
            canvas.Bar = new UnitMapBar(OlderBar, false, true);
            H.Pump();
            DropOutside(canvas);
            H.ById<Button>(canvas, UnitMapCanvas.ID_UNDO_DISMISS)!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            return (Text(canvas), H.ById<Button>(canvas, UnitMapCanvas.ID_UNDO)!.Visibility == Visibility.Visible);
        });

        Assert.Equal(OlderBar, text);             // ✕ 는 알림만 걷는다 — 앞 조작의 되돌리기는 그대로 살아 있다
        Assert.True(undoVisible);
    }

    [Fact]
    public void should_replace_the_outside_notice_when_the_view_model_raises_a_newer_bar()
    {
        var text = H.Run(canvas =>
        {
            H.Attach(canvas);
            DropOutside(canvas);
            canvas.Bar = new UnitMapBar("‘7중대’ 위치를 옮겼습니다.", false, true);
            H.Pump();
            return Text(canvas);
        });

        Assert.Equal("‘7중대’ 위치를 옮겼습니다.", text);
    }

    private static void DropOutside(UnitMapCanvas canvas)
    {
        canvas.SetView(0.5, H.WorldOf(canvas, "7중대"));
        var node = H.Node(canvas, "7중대");
        var at = H.ScreenOf(canvas, node.UnitId);
        canvas.OnPointerPressed(at, node, spaceHeld: false);
        canvas.OnPointerMoved(at + new Vector(30, 0));
        var outside = new Point(canvas.ActualWidth + 40, at.Y);
        canvas.OnPointerMoved(outside);
        canvas.OnPointerReleased(outside);
        H.Pump();
    }

    private static string? Text(UnitMapCanvas canvas) => H.ById<ConsoleText>(canvas, UnitMapCanvas.ID_UNDO_TEXT)?.Text;
}
