using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 검증(wiring-fence-view FR-14 · 옛 device-wiring-setup FR-23 ~ FR-26) — 무엇이 저장을 막고 무엇이 알리기만 하는가.
/// 문장은 <b>무엇이 · 어디가 · 어떻게</b> 세 조각이어야 한다(WS L186-188).
/// </summary>
public class WiringValidationTests
{
    private const string SMART = "SmartController";

    /// <summary>센서 <paramref name="count"/> 대 — 저장된 결선 없이. 링이면 번호순 제안으로 체인에 붙는다.</summary>
    private static WiringBoard Board(int count, string? controllerType = SMART, string type = "SmartSensor2", int? channelOffset = null,
                                     int placed = 0)
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, count).Select(i => (
            Id: 100 + i,
            Channel: channelOffset is null ? (int?)null : (int?)(i + channelOffset.Value),
            Facts: new SensorFacts(1101 + i, $"북측 {i + 1}구간 센서", type, "북측 7구간"),
            Placement: i < placed ? new WiringPlacement(1, i + 1) : (WiringPlacement?)null,
            Issue: (string?)null,
            Groups: (IReadOnlyList<int>?)null)), controllerType);
        return board;
    }

    /// <summary>링 체인에 전부 붙이고 제안을 적용한 보드.</summary>
    private static WiringBoard Ring(int count, int? channelOffset = null)
    {
        var board = Board(count, channelOffset: channelOffset);
        board.AcceptSuggestions();
        return board;
    }

    #region - Evaluate -
    [Fact]
    public void should_report_nothing_when_a_ring_has_every_sensor_on_one_chain()
    {
        // 옛 모델은 2차 선이 비면 "루프가 닫히지 않습니다"(치명)였다 — 링의 돌아오는 길은 센서 없는 리턴케이블이라 이것이 정상이다.
        var board = Ring(4);

        var issues = WiringValidation.Evaluate(board);

        Assert.Empty(issues);
        Assert.False(WiringValidation.BlocksSave(issues));
    }

    [Fact]
    public void should_notify_but_not_block_when_sensors_are_still_unplaced()
    {
        var board = Ring(3);
        board.Unplace(board.Rows[2].Key);

        var issues = WiringValidation.Evaluate(board);

        var unplaced = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_UNPLACED));
        Assert.Equal(WiringIssueLevel.Info, unplaced.Level);           // FR-14 ① 알림
        Assert.Contains("1대", unplaced.Message);
        Assert.False(WiringValidation.BlocksSave(issues));
    }

    [Fact]
    public void should_warn_over_the_product_limit_when_a_smart_ring_has_more_than_34()
    {
        Assert.DoesNotContain(WiringValidation.Evaluate(Ring(34)), i => i.Code == WiringValidation.CODE_LIMIT);

        var issues = WiringValidation.Evaluate(Ring(35));

        var limit = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_LIMIT));
        Assert.Equal(WiringIssueLevel.Warning, limit.Level);
        Assert.Contains("34", limit.Message);
        Assert.False(WiringValidation.BlocksSave(issues));
    }

    [Fact]
    public void should_warn_when_smart_and_pids_sensors_share_a_controller()
    {
        var board = Ring(2);
        board.Rows[1].Facts = board.Rows[1].Facts with { TypeText = "Fence" };      // 표에서 종류를 바꾸면 바로 따라온다

        var issues = WiringValidation.Evaluate(board);

        var mix = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_MIX));
        Assert.Equal(WiringIssueLevel.Warning, mix.Level);
        Assert.False(WiringValidation.BlocksSave(issues));
    }

    [Fact]
    public void should_warn_about_the_closed_up_gap_when_saved_orders_had_a_hole()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 1, Channel: (int?)null, Facts: new SensorFacts(1101, "센서 1", "SmartSensor2", ""), Placement: (WiringPlacement?)new WiringPlacement(1, 1),
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)null),
            (Id: 2, Channel: (int?)null, Facts: new SensorFacts(1102, "센서 2", "SmartSensor2", ""), Placement: (WiringPlacement?)new WiringPlacement(1, 3),
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)null),
        }, SMART);

        var issues = WiringValidation.Evaluate(board);

        var gap = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_GAP));
        Assert.Equal(WiringIssueLevel.Warning, gap.Level);
        Assert.Contains("1차 2번 자리가 비어", gap.Message);
        Assert.Contains("고장 구간", gap.Message);                      // 어떻게 확인하는지가 문장에 있다
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
    public void should_block_when_two_sensors_claimed_the_same_saved_place()
    {
        var board = new WiringBoard();
        board.Load(new[]
        {
            (Id: 1, Channel: (int?)null, Facts: new SensorFacts(1101, "센서 1", "SmartSensor2", ""), Placement: (WiringPlacement?)new WiringPlacement(1, 1),
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)null),
            (Id: 2, Channel: (int?)null, Facts: new SensorFacts(1102, "센서 2", "SmartSensor2", ""), Placement: (WiringPlacement?)new WiringPlacement(1, 1),
             Issue: (string?)null, Groups: (IReadOnlyList<int>?)null),
        }, SMART);

        Assert.True(WiringValidation.BlocksSave(WiringValidation.Evaluate(board)));
    }

    [Fact]
    public void should_note_but_not_block_when_bus_address_differs_from_order()
    {
        var board = Ring(2, channelOffset: 7);      // 주소 7 · 8 인데 순번은 1 · 2

        var issues = WiringValidation.Evaluate(board);

        var note = Assert.Single(issues.Where(i => i.Code == WiringValidation.CODE_CHANNEL));
        Assert.Equal(WiringIssueLevel.Info, note.Level);
        Assert.Contains("주소", note.Message);
        Assert.False(WiringValidation.BlocksSave(issues));
    }
    #endregion

    #region - Words -
    [Fact]
    public void should_write_the_ring_from_sensor_a_to_sensor_b_when_ring()
    {
        var text = WiringValidation.LoopText(Ring(3));

        Assert.Contains("Sensor A ─▶ 1. 북측 1구간 센서 → 2. 북측 2구간 센서 → 3. 북측 3구간 센서 ◀─ Sensor B", text);
        Assert.Contains("리턴케이블", text);
    }

    [Fact]
    public void should_write_each_branch_when_two_branch()
    {
        var board = Board(3, controllerType: "Controller", type: "Fence");
        board.Place(board.Rows[0].Key, 1, 0);
        board.Place(board.Rows[1].Key, 2, 0);

        var text = WiringValidation.LoopText(board);

        Assert.Contains("제어기 ─왼쪽▶ 1. 북측 1구간 센서", text);
        Assert.Contains("제어기 ─오른쪽▶ 1. 북측 2구간 센서", text);
    }

    [Fact]
    public void should_say_empty_when_nothing_is_on_the_chain()
        => Assert.Contains("(비어 있음)", WiringValidation.LoopText(new WiringBoard()));

    [Fact]
    public void should_show_both_port_examples_when_a_ring_has_five_or_more()
    {
        var hint = WiringValidation.FaultHint(Ring(6));

        Assert.Contains("1차 4~5 → 북측 4구간 센서 와 북측 5구간 센서 사이", hint);
        Assert.Contains("2차 4~5 → 북측 3구간 센서 와 북측 2구간 센서 사이", hint);   // B 쪽에서: 2차 4 = 위치 3 · 2차 5 = 위치 2
    }

    [Fact]
    public void should_ask_for_more_sensors_when_the_chain_is_short()
        => Assert.Contains("5대 이상", WiringValidation.FaultHint(Ring(2)));
    #endregion

    #region - Fault section (A/B numbering) -
    [Fact]
    public void should_count_first_from_sensor_a_when_describing_a_ring_fault_section()
    {
        var board = Ring(6);

        Assert.Equal("1차 2~3 → 북측 2구간 센서 와 북측 3구간 센서 사이", WiringValidation.DescribeFaultSection(board, 1, 2, 3));
        Assert.Equal("1차 2번 = 북측 2구간 센서", WiringValidation.DescribeFaultSection(board, 1, 2, 2));
    }

    [Fact]
    public void should_count_second_from_sensor_b_when_describing_a_ring_fault_section()
    {
        var board = Ring(6);

        // 2차 n = 체인 위치 N+1−n — 같은 체인을 반대쪽 포트에서 센다.
        Assert.Equal("2차 1번 = 북측 6구간 센서", WiringValidation.DescribeFaultSection(board, 2, 1, 1));
        Assert.Equal("2차 2~3 → 북측 5구간 센서 와 북측 4구간 센서 사이", WiringValidation.DescribeFaultSection(board, 2, 2, 3));
        Assert.Equal("2차 6번 = 북측 1구간 센서", WiringValidation.DescribeFaultSection(board, 2, 6, 6));
    }

    [Fact]
    public void should_read_each_branch_from_the_controller_when_describing_a_two_branch_fault_section()
    {
        var board = Board(3, controllerType: "Controller", type: "Fence");
        board.Place(board.Rows[0].Key, 2, 0);
        board.Place(board.Rows[1].Key, 2, 1);

        Assert.Equal("2차 1~2 → 북측 1구간 센서 와 북측 2구간 센서 사이", WiringValidation.DescribeFaultSection(board, 2, 1, 2));
        Assert.Contains("없어", WiringValidation.DescribeFaultSection(board, 1, 1, 1));
    }

    [Fact]
    public void should_say_out_of_range_when_the_fault_section_is_beyond_the_wiring()
    {
        var board = Ring(3);

        Assert.Contains("밖입니다", WiringValidation.DescribeFaultSection(board, 1, 4, 5));
        Assert.Contains("밖입니다", WiringValidation.DescribeFaultSection(board, 2, 3, 4));
        Assert.Contains("없어", WiringValidation.DescribeFaultSection(new WiringBoard(), 1, 1, 2));
    }
    #endregion
}
