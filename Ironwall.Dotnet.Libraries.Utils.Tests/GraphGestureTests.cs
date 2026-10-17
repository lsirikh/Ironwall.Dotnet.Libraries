using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// unit-relationship-map TEST-02 (FR-13 · NFR-11) — 팬과 노드 끌기의 구분(스토리보드 S4 표 전 행).
/// 판정은 <b>누른 순간</b>의 사실 하나로 정하고 끝날 때까지 바꾸지 않는다. 데드존은 커널 <see cref="DragMath.DeadZone"/>(8.0).
/// </summary>
public class GraphGestureTests
{
    private static readonly Point Origin = new(100, 100);

    private static GraphPress Empty(GraphPointerButton button = GraphPointerButton.Left, bool space = false)
        => new(GraphPressTarget.Empty, button, space, CanDragNode: false);

    private static GraphPress Node(bool canDrag, GraphPointerButton button = GraphPointerButton.Left, bool space = false)
        => new(GraphPressTarget.Node, button, space, canDrag);

    /// <summary>누르고 → 가로로 <paramref name="distance"/> 움직이고 → 그 자리에서 놓는다.</summary>
    private static (GraphGestureKind DuringMove, GraphGestureKind OnRelease) Run(GraphPress press, double distance)
    {
        var gesture = GraphGesture.Begin(press, Origin);
        var end = new Point(Origin.X + distance, Origin.Y);
        var during = gesture.Move(end);
        var released = gesture.Release(end);
        return (during, released);
    }

    #region - S4 표 -
    [Fact]
    public void should_clear_selection_when_empty_left_released_within_7_9()
    {
        var (during, released) = Run(Empty(), 7.9);

        Assert.Equal(GraphGestureKind.None, during);
        Assert.Equal(GraphGestureKind.ClearSelection, released);
    }

    [Fact]
    public void should_pan_when_empty_left_moves_8_1()
    {
        var (during, released) = Run(Empty(), 8.1);

        Assert.Equal(GraphGestureKind.Pan, during);
        Assert.Equal(GraphGestureKind.Pan, released);
    }

    [Fact]
    public void should_select_when_draggable_node_left_released_within_7_9()
    {
        var (during, released) = Run(Node(canDrag: true), 7.9);

        Assert.Equal(GraphGestureKind.None, during);
        Assert.Equal(GraphGestureKind.Select, released);
    }

    [Fact]
    public void should_drag_node_when_draggable_node_left_moves_8_1()
    {
        var (during, released) = Run(Node(canDrag: true), 8.1);

        Assert.Equal(GraphGestureKind.NodeDrag, during);
        Assert.Equal(GraphGestureKind.NodeDrag, released);
    }

    [Fact]
    public void should_select_when_l0_node_left_released_within_7_9()
    {
        var (_, released) = Run(Node(canDrag: false), 7.9);

        Assert.Equal(GraphGestureKind.Select, released);
    }

    [Fact]
    public void should_pan_when_l0_node_left_moves_8_1()
    {
        var (during, released) = Run(Node(canDrag: false), 8.1);

        Assert.Equal(GraphGestureKind.Pan, during);
        Assert.Equal(GraphGestureKind.Pan, released);
    }

    [Theory]
    [InlineData(GraphPressTarget.Empty)]
    [InlineData(GraphPressTarget.Node)]
    public void should_pan_immediately_without_deadzone_when_middle_button(GraphPressTarget target)
    {
        var gesture = GraphGesture.Begin(new GraphPress(target, GraphPointerButton.Middle, false, CanDragNode: true), Origin);

        Assert.True(gesture.IsActive);                                          // 누른 순간 팬
        Assert.Equal(GraphGestureKind.Pan, gesture.Kind);
        Assert.Equal(GraphGestureKind.Pan, gesture.Move(new Point(101, 100)));  // 1 DIU 도 팬
        Assert.Equal(GraphGestureKind.Pan, gesture.Release(new Point(101, 100)));
    }

    [Fact]
    public void should_pan_when_space_held_and_node_left_moves_8_1()
    {
        var (during, released) = Run(Node(canDrag: true, space: true), 8.1);

        Assert.Equal(GraphGestureKind.Pan, during);
        Assert.Equal(GraphGestureKind.Pan, released);
    }

    [Fact]
    public void should_do_nothing_when_space_held_and_released_within_7_9()
    {
        var (_, released) = Run(Node(canDrag: true, space: true), 7.9);

        Assert.Equal(GraphGestureKind.None, released);   // S4 표: Space 행의 '8 미만' 칸은 "—"
    }

    [Fact]
    public void should_do_nothing_when_right_button()
    {
        var (during, released) = Run(Node(canDrag: true, GraphPointerButton.Right), 50);

        Assert.Equal(GraphGestureKind.None, during);
        Assert.Equal(GraphGestureKind.None, released);
    }
    #endregion

    #region - 결정 불변 · 데드존 공유 -
    [Fact]
    public void should_keep_initial_decision_when_pointer_crosses_node()
    {
        // Arrange — 빈 곳에서 눌러 팬이 시작됐다
        var gesture = GraphGesture.Begin(Empty(), Origin);
        Assert.Equal(GraphGestureKind.Pan, gesture.Move(new Point(120, 100)));

        // Act — 포인터가 노드 위를 지나 되돌아와도, 데드존 안으로 돌아와도
        var overNode = gesture.Move(new Point(160, 130));
        var backInside = gesture.Move(new Point(101, 101));
        var released = gesture.Release(new Point(101, 101));

        // Assert — 누른 순간의 결정(팬)이 끝까지 간다. 제스처는 누른 뒤의 히트를 묻지 않는다.
        Assert.Equal(GraphGestureKind.Pan, overNode);
        Assert.Equal(GraphGestureKind.Pan, backInside);
        Assert.Equal(GraphGestureKind.Pan, released);
    }

    [Fact]
    public void should_stay_node_drag_when_pointer_returns_inside_deadzone()
    {
        var gesture = GraphGesture.Begin(Node(canDrag: true), Origin);
        gesture.Move(new Point(100, 120));

        Assert.Equal(GraphGestureKind.NodeDrag, gesture.Release(Origin));
    }

    [Theory]
    [InlineData(5.6, 5.6, false)]    // 7.92
    [InlineData(5.7, 5.7, true)]     // 8.06
    [InlineData(0, -8, false)]       // 정확히 8 은 아직 클릭
    [InlineData(0, -8.01, true)]
    public void should_share_kernel_deadzone_when_diagonal(double dx, double dy, bool expectedActive)
    {
        var gesture = GraphGesture.Begin(Node(canDrag: true), Origin);

        gesture.Move(new Point(Origin.X + dx, Origin.Y + dy));

        Assert.Equal(expectedActive, gesture.IsActive);
        Assert.Equal(DragMath.IsDrag(dx, dy), gesture.IsActive);
    }

    [Fact]
    public void should_expose_press_facts_when_begun()
    {
        var press = Node(canDrag: true);

        var gesture = GraphGesture.Begin(press, Origin);

        Assert.Equal(press, gesture.Press);
        Assert.Equal(Origin, gesture.Origin);
        Assert.False(gesture.IsActive);
        Assert.Equal(GraphGestureKind.None, gesture.Kind);
    }

    [Fact]
    public void should_ignore_moves_after_release()
    {
        var gesture = GraphGesture.Begin(Empty(), Origin);
        Assert.Equal(GraphGestureKind.ClearSelection, gesture.Release(Origin));

        Assert.Equal(GraphGestureKind.None, gesture.Move(new Point(300, 300)));
        Assert.Equal(GraphGestureKind.None, gesture.Release(new Point(300, 300)));
    }
    #endregion
}
