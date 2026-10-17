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

    #region 목록 오른쪽 인셋 — 서랍이 덮을 때 목록이 피해야 할 폭
    [Fact]
    public void should_return_zero_inset_when_docked_because_detail_has_its_own_column()
    {
        var layout = ConsoleLayoutMath.Resolve(1280);

        Assert.Equal(0, ConsoleLayoutMath.ListRightInset(layout, isDetailOpen: true));
    }

    [Fact]
    public void should_return_zero_inset_when_drawer_is_closed_because_nothing_to_avoid()
    {
        var layout = ConsoleLayoutMath.Resolve(1100);

        Assert.Equal(0, ConsoleLayoutMath.ListRightInset(layout, isDetailOpen: false));
    }

    [Fact]
    public void should_return_drawer_width_as_inset_when_drawer_is_open()
    {
        var layout = ConsoleLayoutMath.Resolve(1100);

        Assert.Equal(layout.DetailWidth, ConsoleLayoutMath.ListRightInset(layout, isDetailOpen: true));
        Assert.True(ConsoleLayoutMath.ListRightInset(layout, isDetailOpen: true) > 0);
    }

    [Fact]
    public void should_return_drawer_width_as_inset_when_compact_and_open()
    {
        var layout = ConsoleLayoutMath.Resolve(900);

        Assert.Equal(ConsoleLayoutMode.Compact, layout.Mode);
        Assert.Equal(layout.DetailWidth, ConsoleLayoutMath.ListRightInset(layout, isDetailOpen: true));
    }
    #endregion

    #region 목록 실효 폭 — ConsoleShell.EffectiveListWidth 계약이 소비하는 순수 판정
    [Fact]
    public void should_equal_list_width_when_docked_because_inset_is_always_zero()
    {
        var layout = ConsoleLayoutMath.Resolve(1400);

        Assert.Equal(layout.ListWidth, ConsoleLayoutMath.EffectiveListWidth(layout, isDetailOpen: true));
    }

    [Fact]
    public void should_equal_list_width_when_drawer_is_closed()
    {
        var layout = ConsoleLayoutMath.Resolve(1100);

        Assert.Equal(layout.ListWidth, ConsoleLayoutMath.EffectiveListWidth(layout, isDetailOpen: false));
    }

    [Fact]
    public void should_subtract_the_drawer_width_when_drawer_is_open()
    {
        var layout = ConsoleLayoutMath.Resolve(1100);

        Assert.Equal(layout.ListWidth - layout.DetailWidth, ConsoleLayoutMath.EffectiveListWidth(layout, isDetailOpen: true));
    }

    [Fact]
    public void should_subtract_the_drawer_width_when_compact_and_open()
    {
        var layout = ConsoleLayoutMath.Resolve(900);

        Assert.Equal(ConsoleLayoutMode.Compact, layout.Mode);
        Assert.Equal(layout.ListWidth - layout.DetailWidth, ConsoleLayoutMath.EffectiveListWidth(layout, isDetailOpen: true));
    }

    [Fact]
    public void should_never_go_negative_when_the_drawer_is_wider_than_the_list()
    {
        var layout = ConsoleLayoutMath.Resolve(0);

        Assert.True(ConsoleLayoutMath.EffectiveListWidth(layout, isDetailOpen: true) >= 0);
    }
    #endregion

    #region 툴바 두 줄 — 필터는 첫째 줄 예산 밖 (D-2026-09-27-71c353)
    // 옛 D-23 실측(이벤트 서랍 1150 · "직접"): 왼쪽 묶음 897 = [삭제][갱신] + 기간 칩 + 날짜 범위. 그중 필터가 약 800 이었다.
    // 두 줄 구조에서 필터는 둘째 줄에 서므로 첫째 줄 판정의 입력이 아니다 — 같은 화면이 이제는 검색 전체 폭을 받는다.
    private const double EventsFixedLeft = 96, EventsExtra = 130;

    [Fact]
    public void should_not_take_a_filter_width_as_input_so_filters_cannot_squeeze_the_action_row()
    {
        // Arrange + Act — 판정 함수의 입력에 필터 폭이 없다(시그니처가 계약이다)
        var parameters = typeof(ConsoleLayoutMath).GetMethod(nameof(ConsoleLayoutMath.ResolveToolbarFit))!.GetParameters();

        // Assert
        Assert.DoesNotContain(parameters, p => p.Name!.Contains("filter", StringComparison.OrdinalIgnoreCase));
        Assert.False(Enum.IsDefined(typeof(ConsoleToolbarOverflow), "Filters"), "필터는 더 이상 [⋯] 로 옮길 대상이 아니다");
    }

    [Theory]
    [InlineData(1280, false)]
    [InlineData(1150, false)]
    [InlineData(1150, true)]
    [InlineData(900, false)]
    [InlineData(900, true)]
    public void should_keep_the_events_search_at_full_width_where_the_one_row_toolbar_folded_it(double surface, bool drawerOpen)
    {
        // Arrange — 옛 한 줄 판정은 이 폭들에서 모두 아이콘으로 접었다(D-23 시험들)
        var width = ConsoleLayoutMath.EffectiveListWidth(ConsoleLayoutMath.Resolve(surface), drawerOpen);

        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(width, EventsFixedLeft, EventsExtra, 0, showSearch: true);

        // Assert
        Assert.False(fit.IsSearchCompact);
        Assert.True(fit.SearchWidth >= ConsoleLayoutMath.ToolbarSearchFullMinWidth, $"목록 {width} → 검색 {fit.SearchWidth}");
    }

    [Theory]
    [InlineData(24 + 12 + 180, 180, false)]           // 예산이 정확히 최소폭 — 전체
    [InlineData(24 + 12 + 179.9, 0, true)]            // 0.1 모자라면 아이콘(180 아래로 누르지 않는다)
    [InlineData(24 + 12 + 300, 240, false)]           // 넉넉하면 편한 폭 240 에서 멈춘다(내용에 맞춰 늘지 않는다)
    public void should_use_the_full_minimum_as_the_exact_boundary(double toolbarWidth, double expectedWidth, bool expectedCompact)
    {
        // Act — 왼쪽 · Extra · [열] 없음: 예산 = 폭 − 24 − 12
        var fit = ConsoleLayoutMath.ResolveToolbarFit(toolbarWidth, 0, 0, 0, showSearch: true);

        // Assert
        Assert.Equal(expectedCompact, fit.IsSearchCompact);
        Assert.Equal(expectedWidth, fit.SearchWidth, 3);
    }

    [Fact]
    public void should_keep_the_search_minimum_comfortably_above_the_old_squeezed_width()
    {
        // 실창 캡처(022 · 024 · 007 · 032)에서 검색은 늘 120 으로 눌려 안내 · 입력 글이 테두리에 닿았다.
        Assert.True(ConsoleLayoutMath.ToolbarSearchFullMinWidth >= 180);
        Assert.True(ConsoleLayoutMath.ToolbarSearchPreferredWidth >= ConsoleLayoutMath.ToolbarSearchFullMinWidth);
        Assert.True(ConsoleLayoutMath.ToolbarSearchFullMaxWidth >= ConsoleLayoutMath.ToolbarSearchPreferredWidth);
    }
    #endregion

    #region 40(48)px 띠 고정 — D-23
    // Generic.xaml 의 툴바 Border 는 Height(고정) = ToolbarHeight 를 쓴다. Padding "12,8" 이 위아래
    // 16 을 먹으므로, 안쪽에 32(InputHeight, 버튼·검색 높이) 를 넣고 나면 남는 여유가 정확히 0 이어야
    // 아무 것도 띠를 못 키운다 — 이 관계가 깨지면(예: InputHeight 만 올리고 ToolbarHeight 를 안 올리면)
    // 버튼·검색이 잘리기 시작한다는 뜻이라 여기서 잡는다.
    [Fact]
    public void should_have_no_slack_when_toolbar_band_holds_one_input_row()
    {
        const double verticalPadding = 16; // Generic.xaml Border Padding="12,8" 의 위 8 + 아래 8

        Assert.Equal(ConsoleLayoutMath.ToolbarHeight, ConsoleLayoutMath.InputHeight + verticalPadding);
    }
    #endregion
}
