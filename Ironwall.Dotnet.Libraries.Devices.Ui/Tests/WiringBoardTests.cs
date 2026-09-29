using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 보드(wiring-fence-view F-2 · FR-01 ~ FR-03 · FR-16) — 체인 끼워 넣기 · 옛 배치 변환 · 번호순 제안 · 되돌리기 · 변경 미리보기.
/// 드래그 제스처는 자동화로 단언할 수 없으므로(.NET 8 WPF 에 드래그 패턴이 없다) <b>판정만</b> 헤드리스로 못 박는다.
/// </summary>
/// <remarks>
/// 옛 칸 모델(N04: 1차 · 2차 선에 빈 칸, 자동 배치 = 앞 절반 1차 · 뒤 절반 2차)의 시험은 이 모델로 바뀌었다 —
/// 빈 칸 · ＋ 칸 · 칸 상한(64) · "찬 칸 거절" 이 없어졌고, 끼워 넣으면 뒤가 밀리고 빼면 당겨진다.
/// </remarks>
public class WiringBoardTests
{
    private const string SMART = "SmartController";

    private static SensorFacts Facts(int number, string? name = null, string type = "Fence", string zone = "북측 7구간")
        => new(number, name ?? $"북측 {number}구간 펜스", type, zone);

    private static (int Id, int? Channel, SensorFacts Facts, WiringPlacement? Placement, string? Issue, IReadOnlyList<int>? Groups)
        Seed(int id, int number, WiringPlacement? placement = null, string type = "Fence")
        => (id, (int?)null, Facts(number, type: type), placement, (string?)null, (IReadOnlyList<int>?)null);

    /// <summary>펜스 센서 · 제어기 종류 모름 → 양쪽 가지(잠정). 앞 <paramref name="placedOnFirst"/> 대는 왼쪽 가지 1번부터.</summary>
    private static WiringBoard Loaded(int count, int placedOnFirst = 0)
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, count).Select(i => Seed(100 + i, 1101 + i, i < placedOnFirst ? new WiringPlacement(1, i + 1) : null)));
        return board;
    }

    /// <summary>스마트 제어기 링 — 앞 <paramref name="placed"/> 대는 저장된 체인 위치, 나머지는 번호순 제안.</summary>
    private static WiringBoard Ring(int count, int placed = 0)
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, count).Select(i => Seed(100 + i, 1101 + i, i < placed ? new WiringPlacement(1, i + 1) : null, "SmartSensor2")), SMART);
        return board;
    }

    private static int[] Numbers(IEnumerable<WiringSensorRow> rows) => rows.Select(r => r.Facts.Number).ToArray();

    #region - Topology (FR-16) -
    [Fact]
    public void should_use_one_list_when_the_controller_is_a_smart_ring()
    {
        var board = Ring(3, placed: 3);

        Assert.Equal(WiringShape.Ring, board.Shape);
        Assert.Equal(1, board.LineCount);
        Assert.Empty(board.Line(2));
        Assert.False(board.Place(board.Rows[0].Key, 2, 0));          // 링에는 두 번째 목록이 없다
    }

    [Fact]
    public void should_use_two_lists_when_fence_sensors_hang_on_an_unknown_controller()
    {
        var board = Loaded(2);

        Assert.Equal(WiringShape.TwoBranch, board.Shape);
        Assert.Equal(2, board.LineCount);
    }
    #endregion

    #region - Chain edit -
    [Fact]
    public void should_insert_and_push_the_rest_when_placed_on_an_occupied_position()
    {
        var board = Ring(3, placed: 3);                              // [1101, 1102, 1103]
        var last = board.Rows[2].Key;

        Assert.True(board.Place(last, 1, 0));

        Assert.Equal(new[] { 1103, 1101, 1102 }, Numbers(board.Placed(1)));
        Assert.Equal(new WiringPlacement(1, 1), board.PlacementOf(last));
        Assert.Equal(new WiringPlacement(1, 3), board.PlacementOf(board.Rows[1].Key));
    }

    [Fact]
    public void should_close_up_the_rest_when_a_middle_sensor_is_unplaced()
    {
        var board = Ring(3, placed: 3);
        var keys = board.Rows.Select(r => r.Key).ToList();

        board.Unplace(keys[1]);

        // 체인에는 빈 칸이 없다 — 뒤 센서가 당겨지고, 그 센서도 "바뀐 줄"이 된다(저장하면 순번이 바뀐다).
        Assert.Null(board.PlacementOf(keys[1]));
        Assert.Equal(new WiringPlacement(1, 2), board.PlacementOf(keys[2]));
        Assert.Equal(new[] { keys[1], keys[2] }, board.Diff().WiringChanged.Select(r => r.Key).ToArray());
    }

    [Fact]
    public void should_report_no_change_when_placed_where_it_already_is()
    {
        var board = Ring(3, placed: 3);

        Assert.False(board.Place(board.Rows[1].Key, 1, 1));           // 자기 앞 틈
        Assert.False(board.Place(board.Rows[1].Key, 1, 2));           // 자기 뒤 틈
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_keep_one_sensor_once_when_placed_twice()
    {
        var board = Ring(2, placed: 1);
        var key = board.Rows[0].Key;

        board.Place(key, 1, 0);
        board.Place(key, 1, 5);

        Assert.Equal(1, board.Line(1).Count(k => k == key));
        Assert.Equal(new WiringPlacement(1, 2), board.PlacementOf(key));   // 범위 밖 자리 → 끝
    }

    [Fact]
    public void should_swap_with_the_neighbour_when_moved_by_one()
    {
        var board = Ring(3, placed: 3);
        var first = board.Rows[0].Key;

        Assert.True(board.MoveBy(first, 1));
        Assert.Equal(new[] { 1102, 1101, 1103 }, Numbers(board.Placed(1)));
        Assert.True(board.MoveBy(first, -1));
        Assert.False(board.MoveBy(first, -1));                        // 맨 앞
        Assert.False(board.MoveBy(board.Rows[2].Key, 1));             // 맨 끝
    }

    [Fact]
    public void should_move_to_the_same_position_of_the_other_branch_only_when_two_branch()
    {
        var board = Loaded(3, placedOnFirst: 2);
        var second = board.Rows[1].Key;

        Assert.True(board.MoveToOtherLine(second));
        Assert.Equal(new WiringPlacement(2, 1), board.PlacementOf(second));   // 오른쪽 가지가 비어 있어 1번

        var ring = Ring(2, placed: 2);
        Assert.False(ring.MoveToOtherLine(ring.Rows[0].Key));
    }

    [Fact]
    public void should_append_to_the_end_when_placed_from_the_palette_by_keyboard()
    {
        var board = Ring(3, placed: 2);
        board.AcceptSuggestions();
        board.Unplace(board.Rows[0].Key);

        Assert.Equal(1, board.Append(new[] { board.Rows[0].Key }));

        Assert.Equal(new[] { 1102, 1103, 1101 }, Numbers(board.Placed(1)));
    }
    #endregion

    #region - Auto layout (FR-03) -
    [Fact]
    public void should_line_up_by_number_then_id_on_one_chain_when_auto_laying_out_a_ring()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(30, 105, type: "SmartSensor2"), Seed(10, 105, type: "SmartSensor2"), Seed(20, 101, type: "SmartSensor2"),
            Seed(40, 110, new WiringPlacement(1, 1), "SmartSensor2"),
        }, SMART);

        Assert.True(board.AutoLayoutByNumber());

        Assert.Equal(new[] { 20, 10, 30, 40 }, board.Placed(1).Select(r => r.Id));   // 105 두 대는 id 순
        Assert.Empty(board.Line(2));                                                 // 옛 "앞 절반 1차 · 뒤 절반 2차" 가 아니다
        Assert.Empty(board.Unplaced);
        Assert.False(board.HasSuggestion);                                           // 사람이 누른 것 — 저장 대기다
        Assert.True(board.IsDirty);
    }

    [Fact]
    public void should_sort_within_each_branch_and_keep_the_palette_when_auto_laying_out_two_branches()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(1, 1104, new WiringPlacement(1, 1)), Seed(2, 1102, new WiringPlacement(1, 2)),
            Seed(3, 1103, new WiringPlacement(2, 1)), Seed(4, 1101, new WiringPlacement(2, 2)),
            Seed(5, 1100),
        }, "Controller");

        board.AutoLayoutByNumber();

        Assert.Equal(new[] { 1102, 1104 }, Numbers(board.Placed(1)));
        Assert.Equal(new[] { 1101, 1103 }, Numbers(board.Placed(2)));
        Assert.Equal(new[] { 1100 }, Numbers(board.Unplaced));
    }
    #endregion

    #region - Load -
    [Fact]
    public void should_convert_legacy_second_line_reversed_and_count_it_as_changed_when_loading_a_ring()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(1, 1101, new WiringPlacement(1, 1), "SmartSensor2"),
            Seed(2, 1102, new WiringPlacement(1, 2), "SmartSensor2"),
            Seed(5, 1105, new WiringPlacement(2, 1), "SmartSensor2"),     // 2차 1번 = Sensor B 바로 옆
            Seed(4, 1104, new WiringPlacement(2, 2), "SmartSensor2"),
        }, SMART);

        Assert.True(board.ConvertedFromLegacy);
        Assert.Equal(new[] { 1, 2, 4, 5 }, board.Placed(1).Select(r => r.Id));
        Assert.Equal(1, board.NumberOf(5)!.OppositeOrder);                 // 옛 2차 1번 = 새 B1
        // 자동 저장하지 않는다 — 옮겨진 두 대가 "바뀐 줄"이 되어 [저장하기]가 써 준다.
        Assert.Equal(new[] { 4, 5 }, board.Diff().WiringChanged.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public void should_forget_the_legacy_notice_when_the_converted_chain_is_saved()
    {
        var board = new WiringBoard();
        board.Load(new[] { Seed(1, 1101, new WiringPlacement(1, 1), "SmartSensor2"), Seed(2, 1102, new WiringPlacement(2, 1), "SmartSensor2") }, SMART);

        board.MarkBaseline();

        Assert.False(board.ConvertedFromLegacy);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_suggest_by_number_without_making_the_board_dirty_when_nothing_is_saved()
    {
        var board = Ring(3);

        Assert.Equal(3, board.SuggestedCount);
        Assert.False(board.IsDirty);                                    // 제안은 저장 대기가 아니다(O-3)
        Assert.Null(board.PlacementOf(board.Rows[0].Key));
        Assert.Equal(new WiringPlacement(1, 1), board.DisplayPlacementOf(board.Rows[0].Key));

        Assert.True(board.AcceptSuggestions());

        Assert.True(board.IsDirty);
        Assert.Equal(3, board.UnsavedChangeCount);
        Assert.False(board.AcceptSuggestions());
    }

    [Fact]
    public void should_accept_only_the_moved_sensor_when_a_suggested_sensor_is_placed_by_hand()
    {
        var board = Ring(3, placed: 1);                                 // 1101 저장 · 1102 · 1103 제안
        var moved = board.Rows[2].Key;

        board.Place(moved, 1, 0);

        Assert.False(board.IsSuggested(moved));
        Assert.True(board.IsSuggested(board.Rows[1].Key));
        Assert.Equal(new[] { board.Rows[0].Key, moved }, board.Diff().WiringChanged.Select(r => r.Key).ToArray());   // 제안 1102 는 세지 않는다
    }

    [Fact]
    public void should_leave_unplaced_in_the_palette_without_suggestion_when_two_branch()
    {
        var board = Loaded(3);

        Assert.Equal(3, board.Unplaced.Count);
        Assert.False(board.HasSuggestion);
    }

    [Fact]
    public void should_close_up_and_count_as_changed_when_saved_orders_have_a_hole()
    {
        var board = new WiringBoard();
        board.Load(new[] { Seed(1, 1101, new WiringPlacement(1, 2), "SmartSensor2"), Seed(2, 1102, new WiringPlacement(1, 3), "SmartSensor2") }, SMART);

        // 체인에는 빈 자리가 없다 — 당겨 붙이고 경고한다. 옛 칸 모델은 빈 자리를 두고 깨끗이 열었다(C5).
        Assert.Equal(new WiringPlacement(1, 1), board.PlacementOf(board.Rows[0].Key));
        Assert.True(board.IsDirty);
        Assert.Single(board.LoadNotices);
    }

    [Fact]
    public void should_place_from_saved_orders_when_loading_two_branches()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(1, 1101, new WiringPlacement(1, 2)),
            Seed(2, 1102, new WiringPlacement(1, 1)),
            Seed(3, 1103, new WiringPlacement(2, 1)),
        });

        Assert.Equal(new[] { 1102, 1101 }, Numbers(board.Placed(1)));
        Assert.Equal(new WiringPlacement(1, 1), board.PlacementOf(board.Rows[1].Key));
        Assert.Equal(new[] { 1103 }, Numbers(board.Placed(2)));
        Assert.Empty(board.Unplaced);
        Assert.False(board.IsDirty);          // 불러오기만으로 "바뀐 줄"이 생기지 않는다
        Assert.False(board.ConvertedFromLegacy);
    }

    [Fact]
    public void should_leave_the_second_claim_unplaced_when_two_sensors_claim_one_order()
    {
        var board = new WiringBoard();
        board.Load(new[] { Seed(1, 1101, new WiringPlacement(1, 1)), Seed(2, 1102, new WiringPlacement(1, 1)) });

        Assert.Single(board.Placed(1));
        Assert.Single(board.Unplaced);
        Assert.NotNull(board.Unplaced[0].LoadIssue);
    }
    #endregion

    #region - Undo · diff -
    [Fact]
    public void should_restore_both_table_and_wiring_when_undoing_once()
    {
        var board = Loaded(2, placedOnFirst: 1);
        var keys = board.Rows.Select(r => r.Key).ToList();

        board.PushUndo();
        board.Rows[0].Facts = board.Rows[0].Facts with { Name = "고친 이름" };
        board.Place(keys[1], 2, 0);

        Assert.True(board.Undo());

        Assert.Equal("북측 1101구간 펜스", board.Rows[0].Facts.Name);
        Assert.Null(board.PlacementOf(keys[1]));
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void should_report_nothing_to_undo_when_nothing_was_pushed()
        => Assert.False(new WiringBoard().Undo());

    [Fact]
    public void should_count_a_row_once_when_both_value_and_wiring_changed()
    {
        var board = Loaded(2, placedOnFirst: 2);
        var keys = board.Rows.Select(r => r.Key).ToList();

        // 왼쪽 가지 끝(2번) 센서를 오른쪽 가지로 — 앞 센서의 순번은 그대로라 이 한 줄만 바뀐다.
        board.Rows[1].Facts = board.Rows[1].Facts with { Name = "새 이름" };
        board.Place(keys[1], 2, 0);

        var diff = board.Diff();

        Assert.Single(diff.FactChanged);
        Assert.Contains(diff.WiringChanged, r => r.Key == keys[1]);
        Assert.Single(diff.ToSend);                  // 한 줄이 둘 다 바뀌어도 호출은 한 번
    }

    [Fact]
    public void should_treat_added_rows_as_creates_when_diffing()
    {
        var board = Loaded(1, placedOnFirst: 1);
        board.AddRow(Facts(1201));

        var diff = board.Diff();

        Assert.Single(diff.Created);
        Assert.True(diff.Created[0].IsNew);
        Assert.Empty(diff.FactChanged);
    }

    [Fact]
    public void should_stop_counting_changes_when_marked_as_the_new_baseline()
    {
        var board = Loaded(2, placedOnFirst: 2);
        board.Rows[0].Facts = board.Rows[0].Facts with { Name = "새 이름" };
        Assert.True(board.IsDirty);

        board.MarkBaseline();

        Assert.False(board.IsDirty);
        Assert.Equal(0, board.UnsavedChangeCount);
    }

    [Fact]
    public void should_keep_the_key_when_a_created_row_gets_a_server_id()
    {
        var board = new WiringBoard();
        var row = board.AddRow(Facts(1201));
        board.Place(row.Key, 1, 0);

        var promoted = board.Promote(row.Key, 555);

        Assert.NotNull(promoted);
        Assert.Equal(row.Key, promoted!.Key);
        Assert.Equal(555, promoted.Id);
        Assert.False(promoted.IsNew);
        Assert.Equal(new WiringPlacement(1, 1), promoted.BaselinePlacement);
    }
    #endregion

    #region - Spec round trip -
    [Fact]
    public void should_read_line_and_order_when_spec_carries_wiring()
    {
        var spec = JObject.Parse("""{"resolution":"4K","wiring":{"line":2,"order":3}}""");

        var placement = WiringSpec.Read(spec);

        Assert.Equal(new WiringPlacement(2, 3), placement);
        Assert.Null(WiringSpec.Validate(spec));
    }

    [Fact]
    public void should_keep_other_spec_keys_when_writing_wiring()
    {
        var spec = JObject.Parse("""{"resolution":"4K"}""");

        var next = WiringSpec.Apply(spec, new WiringPlacement(1, 4));

        Assert.Equal("4K", (string?)next["resolution"]);
        Assert.Equal(1, (int?)next["wiring"]!["line"]);
        Assert.Equal(4, (int?)next["wiring"]!["order"]);
        Assert.Null(spec["wiring"]);                     // 원본은 건드리지 않는다(재조회 비교의 기준이다)
    }

    [Fact]
    public void should_remove_only_the_wiring_key_when_unplacing()
    {
        var spec = JObject.Parse("""{"resolution":"4K","wiring":{"line":1,"order":1}}""");

        var next = WiringSpec.Apply(spec, null);

        Assert.Null(next["wiring"]);
        Assert.Equal("4K", (string?)next["resolution"]);
    }

    [Theory]
    [InlineData(65)]      // 옛 칸 상한(64)을 넘어도 읽는다 — 펜스센서는 제어기 한 대에 수백 대다
    [InlineData(400)]
    [InlineData(WiringSpec.MAX_ORDER)]
    public void should_read_orders_up_to_the_raised_limit_when_spec_carries_a_long_chain(int order)
    {
        var spec = new JObject { ["wiring"] = new JObject { ["line"] = 1, ["order"] = order } };

        Assert.Equal(new WiringPlacement(1, order), WiringSpec.Read(spec));
        Assert.Null(WiringSpec.Validate(spec));
    }

    [Fact]
    public void should_refuse_an_order_beyond_the_limit_when_reading()
    {
        var spec = new JObject { ["wiring"] = new JObject { ["line"] = 1, ["order"] = WiringSpec.MAX_ORDER + 1 } };

        Assert.Equal(1000, WiringSpec.MAX_ORDER);
        Assert.Null(WiringSpec.Read(spec));
        Assert.Contains("범위", WiringSpec.Validate(spec));
    }

    [Theory]
    [InlineData("""{"wiring":{"line":3,"order":1}}""")]
    [InlineData("""{"wiring":{"line":1,"order":0}}""")]
    [InlineData("""{"wiring":{"line":1}}""")]
    [InlineData("""{"wiring":"1-4"}""")]
    public void should_report_a_reason_when_saved_wiring_cannot_be_read(string json)
    {
        var spec = JObject.Parse(json);

        Assert.Null(WiringSpec.Read(spec));
        Assert.NotNull(WiringSpec.Validate(spec));
    }

    [Fact]
    public void should_say_nothing_when_there_is_no_saved_wiring()
    {
        Assert.Null(WiringSpec.Validate(null));
        Assert.Null(WiringSpec.Validate(JObject.Parse("""{"resolution":"4K"}""")));
    }
    #endregion
}
