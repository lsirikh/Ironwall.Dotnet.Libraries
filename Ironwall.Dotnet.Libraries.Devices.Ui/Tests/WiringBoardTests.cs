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

    /// <summary>펜스 센서 · 제어기 종류 모름 → 링(v0.4 · 모든 제어기). 앞 <paramref name="placedOnFirst"/> 대는 저장된 체인 1번부터, 나머지는 번호순 제안.</summary>
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
    public void should_use_one_ring_list_when_fence_sensors_hang_on_an_unknown_controller()
    {
        // Arrange · Act — 옛 규칙은 양쪽 가지(두 목록)였다
        var board = Loaded(2, placedOnFirst: 2);

        // Assert
        Assert.Equal(WiringShape.Ring, board.Shape);
        Assert.Equal(1, board.LineCount);
        Assert.False(board.Place(board.Rows[0].Key, 2, 0));
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
    public void should_line_up_everything_on_one_chain_when_auto_laying_out_a_fence_controller()
    {
        // Arrange — 옛 규칙은 가지 안에서만 줄 세우고 팔레트는 그대로였다. 이제 PIDS 제어기도 링이라 전체가 한 줄이다.
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(1, 1104, new WiringPlacement(1, 1)), Seed(2, 1102, new WiringPlacement(1, 2)),
            Seed(3, 1103, new WiringPlacement(2, 1)), Seed(4, 1101, new WiringPlacement(2, 2)),
            Seed(5, 1100),
        }, "Controller");

        // Act
        board.AutoLayoutByNumber();

        // Assert
        Assert.Equal(new[] { 1100, 1101, 1102, 1103, 1104 }, Numbers(board.Placed(1)));
        Assert.Empty(board.Line(2));
        Assert.Empty(board.Unplaced);
    }
    #endregion

    #region - Load -
    [Fact]
    public void should_propose_the_legacy_conversion_without_making_the_board_dirty_until_applied()
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
        Assert.Equal(new[] { 1, 2, 4, 5 }, board.Placed(1).Select(r => r.Id));     // 보이는 것은 한 줄
        Assert.Equal(1, board.NumberOf(5)!.OppositeOrder);                         // 옛 2차 1번 = 새 B1
        // F-2b: 변환은 제안이다 — 적용 전에는 바뀐 줄이 아니고 저장에 실리지 않는다(저장될 자리 = 서버 원값).
        Assert.True(board.HasPendingProposals);
        Assert.Equal(2, board.ProposalCount(WiringProposalKind.Converted));
        Assert.False(board.IsDirty);
        Assert.Equal(new WiringPlacement(2, 1), board.PlacementOf(5));

        Assert.True(board.AcceptProposals());

        Assert.Equal(new[] { 4, 5 }, board.Diff().WiringChanged.Select(r => r.Id).OrderBy(i => i));
        Assert.Equal(new WiringPlacement(1, 4), board.PlacementOf(5));
    }

    [Fact]
    public void should_forget_the_legacy_notice_when_the_converted_chain_is_applied_and_saved()
    {
        var board = new WiringBoard();
        board.Load(new[] { Seed(1, 1101, new WiringPlacement(1, 1), "SmartSensor2"), Seed(2, 1102, new WiringPlacement(2, 1), "SmartSensor2") }, SMART);

        board.MarkBaseline();                                           // 적용 없이 저장(이름만 고친 저장) — 제안은 그대로
        Assert.True(board.ConvertedFromLegacy);
        Assert.True(board.HasPendingProposals);

        board.AcceptProposals();
        board.MarkBaseline();

        Assert.False(board.ConvertedFromLegacy);
        Assert.False(board.IsDirty);
        Assert.Equal(new WiringPlacement(1, 2), board.Rows[1].ServerPlacement);   // 다음 드리프트 기준도 보낸 값
    }

    [Fact]
    public void should_suggest_by_number_without_making_the_board_dirty_when_nothing_is_saved()
    {
        var board = Ring(3);

        Assert.Equal(3, board.SuggestedCount);
        Assert.False(board.IsDirty);                                    // 제안은 저장 대기가 아니다(O-3)
        Assert.Null(board.PlacementOf(board.Rows[0].Key));
        Assert.Equal(new WiringPlacement(1, 1), board.DisplayPlacementOf(board.Rows[0].Key));

        Assert.True(board.AcceptProposals());

        Assert.True(board.IsDirty);
        Assert.Equal(3, board.UnsavedChangeCount);
        Assert.False(board.AcceptProposals());
    }

    [Fact]
    public void should_apply_every_proposal_together_with_the_first_chain_edit()
    {
        var board = Ring(3, placed: 1);                                 // 1101 저장 · 1102 · 1103 제안
        var moved = board.Rows[2].Key;

        board.PushUndo();
        board.Place(moved, 1, 0);

        // H2: 적용하지 않은 제안 위에서 번호를 매기지 않는다 — 첫 편집이 제안을 전부 적용한다(같은 되돌리기 한 걸음).
        Assert.False(board.HasPendingProposals);
        Assert.True(board.ProposalsAppliedByEdit);
        Assert.Equal(new[] { 1103, 1101, 1102 }, Numbers(board.Placed(1)));
        Assert.Equal(3, board.Diff().WiringChanged.Count);

        Assert.True(board.Undo());
        Assert.True(board.HasPendingProposals);
        Assert.False(board.ProposalsAppliedByEdit);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_number_the_append_after_the_applied_suggestions_when_enter_appends_to_a_ring_with_proposals()
    {
        // 저장 a1 · b2 + 제안 c · d + 팔레트 새 줄 e — 편집 없이는 c · d 가 저장에 실리지 않는다.
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(1, 101, new WiringPlacement(1, 1), "SmartSensor2"), Seed(2, 102, new WiringPlacement(1, 2), "SmartSensor2"),
            Seed(3, 103, type: "SmartSensor2"), Seed(4, 104, type: "SmartSensor2"),
        }, SMART);
        var e = board.AddRow(Facts(105, type: "SmartSensor2"));

        Assert.DoesNotContain(board.Diff().ToSend, r => r.Id is 3 or 4);

        Assert.Equal(1, board.Append(new[] { e.Key }));

        Assert.Equal(new[] { 1, 2, 3, 4, 0 }, board.Placed(1).Select(r => r.Id));
        Assert.Equal(new WiringPlacement(1, 3), board.PlacementOf(3));
        Assert.Equal(new WiringPlacement(1, 4), board.PlacementOf(4));
        Assert.Equal(new WiringPlacement(1, 5), board.PlacementOf(e.Key));
        var send = board.Diff().ToSend;
        Assert.Contains(send, r => r.Id == 3);
        Assert.Contains(send, r => r.Id == 4);
        Assert.Contains(send, r => r.Key == e.Key);
    }

    [Fact]
    public void should_restore_the_load_issue_when_undoing_the_placement_that_cleared_it()
    {
        var board = new WiringBoard();
        board.Load(new[] { Seed(1, 1101, new WiringPlacement(1, 1)), Seed(2, 1102, new WiringPlacement(1, 1)) });
        var loser = board.Unplaced.Single();
        Assert.NotNull(loser.LoadIssue);

        board.PushUndo();
        board.Place(loser.Key, 1, 1);
        Assert.Null(loser.LoadIssue);

        board.Undo();

        Assert.NotNull(loser.LoadIssue);                                // L1 — 되돌리면 경고도 돌아온다
        Assert.Contains(loser, board.Unplaced);
    }

    [Fact]
    public void should_keep_the_raw_server_placement_for_drift_when_a_duplicate_claim_is_sent_to_the_palette()
    {
        var board = new WiringBoard();
        board.Load(new[] { Seed(1, 1101, new WiringPlacement(1, 1)), Seed(2, 1102, new WiringPlacement(1, 1)) });
        var loser = board.Unplaced.Single();

        Assert.Null(loser.BaselinePlacement);                           // 받아들인 기준 = 미배치(불러오기만으로 더러워지지 않게)
        Assert.Equal(new WiringPlacement(1, 1), loser.ServerPlacement); // 서버 원값은 그대로 — 드리프트 기준(H1)
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_suggest_by_number_when_fence_sensors_hang_on_an_unknown_controller()
    {
        // Arrange · Act — 옛 양쪽 가지는 팔레트에만 두었다. 링은 번호순으로 제안한다.
        var board = Loaded(3);

        // Assert
        Assert.Empty(board.Unplaced);
        Assert.Equal(3, board.SuggestedCount);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_propose_the_closed_up_gap_without_making_the_board_dirty_when_saved_orders_have_a_hole()
    {
        var board = new WiringBoard();
        board.Load(new[] { Seed(1, 1101, new WiringPlacement(1, 2), "SmartSensor2"), Seed(2, 1102, new WiringPlacement(1, 3), "SmartSensor2") }, SMART);

        // 체인에는 빈 자리가 없다 — 당겨 붙여 보이되 제안이다(적용 전 저장 대상 아님 · F-2b).
        Assert.Equal(new WiringPlacement(1, 1), board.DisplayPlacementOf(board.Rows[0].Key));
        Assert.Equal(new WiringPlacement(1, 2), board.PlacementOf(board.Rows[0].Key));
        Assert.False(board.IsDirty);
        Assert.Equal(2, board.ProposalCount(WiringProposalKind.Compacted));
        Assert.Single(board.LoadNotices);

        board.AcceptProposals();

        Assert.True(board.IsDirty);
        Assert.Equal(new WiringPlacement(1, 1), board.PlacementOf(board.Rows[0].Key));
    }

    [Fact]
    public void should_join_v2_branch_data_into_one_ring_when_the_controller_has_only_underground_sensors()
    {
        // Arrange — 옛 규칙은 저장된 "가지" 모양으로 열었다(H3). 이제 가지는 링으로 이어 붙이는 제안이다.
        var board = new WiringBoard();

        // Act
        board.Load(new[]
        {
            Seed(1, 501, new WiringPlacement(1, 1), "Underground"),
            Seed(2, 502, new WiringPlacement(2, 1), "Underground"),
        }, controllerType: null, savedShapes: new Dictionary<int, WiringShape> { [1] = WiringShape.TwoBranch, [2] = WiringShape.TwoBranch });

        // Assert
        Assert.Equal(WiringShape.Ring, board.Shape);
        Assert.Equal(WiringShape.TwoBranch, board.StoredShape);
        Assert.True(board.JoinedFromBranches);
        Assert.False(board.ConvertedFromLegacy);
        Assert.Null(board.ShapeNotice);
        Assert.Equal(new[] { 501, 502 }, Numbers(board.Placed(1)));
        Assert.Equal(1, board.Chain.ControllerGap);
        Assert.Equal(1, board.ProposalCount(WiringProposalKind.JoinedBranches));   // 501 은 (1,1) 그대로 — 502 만 자리가 바뀐다
    }

    [Fact]
    public void should_join_left_branch_reversed_then_right_branch_when_loading_v2_branch_data()
    {
        // Arrange — 왼쪽 가지 L1 · L2 · L3(제어기 → 바깥) + 오른쪽 가지 R1 · R2
        var board = new WiringBoard();
        var shapes = Enumerable.Range(1, 5).ToDictionary(id => id, _ => WiringShape.TwoBranch);

        // Act
        board.Load(new[]
        {
            Seed(1, 1101, new WiringPlacement(1, 1)), Seed(2, 1102, new WiringPlacement(1, 2)), Seed(3, 1103, new WiringPlacement(1, 3)),
            Seed(4, 1201, new WiringPlacement(2, 1)), Seed(5, 1202, new WiringPlacement(2, 2)),
        }, "Controller", shapes);

        // Assert — 펜스를 따라 왼쪽 바깥 L3 → L2 → L1 → (제어기) → R1 → R2
        Assert.Equal(WiringShape.Ring, board.Shape);
        Assert.Equal(new[] { 3, 2, 1, 4, 5 }, board.Placed(1).Select(r => r.Id));
        Assert.Equal(3, board.Chain.ControllerGap);                                 // 함체 틈 = 왼쪽 가지 수
        Assert.True(board.JoinedFromBranches);
        Assert.Equal("옛 가지 배치를 링으로 이어 붙였습니다 — 확인 후 저장", WiringBoard.JOINED_NOTICE);
        Assert.True(board.HasPendingProposals);
        Assert.Equal(4, board.ProposalCount(WiringProposalKind.JoinedBranches));    // L2 는 (1,2) 그대로라 제안이 아니다
        Assert.All(board.Proposals.Values, k => Assert.Equal(WiringProposalKind.JoinedBranches, k));
        Assert.False(board.IsDirty);
        Assert.Equal(new WiringPlacement(2, 1), board.PlacementOf(4));              // 적용 전 저장될 자리 = 받아들인 기준
        Assert.Equal(new WiringPlacement(1, 4), board.DisplayPlacementOf(4));
    }

    [Fact]
    public void should_save_the_joined_ring_places_when_the_joined_proposal_is_accepted()
    {
        // Arrange
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(1, 1101, new WiringPlacement(1, 1)), Seed(2, 1102, new WiringPlacement(1, 2)), Seed(3, 1103, new WiringPlacement(1, 3)),
            Seed(4, 1201, new WiringPlacement(2, 1)), Seed(5, 1202, new WiringPlacement(2, 2)),
        }, "Controller", Enumerable.Range(1, 5).ToDictionary(id => id, _ => WiringShape.TwoBranch));

        // Act
        Assert.True(board.AcceptProposals());

        // Assert
        Assert.Equal(new WiringPlacement(1, 1), board.PlacementOf(3));
        Assert.Equal(new WiringPlacement(1, 3), board.PlacementOf(1));
        Assert.Equal(new WiringPlacement(1, 5), board.PlacementOf(5));
        Assert.Equal(new[] { 1, 3, 4, 5 }, board.Diff().WiringChanged.Select(r => r.Id).OrderBy(i => i));

        board.MarkBaseline();
        Assert.False(board.JoinedFromBranches);                                     // 저장하면 알림을 걷는다
    }

    [Fact]
    public void should_propose_a_conversion_only_for_v_less_second_line_on_a_ring()
    {
        var board = new WiringBoard();
        board.Load(new[] { Seed(1, 1101, new WiringPlacement(1, 1), "SmartSensor2"), Seed(2, 1102, new WiringPlacement(2, 1), "SmartSensor2") }, SMART);

        Assert.True(board.ConvertedFromLegacy);
        Assert.Null(board.StoredShape);
        Assert.Null(board.ShapeNotice);
        Assert.Equal(1, board.ProposalCount(WiringProposalKind.Converted));
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

    #region - Spacing · mixed family (v0.4 §1-C) -
    [Fact]
    public void should_not_make_the_board_dirty_when_the_fence_spacing_changes()
    {
        // Arrange
        var board = Loaded(3, placedOnFirst: 3);
        var before = board.ChainLengthMetres;

        // Act
        var changed = board.SetFenceSpacing(2.0);

        // Assert — 그림만 다시 놓인다(O-10 · 서버에 싣지 않는다)
        Assert.True(changed);
        Assert.Equal(2.0, board.Spacing.FenceMetres);
        Assert.Equal(6.0, before);                                                  // 기본 3m × 2
        Assert.Equal(4.0, board.ChainLengthMetres);
        Assert.False(board.IsDirty);
        Assert.False(board.CanUndo);
        Assert.False(board.SetFenceSpacing(2.2));                                   // 0.5m 로 맞추면 같은 2m
    }

    [Fact]
    public void should_withdraw_number_suggestions_to_the_palette_when_a_mixed_controller_chain_is_edited()
    {
        // Arrange — 스마트 1 · 2 저장 + 스마트 3 · 펜스 101 저장 없음(번호순 제안)
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(1, 1, new WiringPlacement(1, 1), "SmartSensor2"), Seed(2, 2, new WiringPlacement(1, 2), "SmartSensor2"),
            Seed(3, 3, type: "SmartSensor2"), Seed(4, 101, type: "Fence"),
        }, SMART);
        Assert.True(board.IsMixedFamily);
        Assert.Equal(2, board.SuggestedCount);

        // Act
        Assert.True(board.MoveBy(board.Rows[0].Key, 1));

        // Assert — 섞인 제어기는 번호순이 실제 순서가 아닐 수 있어 제안을 저절로 적용하지 않는다(O-12)
        Assert.Equal(new[] { 2, 1 }, Numbers(board.Placed(1)));
        Assert.Equal(new[] { 3, 101 }, Numbers(board.Unplaced));
        Assert.False(board.HasPendingProposals);
        Assert.Equal(0, board.ProposalCount(WiringProposalKind.Suggested));
        Assert.DoesNotContain(board.Diff().WiringChanged, r => r.Id is 3 or 4);
    }

    [Fact]
    public void should_order_every_sensor_by_number_when_auto_laying_out_a_mixed_controller()
    {
        // Arrange
        var board = new WiringBoard();
        board.Load(new[]
        {
            Seed(4, 101, new WiringPlacement(1, 1), "Fence"), Seed(1, 1, new WiringPlacement(1, 2), "SmartSensor2"),
            Seed(3, 3, type: "SmartSensor2"), Seed(2, 2, type: "SmartSensor2"),
        }, SMART);

        // Act
        Assert.True(board.AutoLayoutByNumber());

        // Assert — 사람이 번호순을 고른 동작은 전부 줄 세운다
        Assert.Equal(new[] { 1, 2, 3, 101 }, Numbers(board.Placed(1)));
        Assert.Empty(board.Unplaced);
        Assert.False(board.HasPendingProposals);
    }

    [Fact]
    public void should_apply_number_suggestions_with_the_edit_when_the_controller_is_not_mixed()
    {
        // Arrange
        var board = Ring(4, placed: 2);

        // Act
        Assert.True(board.MoveBy(board.Rows[0].Key, 1));

        // Assert — 섞이지 않았으면 옛 규칙(H2) 그대로 함께 적용된다
        Assert.False(board.IsMixedFamily);
        Assert.Equal(new[] { 1102, 1101, 1103, 1104 }, Numbers(board.Placed(1)));
        Assert.Empty(board.Unplaced);
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
        Assert.True(board.Place(keys[1], 1, 0));                     // 제안 센서를 맨 앞으로 — 제안 적용 + 자리

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

        // 체인 끝(2번) 센서를 뺀다 — 앞 센서의 순번은 그대로라 이 한 줄만 바뀐다.
        board.Rows[1].Facts = board.Rows[1].Facts with { Name = "새 이름" };
        Assert.True(board.Unplace(keys[1]));

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
    [InlineData(999)]
    [InlineData(1000)]
    public void should_read_orders_up_to_the_raised_limit_when_spec_carries_a_long_chain(int order)
    {
        var spec = new JObject { ["wiring"] = new JObject { ["line"] = 1, ["order"] = order } };

        Assert.Equal(new WiringPlacement(1, order), WiringSpec.Read(spec));
        Assert.Null(WiringSpec.Validate(spec));
    }

    [Fact]
    public void should_refuse_an_order_of_1001_when_reading()
    {
        var spec = new JObject { ["wiring"] = new JObject { ["line"] = 1, ["order"] = 1001 } };

        Assert.Null(WiringSpec.Read(spec));
        Assert.Contains("범위", WiringSpec.Validate(spec));
    }

    [Fact]
    public void should_write_the_v2_marker_with_the_shape_and_read_it_back()
    {
        var spec = WiringSpec.Apply(JObject.Parse("""{"resolution":"4K"}"""), new WiringPlacement(1, 7), WiringShape.Ring);
        var patch = WiringSpec.MergePatch(new WiringPlacement(2, 3), WiringShape.TwoBranch);

        Assert.Equal(2, (int?)spec["wiring"]!["v"]);
        Assert.Equal("ring", (string?)spec["wiring"]!["shape"]);
        Assert.Equal(7, (int?)spec["wiring"]!["order"]);                  // 옛 클라이언트가 읽는 키는 그대로
        Assert.Equal(WiringShape.Ring, WiringSpec.ReadShape(spec));
        Assert.Equal(new WiringPlacement(1, 7), WiringSpec.Read(spec));
        Assert.Equal(WiringShape.TwoBranch, WiringSpec.ReadShape(patch));
        Assert.Null(WiringSpec.ReadShape(JObject.Parse("""{"wiring":{"line":2,"order":1}}""")));   // 표지 없는 옛 값
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
