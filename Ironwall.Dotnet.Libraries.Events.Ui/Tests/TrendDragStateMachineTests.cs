using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 기간 끌기 상태 기계(N-07 적대 검토 R3). ESC 경로는 전에 시각 트리 안에 있어 단언할 수 없었다.
/// </summary>
public class TrendDragStateMachineTests
{
    private static TrendDragStateMachine Dragging()
    {
        var m = new TrendDragStateMachine();
        m.Press(100, 50);
        m.Move(200, 50);            // 데드존(8)을 넘긴다
        return m;
    }

    [Fact]
    public void should_ask_for_a_key_subscription_when_pressed()
    {
        var m = new TrendDragStateMachine();

        Assert.True(m.Press(100, 50));      // 구독은 누를 때 건다 — 포커스를 기다리지 않는다
        Assert.True(m.IsPressed);
        Assert.False(m.IsDragging);         // 아직 데드존 안이다
        Assert.False(m.Press(120, 50));     // 두 번 걸지 않는다
    }

    [Fact]
    public void should_not_start_dragging_until_the_dead_zone_is_passed()
    {
        var m = new TrendDragStateMachine();
        m.Press(100, 50);

        Assert.False(m.Move(105, 50));      // 5 DIU — 클릭이다
        Assert.False(m.IsDragging);
        Assert.True(m.Move(110, 50));       // 10 DIU — 드래그다
        Assert.True(m.IsDragging);
    }

    [Fact]
    public void should_commit_when_released_after_a_real_drag()
    {
        var finish = Dragging().Release();

        Assert.True(finish.Commit);
        Assert.True(finish.ClearBand);
        Assert.True(finish.Unsubscribe);
        Assert.True(finish.ReleaseCapture);
    }

    [Fact]
    public void should_not_commit_when_released_inside_the_dead_zone()
    {
        var m = new TrendDragStateMachine();
        m.Press(100, 50);
        m.Move(104, 50);

        var finish = m.Release();

        Assert.False(finish.Commit);        // 클릭이 기간을 바꾸지 않는다(서버 호출 0)
        Assert.True(finish.Unsubscribe);    // 그래도 구독은 뗀다
    }

    [Fact]
    public void should_cancel_and_not_commit_when_escape_arrives_while_dragging()
    {
        var m = Dragging();

        var (handled, finish) = m.Escape();

        Assert.True(handled);               // 끄는 중에만 소비한다
        Assert.False(finish.Commit);
        Assert.True(finish.ClearBand);
        Assert.True(finish.Unsubscribe);
        Assert.False(m.IsPressed);
    }

    [Fact]
    public void should_not_consume_escape_when_not_dragging()
    {
        var m = new TrendDragStateMachine();
        var (handledIdle, _) = m.Escape();
        Assert.False(handledIdle);          // 무조건 소비하면 다른 Esc 동작이 깨진다

        m.Press(100, 50);                   // 눌렀지만 데드존 안
        var (handledPressed, _) = m.Escape();
        Assert.False(handledPressed);
    }

    [Fact]
    public void should_not_commit_when_capture_is_lost()
    {
        var finish = Dragging().LostCapture();

        Assert.False(finish.Commit);
        Assert.True(finish.ClearBand);
    }

    [Fact]
    public void should_finish_only_once_when_release_and_lost_capture_both_arrive()
    {
        // 놓음과 캡처 상실이 겹쳐도 두 번 돌면 안 된다(재진입).
        var m = Dragging();

        var first = m.Release();
        var second = m.LostCapture();

        Assert.True(first.Commit);
        Assert.False(second.Commit);
        Assert.False(second.ClearBand);
        Assert.False(second.Unsubscribe);
    }
}
