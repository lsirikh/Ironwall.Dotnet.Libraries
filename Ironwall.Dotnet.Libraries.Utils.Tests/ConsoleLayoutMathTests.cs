using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// console-kernel FR-01~03 — 폭별 배치 판정은 이 순수 함수 한 곳에서 한다
/// (설계 정본 window-layout-system-storyboard.html L2175-2181 · L981-989).
/// </summary>
public class ConsoleLayoutMathTests
{
    [Theory]
    [InlineData(1600, ConsoleLayoutMode.Docked)]
    [InlineData(1280, ConsoleLayoutMode.Docked)]
    [InlineData(1279.9, ConsoleLayoutMode.Drawer)]
    [InlineData(960, ConsoleLayoutMode.Drawer)]
    [InlineData(959.9, ConsoleLayoutMode.Compact)]
    [InlineData(400, ConsoleLayoutMode.Compact)]
    public void should_pick_mode_by_width_when_resolving(double width, ConsoleLayoutMode expected)
    {
        Assert.Equal(expected, ConsoleLayoutMath.Resolve(width).Mode);
    }

    [Theory]
    [InlineData(1280, 184)]
    [InlineData(960, 184)]
    [InlineData(959, 56)]
    public void should_collapse_rail_only_when_compact(double width, double expectedRail)
    {
        Assert.Equal(expectedRail, ConsoleLayoutMath.Resolve(width).RailWidth);
    }

    [Theory]
    [InlineData(340, 340)]
    [InlineData(100, 300)]
    [InlineData(300, 300)]
    [InlineData(480, 480)]
    [InlineData(900, 480)]
    [InlineData(double.NaN, 340)]
    public void should_clamp_detail_width_when_out_of_range(double requested, double expected)
    {
        Assert.Equal(expected, ConsoleLayoutMath.ClampDetailWidth(requested));
    }

    [Fact]
    public void should_dock_detail_at_requested_width_when_wide()
    {
        var layout = ConsoleLayoutMath.Resolve(1280, requestedDetailWidth: 420);

        Assert.True(layout.IsDetailDocked);
        Assert.True(layout.IsSplitterVisible);
        Assert.Equal(420, layout.DetailWidth);
        Assert.Equal(1280 - 184 - 420, layout.ListWidth);
    }

    [Theory]
    [InlineData(1200, 360)]     // min(360, 86%) — 86% 가 더 크다
    [InlineData(400, 344)]      // 400 * 0.86
    public void should_size_drawer_as_min_of_360_and_86_percent(double width, double expected)
    {
        var layout = ConsoleLayoutMath.Resolve(width);

        Assert.False(layout.IsDetailDocked);
        Assert.False(layout.IsSplitterVisible);
        Assert.Equal(expected, layout.DetailWidth, 3);
    }

    [Fact]
    public void should_give_list_full_width_when_detail_is_a_drawer()
    {
        var layout = ConsoleLayoutMath.Resolve(1100);

        Assert.Equal(1100 - 184, layout.ListWidth);   // 서랍은 목록 위에 겹친다 — 목록을 밀지 않는다
    }

    [Theory]
    [InlineData(ConsoleLayoutMode.Docked, 0, false, true)]    // 도킹이면 늘 보인다(선택 없음 상태를 그린다)
    [InlineData(ConsoleLayoutMode.Drawer, 0, false, false)]
    [InlineData(ConsoleLayoutMode.Drawer, 1, false, true)]
    [InlineData(ConsoleLayoutMode.Drawer, 0, true, true)]     // 새로 등록
    [InlineData(ConsoleLayoutMode.Compact, 3, false, true)]
    public void should_open_drawer_only_when_selected_or_creating(ConsoleLayoutMode mode, int selected, bool creating, bool expected)
    {
        Assert.Equal(expected, ConsoleLayoutMath.IsDetailOpen(mode, selected, creating));
    }

    [Theory]
    [InlineData(340, -10, 350)]   // ← : 넓힌다 (경계가 왼쪽으로)
    [InlineData(340, 10, 330)]    // → : 좁힌다
    [InlineData(475, -10, 480)]
    [InlineData(305, 10, 300)]
    public void should_move_splitter_by_key_step_within_range(double current, double splitterDelta, double expected)
    {
        Assert.Equal(expected, ConsoleLayoutMath.DetailWidthAfterSplitterMove(current, splitterDelta));
    }

    [Fact]
    public void should_never_return_negative_list_width_when_window_is_tiny()
    {
        Assert.True(ConsoleLayoutMath.Resolve(200).ListWidth >= 0);
        Assert.True(ConsoleLayoutMath.Resolve(0).ListWidth >= 0);
    }
}
