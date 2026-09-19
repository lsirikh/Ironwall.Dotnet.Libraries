using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Newtonsoft.Json.Linq;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 보드(device-wiring-setup FR-13 ~ FR-21) — 순번 · 당김 · 자동 배치 · 되돌리기 · 변경 미리보기.
/// 드래그 제스처는 자동화로 단언할 수 없으므로(.NET 8 WPF 에 드래그 패턴이 없다) <b>판정만</b> 헤드리스로 못 박는다.
/// </summary>
public class WiringBoardTests
{
    private static SensorFacts Facts(int number, string? name = null, string type = "Fence", string zone = "북측 7구간")
        => new(number, name ?? $"북측 {number}구간 펜스", type, zone);

    private static WiringBoard Loaded(int count, int placedOnFirst = 0)
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, count).Select(i => (
            Id: 100 + i,
            Channel: (int?)(i + 1),
            Facts: Facts(1101 + i),
            Placement: i < placedOnFirst ? new WiringPlacement(1, i + 1) : null,
            Issue: (string?)null)));
        return board;
    }

    #region - Placement -
    [Fact]
    public void should_number_by_filled_slots_when_slots_have_gaps()
    {
        var board = Loaded(3);
        var keys = board.Rows.Select(r => r.Key).ToList();

        board.Place(keys[0], 1, 0);
        board.Place(keys[1], 1, 3);      // 가운데 두 칸을 비워 둔다

        Assert.Equal(1, board.OrderAt(1, 0));
        Assert.Equal(0, board.OrderAt(1, 1));            // 빈 칸은 순번이 없다
        Assert.Equal(2, board.OrderAt(1, 3));            // 칸은 4번째인데 순번은 2다
        Assert.Equal(new WiringPlacement(1, 2), board.PlacementOf(keys[1]));
    }

    [Fact]
    public void should_pull_later_orders_up_when_a_middle_sensor_is_unplaced()
    {
        var board = Loaded(3, placedOnFirst: 3);
        var keys = board.Rows.Select(r => r.Key).ToList();

        Assert.Equal(3, board.PlacementOf(keys[2])!.Order);

        board.Unplace(keys[1]);

        Assert.Equal(1, board.PlacementOf(keys[0])!.Order);
        Assert.Null(board.PlacementOf(keys[1]));
        Assert.Equal(2, board.PlacementOf(keys[2])!.Order);       // 뒤 순번이 당겨졌다
    }

    [Fact]
    public void should_refuse_to_place_when_the_slot_is_already_taken()
    {
        var board = Loaded(2, placedOnFirst: 2);
        var keys = board.Rows.Select(r => r.Key).ToList();

        Assert.False(board.Place(keys[1], 1, 0));
        Assert.Equal(keys[0], board.RowAt(1, 0)!.Key);            // 앞 센서가 그대로다
        Assert.Equal(2, board.PlacementOf(keys[1])!.Order);
    }

    [Fact]
    public void should_move_between_lines_when_placed_on_the_other_line()
    {
        var board = Loaded(2, placedOnFirst: 2);
        var keys = board.Rows.Select(r => r.Key).ToList();

        Assert.True(board.Place(keys[1], 2, 0));

        Assert.Single(board.Placed(1));
        Assert.Single(board.Placed(2));
        Assert.Equal(new WiringPlacement(2, 1), board.PlacementOf(keys[1]));
    }

    [Fact]
    public void should_keep_one_sensor_in_one_slot_when_placed_twice()
    {
        var board = Loaded(1);
        var key = board.Rows[0].Key;

        board.Place(key, 1, 0);
        board.Place(key, 1, 4);

        Assert.Equal(1, board.Line(1).Count(k => k == key));
        Assert.Equal(new WiringPlacement(1, 1), board.PlacementOf(key));
    }

    [Fact]
    public void should_grow_slots_when_asked_and_stop_at_the_cap()
    {
        var board = new WiringBoard();
        var before = board.SlotCount(1);

        Assert.True(board.AddSlot(1));
        Assert.Equal(before + 1, board.SlotCount(1));

        while (board.SlotCount(1) < WiringBoard.MAX_SLOTS) board.AddSlot(1);
        Assert.False(board.AddSlot(1));
        Assert.Equal(WiringBoard.MAX_SLOTS, board.SlotCount(1));
    }
    #endregion

    #region - Auto layout -
    [Fact]
    public void should_split_by_number_into_two_lines_when_auto_laying_out()
    {
        var board = Loaded(7);

        board.AutoLayoutByNumber();

        Assert.Equal(4, board.Placed(1).Count);               // 앞 절반(올림)
        Assert.Equal(3, board.Placed(2).Count);
        Assert.Equal(new[] { 1101, 1102, 1103, 1104 }, board.Placed(1).Select(r => r.Facts.Number));
        Assert.Equal(new[] { 1105, 1106, 1107 }, board.Placed(2).Select(r => r.Facts.Number));
        Assert.Empty(board.Unplaced);
    }

    [Fact]
    public void should_keep_a_minimum_of_slots_when_auto_laying_out_few_sensors()
    {
        var board = Loaded(2);

        board.AutoLayoutByNumber();

        Assert.True(board.SlotCount(1) >= WiringBoard.MIN_SLOTS_AFTER_AUTO);
        Assert.True(board.SlotCount(2) >= WiringBoard.MIN_SLOTS_AFTER_AUTO);
    }
    #endregion

    #region - Load -
    [Fact]
    public void should_place_from_saved_orders_when_loading()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 1, Channel: (int?)7, Facts: Facts(1101), Placement: (WiringPlacement?)new WiringPlacement(1, 2), Issue: (string?)null),
            (Id: 2, Channel: (int?)8, Facts: Facts(1102), Placement: (WiringPlacement?)new WiringPlacement(1, 1), Issue: (string?)null),
            (Id: 3, Channel: (int?)9, Facts: Facts(1103), Placement: (WiringPlacement?)new WiringPlacement(2, 1), Issue: (string?)null),
        });

        Assert.Equal(new[] { 1102, 1101 }, board.Placed(1).Select(r => r.Facts.Number));
        Assert.Equal(new[] { 1103 }, board.Placed(2).Select(r => r.Facts.Number));
        Assert.Empty(board.Unplaced);
        Assert.False(board.IsDirty);          // 불러오기만으로 "바뀐 줄"이 생기지 않는다
    }

    [Fact]
    public void should_leave_the_second_claim_unplaced_when_two_sensors_claim_one_order()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 1, Channel: (int?)1, Facts: Facts(1101), Placement: (WiringPlacement?)new WiringPlacement(1, 1), Issue: (string?)null),
            (Id: 2, Channel: (int?)2, Facts: Facts(1102), Placement: (WiringPlacement?)new WiringPlacement(1, 1), Issue: (string?)null),
        });

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

        board.Rows[0].Facts = board.Rows[0].Facts with { Name = "새 이름" };
        board.Place(keys[0], 2, 0);

        var diff = board.Diff();

        Assert.Single(diff.FactChanged);
        Assert.Contains(diff.WiringChanged, r => r.Key == keys[0]);
        Assert.Equal(2, diff.ToSend.Count);          // 1101 한 줄 + 순번이 당겨진 1102
        Assert.Equal(1, diff.ToSend.Count(r => r.Key == keys[0]));
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
