using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 루프 검증(device-wiring-setup FR-23 ~ FR-26) — 무엇이 저장을 막고 무엇이 알리기만 하는가.
/// 문장은 <b>무엇이 · 어디가 · 어떻게</b> 세 조각이어야 한다(WS L186-188).
/// </summary>
public class WiringValidationTests
{
    private static WiringBoard Board(int count, int? channelOffset = null)
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, count).Select(i => (
            Id: 100 + i,
            Channel: channelOffset is null ? (int?)null : (int?)(i + channelOffset.Value),
            Facts: new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", "Fence", "북측 7구간"),
            Placement: (WiringPlacement?)null,
            Issue: (string?)null,
            Groups: (IReadOnlyList<int>?)null)));
        return board;
    }

    private static void PlaceAll(WiringBoard board, int onFirst)
    {
        var keys = board.Rows.Select(r => r.Key).ToList();
        for (var i = 0; i < keys.Count; i++)
        {
            var line = i < onFirst ? 1 : 2;
            var index = i < onFirst ? i : i - onFirst;
            while (board.SlotCount(line) <= index) board.AddSlot(line);
            board.Place(keys[i], line, index);
        }
    }

    [Fact]
    public void should_report_nothing_when_every_sensor_is_on_a_line_and_the_loop_closes()
    {
        var board = Board(4);
        PlaceAll(board, onFirst: 2);

        var issues = WiringValidation.Evaluate(board);

        Assert.Empty(issues);
        Assert.False(WiringValidation.BlocksSave(issues));
    }

    [Fact]
    public void should_warn_when_sensors_are_still_unplaced()
    {
        var board = Board(3);
        PlaceAll(board, onFirst: 1);
        board.Unplace(board.Rows[2].Key);

        var issues = WiringValidation.Evaluate(board);

        var unplaced = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_UNPLACED));
        Assert.Equal(WiringIssueLevel.Warning, unplaced.Level);
        Assert.Contains("1대", unplaced.Message);
        Assert.False(WiringValidation.BlocksSave(issues));      // 막지는 않는다 — 표 값은 저장할 수 있어야 한다
    }

    [Fact]
    public void should_block_when_the_second_line_is_empty_but_the_first_is_not()
    {
        var board = Board(2);
        PlaceAll(board, onFirst: 2);

        var issues = WiringValidation.Evaluate(board);

        var open = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_LOOP_OPEN));
        Assert.Equal(WiringIssueLevel.Critical, open.Level);
        Assert.Contains("루프가 닫히지 않습니다", open.Message);
        Assert.True(WiringValidation.BlocksSave(issues));
    }

    [Fact]
    public void should_warn_with_the_slot_number_when_a_line_has_a_hole_in_the_middle()
    {
        var board = Board(4);
        PlaceAll(board, onFirst: 3);
        board.Unplace(board.Rows[1].Key);          // 1차 2번 칸을 비운다

        var issues = WiringValidation.Evaluate(board);

        var gap = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_GAP));
        Assert.Equal(WiringIssueLevel.Warning, gap.Level);
        Assert.Contains("1차 선 2번 자리가 비어 있어요", gap.Message);
        Assert.Contains("끌어다 놓거나", gap.Message);           // 어떻게 고치는지가 문장에 있다
    }

    [Fact]
    public void should_find_no_gap_when_empty_slots_are_only_at_the_tail()
    {
        var board = Board(2);
        PlaceAll(board, onFirst: 1);

        Assert.Null(WiringValidation.FirstGap(board, 1));
        Assert.Null(WiringValidation.FirstGap(board, 2));
    }

    [Fact]
    public void should_block_when_a_saved_wiring_value_could_not_be_read()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 1, Channel: (int?)1, Facts: new SensorFacts(1101, "센서", "Fence", ""),
             Placement: (WiringPlacement?)null, Issue: (string?)"순번 0 이 쓸 수 있는 범위 밖입니다", Groups: (IReadOnlyList<int>?)null),
        });

        var issues = WiringValidation.Evaluate(board);

        Assert.Contains(issues, i => i.Code == WiringValidation.CODE_LOAD && i.Level == WiringIssueLevel.Critical);
        Assert.True(WiringValidation.BlocksSave(issues));
    }

    [Fact]
    public void should_note_but_not_block_when_bus_address_differs_from_order()
    {
        var board = Board(2, channelOffset: 7);      // 주소 7 · 8 인데 순번은 1 · 1
        PlaceAll(board, onFirst: 1);

        var issues = WiringValidation.Evaluate(board);

        var note = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_CHANNEL));
        Assert.Equal(WiringIssueLevel.Info, note.Level);
        Assert.Contains("주소", note.Message);
        Assert.False(WiringValidation.BlocksSave(issues));
    }

    [Fact]
    public void should_write_the_loop_in_words_when_both_lines_have_sensors()
    {
        var board = Board(3);
        PlaceAll(board, onFirst: 2);

        var text = WiringValidation.LoopText(board);

        Assert.Contains("제어기 ─1차▶ 1. 북측 1구간 펜스 → 2. 북측 2구간 펜스", text);
        Assert.Contains("제어기 ◀2차─ 1. 북측 3구간 펜스", text);
        Assert.Contains("⟲", text);
    }

    [Fact]
    public void should_say_empty_when_a_line_has_no_sensor_in_the_loop_text()
    {
        var text = WiringValidation.LoopText(new WiringBoard());

        Assert.Contains("(비어 있음)", text);
    }

    [Fact]
    public void should_show_the_fault_example_when_the_first_line_has_five_or_more()
    {
        var board = Board(6);
        PlaceAll(board, onFirst: 5);

        var hint = WiringValidation.FaultHint(board);

        Assert.Contains("1차 4~5", hint);
        Assert.Contains("북측 4구간 펜스", hint);
        Assert.Contains("북측 5구간 펜스", hint);
    }

    [Fact]
    public void should_ask_for_more_sensors_when_the_first_line_is_short()
    {
        var board = Board(2);
        PlaceAll(board, onFirst: 2);

        Assert.Contains("5대 이상", WiringValidation.FaultHint(board));
    }

    [Fact]
    public void should_name_both_sensors_when_describing_a_fault_section()
    {
        var board = Board(6);
        PlaceAll(board, onFirst: 5);

        Assert.Equal("1차 2~3 → 북측 2구간 펜스 와 북측 3구간 펜스 사이", WiringValidation.DescribeFaultSection(board, 1, 2, 3));
        Assert.Equal("1차 2번 = 북측 2구간 펜스", WiringValidation.DescribeFaultSection(board, 1, 2, 2));
    }

    [Fact]
    public void should_say_out_of_range_when_the_fault_section_is_beyond_the_wiring()
    {
        var board = Board(3);
        PlaceAll(board, onFirst: 2);

        Assert.Contains("밖입니다", WiringValidation.DescribeFaultSection(board, 1, 4, 5));
        Assert.Contains("없어", WiringValidation.DescribeFaultSection(new WiringBoard(), 1, 1, 2));
    }
}
