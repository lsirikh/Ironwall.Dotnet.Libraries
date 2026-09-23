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

    #region 툴바 검색 오버플로 — D-23 (ResolveToolbarSearchMode)
    // 왼쪽(897) · 오른쪽(130) 클러스터 폭은 EventsConsolePreview --snapshot 실측값이다(서랍 1150px ·
    // "직접" 상태 — 날짜 범위 필드가 필터에 포함될 때). 그 값을 고정해 두고 툴바 자신의 폭(=목록 칸 폭,
    // Docked/Drawer/Compact 세 모드 각각의 실제 ListWidth)만 바꿔 가며 판정한다.
    private const double EventsLeftCluster = 897;
    private const double EventsRightCluster = 130;

    [Fact]
    public void should_stay_full_when_toolbar_is_wide_enough_for_everything()
    {
        var mode = ConsoleLayoutMath.ResolveToolbarSearchMode(toolbarWidth: 1400, leftClusterWidth: 200, rightClusterWidth: 100);

        Assert.Equal(ConsoleToolbarSearchMode.Full, mode);
    }

    [Fact]
    public void should_collapse_to_icon_when_docked_minimum_width_is_too_narrow_for_heavy_filters()
    {
        // Docked 최소 1280, 상세 기본 340 → ListWidth = 1280 - 184 - 340 = 756.
        var layout = ConsoleLayoutMath.Resolve(1280, requestedDetailWidth: 340);

        var mode = ConsoleLayoutMath.ResolveToolbarSearchMode(layout.ListWidth, EventsLeftCluster, EventsRightCluster);

        Assert.Equal(ConsoleLayoutMode.Docked, layout.Mode);
        Assert.Equal(ConsoleToolbarSearchMode.IconOnly, mode);
    }

    [Fact]
    public void should_collapse_to_icon_when_drawer_1150_because_right_cluster_would_run_off_screen()
    {
        // D-23 재현 폭 — 서랍(1150), 실측: 왼쪽 897 + 오른쪽 130 을 두면 66px 밖에 안 남는다.
        var layout = ConsoleLayoutMath.Resolve(1150);

        var mode = ConsoleLayoutMath.ResolveToolbarSearchMode(layout.ListWidth, EventsLeftCluster, EventsRightCluster);

        Assert.Equal(ConsoleLayoutMode.Drawer, layout.Mode);
        Assert.Equal(ConsoleToolbarSearchMode.IconOnly, mode);
    }

    [Fact]
    public void should_collapse_to_icon_when_compact_900_and_filters_are_heavy()
    {
        var layout = ConsoleLayoutMath.Resolve(900);

        var mode = ConsoleLayoutMath.ResolveToolbarSearchMode(layout.ListWidth, EventsLeftCluster, EventsRightCluster);

        Assert.Equal(ConsoleLayoutMode.Compact, layout.Mode);
        Assert.Equal(ConsoleToolbarSearchMode.IconOnly, mode);
    }

    [Fact]
    public void should_stay_full_when_compact_900_and_filters_are_light()
    {
        // 필터가 없거나 가벼운 콘솔(칩 없음)은 900 에서도 검색을 접을 필요가 없다 — 모드가 아니라
        // 실제로 쓰는 폭으로 판정하기 때문(전 콘솔에 같은 폭 문턱을 강제하지 않는다).
        var layout = ConsoleLayoutMath.Resolve(900);

        var mode = ConsoleLayoutMath.ResolveToolbarSearchMode(layout.ListWidth, leftClusterWidth: 260, rightClusterWidth: 90);

        Assert.Equal(ConsoleToolbarSearchMode.Full, mode);
    }

    // 예산 = toolbarWidth - ToolbarHorizontalPadding(24) - ToolbarSearchLeftMargin(12) - left - right.
    // left=right=0 이면 예산 = toolbarWidth - 36.
    [Theory]
    [InlineData(156, ConsoleToolbarSearchMode.Full)]        // 예산이 정확히 최소폭(120) — 아직 전체
    [InlineData(155.9, ConsoleToolbarSearchMode.IconOnly)]  // 0.1 모자라면 접는다
    public void should_use_full_min_width_as_the_exact_boundary(double toolbarWidth, ConsoleToolbarSearchMode expected)
    {
        var mode = ConsoleLayoutMath.ResolveToolbarSearchMode(toolbarWidth, leftClusterWidth: 0, rightClusterWidth: 0);

        Assert.Equal(expected, mode);
    }

    [Fact]
    public void should_treat_nan_or_negative_inputs_as_zero_when_resolving_search_mode()
    {
        var mode = ConsoleLayoutMath.ResolveToolbarSearchMode(double.NaN, double.NaN, double.NaN);

        // 예산이 0 이하로 떨어진다 → 접힘. 입력을 못 믿어도 예외 없이 안전한 쪽(IconOnly)으로 떨어진다.
        Assert.Equal(ConsoleToolbarSearchMode.IconOnly, mode);
    }

    #region 예산을 넘지 않는 연속값 — ResolveToolbarSearchMinWidth (고정 2단계 대신 실측으로 고친 부분)
    // 실측(ToolbarProbe 하네스, D-23) — 왼쪽 750.7 · 오른쪽 84 인 채 900px 로 좁히면 예산이 13.3px 뿐이다.
    // 옛 설계(전체 120 / 아이콘 32 고정 2단계)는 여기서 32 를 강제해 18.7px 이 다시 넘쳤다 — 그래서
    // 최소폭은 "예산 그대로"를 쓴다(0~120 사이의 아무 값이나 될 수 있다), 고정 단계가 없다.
    [Fact]
    public void should_return_the_exact_budget_when_budget_is_between_zero_and_full()
    {
        var minWidth = ConsoleLayoutMath.ResolveToolbarSearchMinWidth(toolbarWidth: 884, leftClusterWidth: 750.7, rightClusterWidth: 84);

        Assert.Equal(13.3, minWidth, 1);
    }

    [Fact]
    public void should_clamp_to_zero_when_left_and_right_clusters_alone_already_exceed_the_toolbar()
    {
        var minWidth = ConsoleLayoutMath.ResolveToolbarSearchMinWidth(toolbarWidth: 200, leftClusterWidth: 150, rightClusterWidth: 100);

        Assert.Equal(0, minWidth);
    }

    [Fact]
    public void should_cap_at_full_min_width_when_budget_is_larger_than_needed()
    {
        var minWidth = ConsoleLayoutMath.ResolveToolbarSearchMinWidth(toolbarWidth: 2000, leftClusterWidth: 100, rightClusterWidth: 100);

        Assert.Equal(ConsoleLayoutMath.ToolbarSearchFullMinWidth, minWidth);
    }

    [Theory]
    [InlineData(1150, 897, 130)]     // D-23 재현 폭 — 서랍(1150) · "직접" (예산 87 — 이룰 수 있다)
    [InlineData(1280, 897, 130)]     // Docked 최소 (상세 폭에 따라 목록이 이보다 좁을 수도 있지만, 여기선
                                      // 툴바 자신의 폭을 1280 으로 바로 준 경우 — 예산 227
    [InlineData(884, 750.7, 84)]     // ToolbarProbe 실측값 — 예전 2단계 설계가 18.7px 넘쳤던 바로 그 폭(예산 13.3)
    [InlineData(200, 0, 0)]
    public void should_never_ask_for_more_than_the_grid_can_actually_give(double toolbarWidth, double left, double right)
    {
        // 회귀 가드 — 왼쪽 · 오른쪽 클러스터만으로도 이미 다 채워지지 않는(=이룰 수 있는) 폭에서는,
        // 검색이 얼마를 더 요구하든 총합이 툴바 자신의 폭을 넘으면 안 된다(넘으면 Extra=기본 액션이
        // 다시 화면 밖으로 밀린다). 왼쪽+오른쪽만으로 이미 넘치는 경우는 검색과 무관한 별개 문제라
        // should_clamp_to_zero_when_left_and_right_clusters_alone_already_exceed_the_toolbar 에서 다룬다.
        var minWidth = ConsoleLayoutMath.ResolveToolbarSearchMinWidth(toolbarWidth, left, right);

        var gridDemand = ConsoleLayoutMath.ToolbarHorizontalPadding + left
            + (minWidth + ConsoleLayoutMath.ToolbarSearchLeftMargin) + right;

        Assert.True(gridDemand <= toolbarWidth + 0.01,
            $"검색이 {minWidth:0.0} 를 요구해 총 {gridDemand:0.0} 이 필요한데 툴바는 {toolbarWidth:0.0} 뿐이다.");
    }
    #endregion
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
