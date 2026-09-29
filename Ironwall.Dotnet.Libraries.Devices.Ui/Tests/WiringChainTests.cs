using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 체인(wiring-fence-view FR-01 ~ FR-03 · FR-16) — 기본 순서 · 옛 배치 변환 · 끼워 넣기/옮기기/빼기 · A/B 번호 · 양쪽 가지 · 바뀐 센서.
/// </summary>
public class WiringChainTests
{
    private static WiringChainSensor S(int key, int number, int line = 0, int order = 0)
        => new(key, key, number, line == 0 ? null : new WiringPlacement(line, order));

    private static WiringChain Ring(params int[] keys) => WiringChain.Create(WiringShape.Ring, keys);

    #region - Default order / suggestion (FR-03) -
    [Fact]
    public void should_order_by_number_then_id_when_numbers_repeat()
    {
        var load = WiringChain.Load(WiringShape.Ring, new[] { S(30, 105), S(10, 105), S(20, 101), S(5, 110) });

        Assert.Equal(new[] { 20, 10, 30, 5 }, load.Chain.Keys);
        Assert.True(load.HasSuggestion);
        Assert.Equal(4, load.Chain.Suggested.Count);
        Assert.False(load.ConvertedFromLegacy);
    }

    [Fact]
    public void should_append_unplaced_as_suggestion_after_saved_chain_when_some_have_placement()
    {
        var load = WiringChain.Load(WiringShape.Ring, new[] { S(1, 900, 1, 1), S(2, 800, 1, 2), S(3, 100), S(4, 50) });

        Assert.Equal(new[] { 1, 2, 4, 3 }, load.Chain.Keys);
        Assert.Equal(new HashSet<int> { 3, 4 }, load.Chain.Suggested.ToHashSet());
    }

    [Fact]
    public void should_leave_unplaced_in_palette_when_suggestion_is_off()
    {
        var load = WiringChain.Load(WiringShape.Ring, new[] { S(1, 1, 1, 1), S(2, 3), S(3, 2) }, suggestUnplaced: false);

        Assert.Equal(new[] { 1 }, load.Chain.Keys);
        Assert.Equal(new[] { 3, 2 }, load.Chain.Unplaced);
        Assert.False(load.HasSuggestion);
    }

    [Fact]
    public void should_clear_suggestion_and_count_as_changed_when_accepted()
    {
        var load = WiringChain.Load(WiringShape.Ring, new[] { S(1, 1), S(2, 2) });
        var baseline = new Dictionary<int, WiringPlacement?> { [1] = null, [2] = null };

        Assert.Empty(load.Chain.ChangedKeys(baseline));                 // 제안은 저장 대기가 아니다(O-3)

        var accepted = load.Chain.AcceptSuggestions();
        Assert.Empty(accepted.Suggested);
        Assert.Equal(new[] { 1, 2 }, accepted.ChangedKeys(baseline).OrderBy(k => k));
    }
    #endregion

    #region - Legacy conversion (FR-02) -
    [Fact]
    public void should_append_second_line_reversed_when_legacy_two_line_placement_is_loaded()
    {
        // 1차 [a1, b2, c3] + 2차 [e1, d2] — 2차 1번(e)은 Sensor B 바로 옆이라 체인 맨 끝에 와야 한다.
        const int a = 1, b = 2, c = 3, d = 4, e = 5;
        var load = WiringChain.Load(WiringShape.Ring, new[]
        {
            S(a, 1, 1, 1), S(b, 2, 1, 2), S(c, 3, 1, 3), S(e, 5, 2, 1), S(d, 4, 2, 2),
        });

        Assert.True(load.ConvertedFromLegacy);
        Assert.Equal(new[] { a, b, c, d, e }, load.Chain.Keys);
        Assert.Equal(1, load.Chain.NumberOf(e)!.OppositeOrder);      // 옛 2차 1번 = 새 B1
        Assert.Equal(2, load.Chain.NumberOf(d)!.OppositeOrder);
        Assert.Equal(1, load.Chain.NumberOf(a)!.Order);
        Assert.Empty(load.Chain.Suggested);
    }

    [Fact]
    public void should_not_flag_legacy_when_only_first_line_is_saved()
    {
        var load = WiringChain.Load(WiringShape.Ring, new[] { S(1, 1, 1, 2), S(2, 2, 1, 1) });

        Assert.False(load.ConvertedFromLegacy);
        Assert.Equal(new[] { 2, 1 }, load.Chain.Keys);
    }

    [Fact]
    public void should_send_duplicate_claim_to_palette_with_critical_issue_when_two_sensors_share_a_place()
    {
        var load = WiringChain.Load(WiringShape.Ring, new[] { S(1, 20, 1, 1), S(2, 10, 1, 1), S(3, 30, 1, 2) });

        Assert.Equal(new[] { 2, 3 }, load.Chain.Keys);                 // 번호가 앞선 센서가 자리를 갖는다
        Assert.Equal(new[] { 1 }, load.Chain.Unplaced);
        var issue = Assert.Single(load.Issues);
        Assert.Equal(WiringIssueLevel.Critical, issue.Level);
        Assert.Equal(1, issue.Key);
    }

    [Fact]
    public void should_close_up_and_warn_when_saved_orders_have_a_gap()
    {
        var load = WiringChain.Load(WiringShape.Ring, new[] { S(1, 1, 1, 1), S(2, 2, 1, 4) });

        Assert.Equal(new[] { 1, 2 }, load.Chain.Keys);
        var issue = Assert.Single(load.Issues);
        Assert.Equal(WiringIssueLevel.Warning, issue.Level);
        Assert.Equal(WiringChain.CODE_GAP, issue.Code);
    }
    #endregion

    #region - Edit -
    [Theory]
    [InlineData(0, new[] { 9, 1, 2, 3 })]
    [InlineData(3, new[] { 1, 2, 3, 9 })]
    [InlineData(1, new[] { 1, 9, 2, 3 })]
    [InlineData(99, new[] { 1, 2, 3, 9 })]     // 범위 밖 → 끝
    [InlineData(-5, new[] { 9, 1, 2, 3 })]     // 음수 → 앞
    public void should_insert_and_push_the_rest_when_inserting_at_gap(int gap, int[] expected)
    {
        var chain = WiringChain.Create(WiringShape.Ring, new[] { 1, 2, 3 }, unplaced: new[] { 9, 8 });

        var next = chain.Insert(9, gap);

        Assert.Equal(expected, next.Keys);
        Assert.Equal(new[] { 8 }, next.Unplaced);
        Assert.Equal(new[] { 1, 2, 3 }, chain.Keys);                   // 원본은 그대로(불변)
    }

    [Theory]
    [InlineData(3, 0, new[] { 3, 1, 2, 4, 5 })]     // 맨 앞으로
    [InlineData(1, 5, new[] { 2, 3, 4, 5, 1 })]     // 맨 뒤로
    [InlineData(2, 1, new[] { 1, 2, 3, 4, 5 })]     // 자기 앞 틈 — 그대로
    [InlineData(2, 2, new[] { 1, 2, 3, 4, 5 })]     // 자기 뒤 틈 — 그대로
    [InlineData(2, 4, new[] { 1, 3, 4, 2, 5 })]
    [InlineData(4, 1, new[] { 1, 4, 2, 3, 5 })]
    [InlineData(1, 42, new[] { 2, 3, 4, 5, 1 })]    // 범위 밖 → 끝
    public void should_move_to_gap_counted_before_removal_when_moving_one(int key, int gap, int[] expected)
        => Assert.Equal(expected, Ring(1, 2, 3, 4, 5).Move(key, gap).Keys);

    [Fact]
    public void should_ignore_move_when_key_is_not_in_chain()
    {
        var chain = Ring(1, 2, 3);
        Assert.Same(chain, chain.Move(77, 0));
    }

    [Fact]
    public void should_keep_chain_order_of_picked_when_moving_many()
    {
        var chain = Ring(1, 2, 3, 4, 5, 6);

        Assert.Equal(new[] { 2, 5, 1, 3, 4, 6 }, chain.MoveMany(new[] { 5, 2 }, 0).Keys);   // 고른 순서가 아니라 체인 순서
        Assert.Equal(new[] { 1, 3, 4, 6, 2, 5 }, chain.MoveMany(new[] { 5, 2 }, 6).Keys);
        Assert.Equal(new[] { 1, 3, 2, 5, 4, 6 }, chain.MoveMany(new[] { 2, 5 }, 3).Keys);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, chain.MoveMany(new[] { 2, 3 }, 2).Keys);   // 무리 안 틈 — 그대로
    }

    [Fact]
    public void should_move_to_palette_and_close_up_when_removed()
    {
        var chain = WiringChain.Create(WiringShape.Ring, new[] { 1, 2, 3 }, unplaced: new[] { 9 });

        var next = chain.Remove(2);

        Assert.Equal(new[] { 1, 3 }, next.Keys);
        Assert.Equal(new[] { 9, 2 }, next.Unplaced);
        Assert.Same(next, next.Remove(2));                             // 없는 것은 그대로
    }

    [Fact]
    public void should_move_existing_to_end_when_appending()
    {
        var chain = WiringChain.Create(WiringShape.Ring, new[] { 1, 2, 3 }, unplaced: new[] { 9 });

        Assert.Equal(new[] { 1, 2, 3, 9 }, chain.Append(9).Keys);
        Assert.Equal(new[] { 2, 3, 1 }, chain.Append(1).Keys);
    }
    #endregion

    #region - Ring numbering (FR-01) -
    [Fact]
    public void should_number_a_from_start_and_b_from_end_when_ring()
    {
        var chain = Ring(10, 20, 30, 40, 50);

        var n = chain.NumberOf(20)!;
        Assert.Equal(2, n.Order);
        Assert.Equal(4, n.OppositeOrder);
        Assert.Equal("A2 · B4", n.Text);
        Assert.Equal(5, chain.NumberOf(10)!.OppositeOrder);
        Assert.Equal(1, chain.NumberOf(50)!.OppositeOrder);
        Assert.Null(chain.NumberOf(99));
    }

    [Fact]
    public void should_keep_enclosure_in_middle_until_moved_by_hand_when_ring()
    {
        var chain = Ring(Enumerable.Range(1, 34).ToArray());
        Assert.Equal(17, chain.ControllerGap);
        Assert.Equal(18, chain.Append(99).Append(98).ControllerGap);

        var moved = chain.WithControllerGap(5);
        Assert.True(moved.IsControllerGapExplicit);
        Assert.Equal(chain.NumberOf(1), moved.NumberOf(1));             // 함체는 표시용 — 번호가 그대로(FR-09)
        Assert.Equal(6, moved.Insert(99, 0).ControllerGap);            // 앞에 끼우면 같은 이웃 사이에 남는다
        Assert.Equal(5, moved.Insert(99, 20).ControllerGap);
        Assert.Equal(34, chain.WithControllerGap(100).ControllerGap);  // 범위 밖 → 끝
    }

    [Fact]
    public void should_write_line_one_with_chain_position_when_ring_saves()
    {
        var chain = Ring(7, 8, 9);

        var placements = chain.ToPlacements();

        Assert.Equal(new WiringPlacement(1, 1), placements[7]);
        Assert.Equal(new WiringPlacement(1, 3), placements[9]);
    }

    [Fact]
    public void should_report_only_sensors_whose_position_changed_when_compared_with_baseline()
    {
        var chain = Ring(1, 2, 3, 4, 5);
        var baseline = chain.ToPlacements();

        var moved = chain.Move(4, 1);                                  // [1,4,2,3,5]

        Assert.Equal(new[] { 2, 3, 4 }, moved.ChangedKeys(baseline).OrderBy(k => k));
        Assert.Equal(new[] { 5 }, chain.Remove(5).ChangedKeys(baseline));
    }

    [Fact]
    public void should_keep_controller_at_left_end_when_line()
    {
        var chain = WiringChain.Create(WiringShape.Line, new[] { 1, 2, 3 }, controllerGap: 2);

        Assert.Equal(0, chain.ControllerGap);
        Assert.Same(chain, chain.WithControllerGap(2));
        Assert.Equal(0, chain.Insert(9, 0).ControllerGap);
        Assert.Equal(new WiringChainNumber(1, 1, 1, null), chain.NumberOf(1));
    }
    #endregion

    #region - Two branch (FR-16 · O-6 잠정) -
    [Fact]
    public void should_number_each_branch_outward_from_controller_when_two_branch()
    {
        // 저장: 선 1(왼쪽) a1 b2 · 선 2(오른쪽) c1 d2 → 화면 [b, a | c, d]
        const int a = 1, b = 2, c = 3, d = 4;
        var load = WiringChain.Load(WiringShape.TwoBranch, new[] { S(a, 1, 1, 1), S(b, 2, 1, 2), S(c, 3, 2, 1), S(d, 4, 2, 2) });
        var chain = load.Chain;

        Assert.False(load.ConvertedFromLegacy);                          // 가지는 옛 배치가 아니다
        Assert.Equal(new[] { b, a, c, d }, chain.Keys);
        Assert.Equal(2, chain.ControllerGap);
        Assert.Equal(new WiringChainNumber(2, 1, 1, null), chain.NumberOf(a));
        Assert.Equal(new WiringChainNumber(1, 1, 2, null), chain.NumberOf(b));
        Assert.Equal(new WiringChainNumber(3, 2, 1, null), chain.NumberOf(c));
        Assert.Equal(new[] { a, b }, chain.Branch(1));
        Assert.Equal(new[] { c, d }, chain.Branch(2));
        Assert.Empty(chain.ChangedKeys(new Dictionary<int, WiringPlacement?>
        {
            [a] = new(1, 1), [b] = new(1, 2), [c] = new(2, 1), [d] = new(2, 2),
        }));
    }

    [Fact]
    public void should_leave_unplaced_in_palette_when_two_branch()
    {
        var load = WiringChain.Load(WiringShape.TwoBranch, new[] { S(1, 1, 2, 1), S(2, 2), S(3, 3) });

        Assert.Equal(new[] { 1 }, load.Chain.Keys);
        Assert.Equal(new[] { 2, 3 }, load.Chain.Unplaced);
        Assert.False(load.HasSuggestion);
    }

    [Fact]
    public void should_join_left_branch_nearest_controller_when_dropped_at_controller_gap()
    {
        var chain = WiringChain.Create(WiringShape.TwoBranch, new[] { 2, 1, 3, 4 }, controllerGap: 2);

        var next = chain.Insert(9, 2);

        Assert.Equal(new[] { 2, 1, 9, 3, 4 }, next.Keys);
        Assert.Equal(3, next.ControllerGap);
        Assert.Equal(new WiringChainNumber(3, 1, 1, null), next.NumberOf(9));
        Assert.Equal(new WiringChainNumber(4, 2, 1, null), next.NumberOf(3));
        var appended = next.Append(5);
        Assert.Equal(5, appended.Keys[^1]);
        Assert.Equal(new WiringChainNumber(6, 2, 3, null), appended.NumberOf(5));   // 붙이기는 오른쪽 가지 바깥 끝
    }

    [Fact]
    public void should_sort_within_each_branch_and_keep_palette_when_two_branch_sorted_by_number()
    {
        var sensors = new[] { S(1, 40), S(2, 10), S(3, 30), S(4, 20), S(9, 5) };
        var chain = WiringChain.Create(WiringShape.TwoBranch, new[] { 1, 2, 3, 4 }, unplaced: new[] { 9 }, controllerGap: 2);
        // 왼쪽 가지(제어기 쪽부터) = [2, 1] · 오른쪽 = [3, 4]

        var sorted = chain.SortByDefaultOrder(sensors);

        Assert.Equal(new[] { 2, 1 }, sorted.Branch(1));                // 10, 40 — 제어기 쪽이 작은 번호
        Assert.Equal(new[] { 4, 3 }, sorted.Branch(2));                // 20, 30
        Assert.Equal(new[] { 9 }, sorted.Unplaced);                    // 가지에는 끌어오지 않는다
        Assert.Equal(2, sorted.ControllerGap);
    }

    [Fact]
    public void should_sort_whole_chain_and_pull_palette_when_ring_sorted_by_number()
    {
        var sensors = new[] { S(1, 40), S(2, 10), S(3, 30), S(9, 5) };
        var chain = WiringChain.Create(WiringShape.Ring, new[] { 1, 2, 3 }, unplaced: new[] { 9 });

        var sorted = chain.SortByDefaultOrder(sensors);

        Assert.Equal(new[] { 9, 2, 3, 1 }, sorted.Keys);
        Assert.Empty(sorted.Unplaced);
    }
    #endregion
}
