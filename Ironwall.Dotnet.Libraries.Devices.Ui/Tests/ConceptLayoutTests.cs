using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Concept;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>개념도 가로 띠가 많은 센서(PIDS 50대)에서도 칸 안에 들어가는가(fence-wiring-editor 검토 V1) — 순수 배치 함수.</summary>
public class ConceptLayoutTests
{
    [Fact]
    public void should_wrap_fifty_nodes_into_snake_rows_inside_the_width_and_keep_the_controller_visible_when_the_strip_is_narrow()
    {
        // Arrange
        var keys = Enumerable.Range(101, 50).ToList();

        // Act
        var g = ConceptLayout.Strip(keys, new Size(1000, 138));

        // Assert — 넘치지 않는다: 노드 · 제어기 모두 보이는 폭 안
        Assert.True(g.Rows > 1);
        Assert.All(g.Nodes, n => Assert.InRange(n.Center.X, ConceptLayout.NODE_R, 1000 - ConceptLayout.NODE_R));
        Assert.InRange(g.Controller.Left, 0, 1000);
        Assert.InRange(g.Controller.Right, 0, 1000);
        Assert.Equal(1000, g.Extent.Width);
        Assert.True(g.Controller.Top > g.Nodes.Max(n => n.Center.Y) + ConceptLayout.NODE_R);   // 제어기는 마지막 줄 아래
        // 뱀 모양 — 꺾이는 곳의 두 노드는 같은 x(흐름이 아래로 이어진다), 둘째 줄은 오른쪽 → 왼쪽
        var (_, columns, _) = ConceptLayout.StripGrid(50, 1000);
        Assert.Equal(g.Nodes[columns - 1].Center.X, g.Nodes[columns].Center.X, 3);
        Assert.True(g.Nodes[columns + 1].Center.X < g.Nodes[columns].Center.X);
        Assert.True(ConceptLayout.StripHeight(50, 1000, 120) > 138);                              // 띠가 키를 키워 달라고 한다
    }

    [Fact]
    public void should_keep_a_single_row_with_the_old_spacing_when_the_nodes_fit()
    {
        var g = ConceptLayout.Strip(Enumerable.Range(101, 8).ToList(), new Size(1000, 138));

        Assert.Equal(1, g.Rows);
        Assert.Single(g.Nodes.Select(n => n.Center.Y).Distinct());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(30)]
    [InlineData(49)]
    public void should_find_the_gap_after_a_node_when_the_pointer_is_just_past_it_along_the_snake(int index)
    {
        // Arrange
        var g = ConceptLayout.Strip(Enumerable.Range(101, 50).ToList(), new Size(1000, 138));
        var node = g.Nodes[index].Center;
        var row = g.Nodes.Select(n => n.Center.Y).Distinct().OrderBy(y => y).ToList().IndexOf(node.Y);
        var past = new Point(node.X + (row % 2 == 0 ? 6 : -6), node.Y);

        // Act
        var gap = ConceptLayout.GapAt(g, past);

        // Assert — 그 노드 바로 뒤 틈
        Assert.Equal(index + 1, gap);
    }

    [Fact]
    public void should_draw_one_flow_arrow_per_link_including_the_row_turns_when_the_strip_wraps()
    {
        var g = ConceptLayout.Strip(Enumerable.Range(101, 50).ToList(), new Size(1000, 138));
        var nodes = g.Nodes.Select((p, i) => new Consoles.Wiring.ConceptNodeInfo(p.Key, p.Key, i + 1, i + 1, $"#{i + 1}", false, string.Empty,
            Consoles.Wiring.Signals.SignalLevel.Unknown, false, false, false)).ToList();

        var shapes = ConceptScene.Background(g, nodes);

        Assert.Equal(49, shapes.Count(s => s.Ink == Consoles.Wiring.Fence.FenceInk.ConceptArrow));
        Assert.Equal(2, shapes.Count(s => s.Ink == Consoles.Wiring.Fence.FenceInk.ConceptReturn));
    }
}
