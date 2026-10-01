using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 겉모양(창 정리 2026-10-01) — 알림 한 줄 접기 · 미배치 띠 · 3D 보기/개념도 나눔 · 속성 칸 접기 · 다른 망 단축키(헤디드 r22) — 뷰모델 · 순수 함수.
/// </summary>
public class WiringLayoutChromeTests
{
    /// <summary>링 센서 <paramref name="placed"/> 대(저장된 배치 있음) + <paramref name="unsaved"/> 대(배치 없음 → 번호순 제안). 펜스 구성은 제안.</summary>
    internal static WiringViewModel Build(int placed = 4, int unsaved = 0)
    {
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < placed + unsaved; i++)
            seeds.Add(new WiringSensorSeed(101 + i, i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}구간", "SmartSensor2", "북측 7구간"),
                i < placed ? new WiringPlacement(1, i + 1) : null));
        return WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"), seeds,
            new[] { "SmartSensor2" }, null, new WiringFakeDialogs { Confirm = true }, fence: new WiringFenceContext(null, new FakeFenceStore(), null));
    }

    #region - Notices -
    [Fact]
    public void should_order_notices_actions_first_then_warnings_then_info_and_drop_empty_ones()
    {
        var notices = new[]
        {
            new WiringNotice(WiringNoticeKind.Fence, "펜스 구성 제안", false),
            new WiringNotice(WiringNoticeKind.Legacy, "옛 배치", true),
            new WiringNotice(WiringNoticeKind.Bands, "대역 없음", false),
            new WiringNotice(WiringNoticeKind.Topology, " ", false),
            new WiringNotice(WiringNoticeKind.Suggestion, "제안", false),
            new WiringNotice(WiringNoticeKind.Applied, "함께 적용", true),
        };

        var ordered = WiringLayoutMath.OrderNotices(notices);

        Assert.Equal(new[] { WiringNoticeKind.Suggestion, WiringNoticeKind.Bands, WiringNoticeKind.Applied, WiringNoticeKind.Legacy, WiringNoticeKind.Fence },
            ordered.Select(n => n.Kind));
        Assert.Equal("알림 5", WiringLayoutMath.NoticeChipText(ordered.Count));
        Assert.Equal(string.Empty, WiringLayoutMath.NoticeChipText(0));
        Assert.Equal(("이대로 적용", "대역 고르기", false), (ordered[0].ActionText, ordered[1].ActionText, ordered[2].HasAction));
    }

    [Fact]
    public void should_collapse_three_notices_into_one_row_with_the_suggestion_first_and_the_rest_behind_the_chip()
    {
        // Arrange — 펜스 구성 제안 · 번호 대역 미설정 · 번호순 제안(배치 없는 3대) — 렌더 검토 그림과 같은 세 알림
        var vm = Build(placed: 4, unsaved: 3);
        vm.GoWiring();

        // Assert
        Assert.Equal(new[] { WiringNoticeKind.Suggestion, WiringNoticeKind.Bands, WiringNoticeKind.Fence }, vm.Notices.Select(n => n.Kind));
        Assert.Equal("알림 3", vm.NoticeChipText);
        Assert.True(vm.HasMoreNotices);
        Assert.True(vm.IsSuggestionPrimary);
        Assert.False(vm.IsBandsPrimary || vm.IsFencePrimary);
    }

    [Fact]
    public void should_promote_the_next_notice_when_the_primary_one_is_resolved()
    {
        var vm = Build(placed: 4, unsaved: 3);
        vm.GoWiring();

        vm.AcceptSuggestion();

        Assert.False(vm.IsSuggestionPrimary);
        Assert.True(vm.IsBandsPrimary);                                                   // 다음 할 일 — 번호 대역
        Assert.Equal("알림 2", vm.NoticeChipText);
    }

    [Fact]
    public void should_run_the_notice_action_from_the_list_and_close_it()
    {
        var vm = Build();
        vm.GoWiring();
        vm.IsNoticeListOpen = true;

        vm.RunNoticeAction(WiringNoticeKind.Bands);

        Assert.False(vm.IsNoticeListOpen);
        Assert.True(vm.IsControllerSelected);                                             // 대역 고르기 = 제어기를 골라 오른쪽 칸에 대역
    }
    #endregion

    #region - Unplaced strip -
    [Fact]
    public void should_keep_the_unplaced_strip_slim_until_a_sensor_is_unplaced()
    {
        var vm = Build();
        vm.GoWiring();
        var slim = (vm.IsUnplacedStripExpanded, vm.UnplacedStripText);

        vm.FenceUnplace(new[] { 102 });

        Assert.Equal((false, "미배치 0 · 전부 결선에 붙었습니다 ✓"), slim);
        Assert.True(vm.IsUnplacedStripExpanded);
        Assert.Equal("미배치 1", vm.UnplacedStripText);
    }

    [Fact]
    public void should_say_where_to_drop_while_a_sensor_drag_is_in_progress()
    {
        var vm = Build();

        vm.NotifySensorDrag(true);
        var during = (vm.IsSensorDragging, vm.BinHintText);
        vm.NotifySensorDrag(false);

        Assert.Equal((true, "여기 놓으면 결선에서 빠집니다"), during);
        Assert.Equal("빼는 곳", vm.BinHintText);
    }

    [Fact]
    public void should_show_fence_counts_in_the_status_bar_only_in_the_fence_view_of_the_wiring_step()
    {
        var vm = Build();
        Assert.False(vm.IsFenceCountsShown);                                             // 센서 단계

        vm.GoWiring();
        var fence = vm.IsFenceCountsShown;
        vm.ShowTableView();

        Assert.True(fence);
        Assert.False(vm.IsFenceCountsShown);
    }
    #endregion

    #region - Split · detail pane -
    [Theory]
    [InlineData(0.58, double.NaN, 0.58)]
    [InlineData(0.05, double.NaN, WiringLayoutMath.MIN_SPLIT)]
    [InlineData(0.99, double.NaN, WiringLayoutMath.MAX_SPLIT)]
    [InlineData(double.NaN, double.NaN, WiringLayoutMath.DEFAULT_SPLIT)]
    [InlineData(0.3, 400.0, 0.4)]               // 위 최소 160 / 400
    [InlineData(0.8, 400.0, 0.625)]             // 아래 최소 150 / 400
    [InlineData(0.7, 250.0, 0.5)]               // 둘 다 못 지키면 가운데
    public void should_clamp_the_split_ratio_to_its_range_and_the_minimum_heights(double ratio, double total, double expected)
        => Assert.Equal(expected, WiringLayoutMath.ClampSplit(ratio, total), 6);

    [Fact]
    public void should_remember_the_split_and_pane_state_through_the_prefs_and_not_save_while_applying_them()
    {
        // Arrange
        var vm = Build();
        var saved = new List<(double, bool)>();

        // Act
        vm.UseLayoutPrefs(0.7, false, (r, open) => saved.Add((r, open)));
        var applied = (vm.FenceSplitRatio, vm.IsDetailPaneOpen, saved.Count);
        var clamped = vm.SetFenceSplitRatio(0.95);
        vm.ToggleDetailPane();

        // Assert
        Assert.Equal((0.7, false, 0), applied);
        Assert.Equal(WiringLayoutMath.MAX_SPLIT, clamped);
        Assert.Equal(new[] { (WiringLayoutMath.MAX_SPLIT, false), (WiringLayoutMath.MAX_SPLIT, true) }, saved);
    }

    [Fact]
    public void should_keep_following_the_selection_while_the_detail_pane_is_collapsed()
    {
        var vm = Build();
        vm.GoWiring();
        vm.ToggleDetailPane();

        vm.FenceSelect(103);
        vm.ToggleDetailPane();

        Assert.True(vm.IsDetailPaneOpen);
        Assert.Equal("›", vm.DetailToggleText);
        Assert.Equal(vm.Board.Find(103)!.Display, vm.SelectedTitle);
    }
    #endregion

    #region - Panel move shortcut (헤디드 r22) -
    [Theory]
    [InlineData(false, Key.Right, ModifierKeys.Control, true)]                       // Ctrl+→ — 주 단축키
    [InlineData(false, Key.Left, ModifierKeys.Control, true)]
    [InlineData(true, Key.Right, ModifierKeys.Alt | ModifierKeys.Shift, true)]      // Alt+Shift+→ — 보조
    [InlineData(true, Key.Right, ModifierKeys.Alt, false)]                          // Alt+→ = 줄 안 한 칸(다른 망 아님)
    [InlineData(false, Key.Right, ModifierKeys.None, false)]
    [InlineData(false, Key.Right, ModifierKeys.Control | ModifierKeys.Shift, false)]
    [InlineData(false, Key.Up, ModifierKeys.Control, false)]
    public void should_treat_ctrl_arrows_as_the_panel_move_and_keep_alt_shift_as_a_secondary_path(bool alt, Key key, ModifierKeys modifiers, bool expected)
        => Assert.Equal(expected, FenceCanvas.IsPanelMoveKey(alt, key, modifiers));
    #endregion
}
