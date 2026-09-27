using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-09 (FR-11 · FR-20 · FR-30 · FR-31 · FR-41 · FR-45 · FR-52 · NFR-11) — 관계도 문구 · peer 문자열.
/// 한글 조사 병기는 커널 <c>KoreanParticles</c> 로 고른다. 시나리오: SIM-P036~P041 · SIM-L053(ISSUE-7) · SIM-A 계열(peer 문자열).
/// </summary>
public class UnitMapTextTests
{
    #region - 이름 · 표지 · 단계 -
    [Theory]
    [InlineData("제○○사단", "제○○사단")]
    [InlineData("2대대 7중대", "7중대")]
    [InlineData("  1연대  ", "1연대")]
    [InlineData("제1사단 2대대\t7중대", "7중대")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void should_use_last_word_when_short_name(string? name, string expected)
    {
        Assert.Equal(expected, UnitMapText.ShortName(name));
    }

    [Theory]
    [InlineData(EnumUnitEchelon.Division, "XX")]
    [InlineData(EnumUnitEchelon.Regiment, "|||")]
    [InlineData(EnumUnitEchelon.Battalion, "||")]
    [InlineData(EnumUnitEchelon.Company, "|")]
    [InlineData(EnumUnitEchelon.Outpost, "●●●")]
    [InlineData(null, "?")]
    public void should_map_echelon_to_app6_mark(EnumUnitEchelon? echelon, string expected)
    {
        Assert.Equal(expected, UnitMapText.EchelonMark(echelon));
    }

    [Theory]
    [InlineData(UnitMapLevel.L0, 0.10, "개요 · 10%")]
    [InlineData(UnitMapLevel.L1, 0.50, "부대 · 50%")]
    [InlineData(UnitMapLevel.L2, 1.60, "상세 · 160%")]
    [InlineData(UnitMapLevel.L1, 0.3999999999999999, "부대 · 40%")]
    [InlineData(UnitMapLevel.L0, 0.1728, "개요 · 17%")]
    public void should_format_hud_label_when_level_and_scale(UnitMapLevel level, double scale, string expected)
    {
        Assert.Equal(expected, UnitMapText.ZoomLabel(level, scale));
    }
    #endregion

    #region - peer · 툴팁 -
    [Fact]
    public void should_format_peer_name_with_echelon_and_code()
    {
        var fixture = UnitMapTestData.Standard200();

        Assert.Equal("7중대 (중대, c0207)", UnitMapText.PeerName(fixture.NodeOf("7중대")));
        Assert.Equal("제○○사단 (사단, d01)", UnitMapText.PeerName(fixture.NodeOf("제○○사단")));
    }

    [Fact]
    public void should_show_raw_echelon_in_peer_name_when_unknown()
    {
        var fixture = UnitMapTestData.UnknownEchelon();

        Assert.Equal("1여단 (Brigade, x01)", UnitMapText.PeerName(fixture.NodeOf("1여단")));
    }

    [Theory]
    [InlineData(UnitMapLevel.L2, true, 16, 0, false, "단계=L2; 옮김=예; 장비=16; 오류=0; 중지=아니오")]
    [InlineData(UnitMapLevel.L0, false, 0, 3, true, "단계=L0; 옮김=아니오; 장비=0; 오류=3; 중지=예")]
    [InlineData(UnitMapLevel.L1, false, 7, 1, false, "단계=L1; 옮김=아니오; 장비=7; 오류=1; 중지=아니오")]
    public void should_format_item_status_as_key_value_pairs(UnitMapLevel level, bool moved, int devices, int errors, bool suspended, string expected)
    {
        Assert.Equal(expected, UnitMapText.ItemStatus(level, moved, devices, errors, suspended));
    }

    [Fact]
    public void should_list_name_code_and_echelon_in_tooltip()
    {
        var fixture = UnitMapTestData.Standard200();

        Assert.Equal("7중대 · c0207 · 중대", UnitMapText.Tooltip(fixture.NodeOf("7중대")));
    }

    [Fact]
    public void should_mention_parent_outside_graph_in_tooltip_when_orphan()
    {
        var fixture = UnitMapTestData.OrphanParent();

        var tooltip = UnitMapText.Tooltip(fixture.NodeOf("9중대"));

        Assert.StartsWith("9중대 · c09 · 중대", tooltip);
        Assert.Contains("상위 편제 밖", tooltip);
    }
    #endregion

    #region - 배치 상태 문구 (FR-11 · ISSUE-7) -
    private static readonly DateTime At1402 = new(2026, 9, 27, 14, 2, 11);

    [Fact]
    public void should_name_last_editor_and_time_when_shared_and_editable()
    {
        Assert.Equal("배치: 모든 운영자 공유 · 마지막 변경 김○○ 09-27 14:02",
                     UnitMapText.LayoutStatus(UnitMapLayoutState.Shared, canEdit: true, "김○○", At1402));
    }

    [Fact]
    public void should_say_me_when_last_editor_is_this_operator()
    {
        Assert.Equal("배치: 모든 운영자 공유 · 마지막 변경 나 09-27 14:02",
                     UnitMapText.LayoutStatus(UnitMapLayoutState.Shared, canEdit: true, "김○○", At1402, updatedByMe: true));
    }

    [Fact]
    public void should_say_nobody_moved_yet_when_shared_document_is_empty()
    {
        // SIM-P003 — 빈 문서(version 0, updated_by null)
        Assert.Equal("배치: 모든 운영자 공유 · 아직 옮긴 부대가 없습니다",
                     UnitMapText.LayoutStatus(UnitMapLayoutState.Shared, canEdit: true));
    }

    [Fact]
    public void should_say_view_only_when_shared_without_edit_permission()
    {
        Assert.Equal("배치: 모든 운영자 공유 · 보기 전용",
                     UnitMapText.LayoutStatus(UnitMapLayoutState.Shared, canEdit: false, "김○○", At1402));
    }

    [Theory]
    [InlineData(UnitMapLayoutState.SessionOnly, "이 서버는 배치 저장을 지원하지 않습니다 — 옮긴 위치는 창을 닫으면 자동 배치로 돌아갑니다")]
    [InlineData(UnitMapLayoutState.ReadFailed, "배치를 불러오지 못했습니다 — 자동 배치로 보입니다")]
    [InlineData(UnitMapLayoutState.VersionMismatch, "배치 판이 달라 자동 배치로 보입니다")]
    [InlineData(UnitMapLayoutState.Loading, "배치를 불러오는 중입니다 — 자동 배치로 먼저 보입니다")]
    public void should_describe_layout_state_when_not_shared(UnitMapLayoutState state, string expected)
    {
        Assert.Equal(expected, UnitMapText.LayoutStatus(state, canEdit: true));
        Assert.Equal(expected, UnitMapText.LayoutStatus(state, canEdit: false));
    }
    #endregion

    #region - 끄는 동안 칩 (FR-30) -
    [Fact]
    public void should_say_move_under_target_when_reparent_chip()
    {
        var fixture = UnitMapTestData.Standard200();

        var chip = UnitMapText.DropChip(UnitMapDropDecision.Reparent(fixture.IdOf("3대대")), fixture.Tree);

        Assert.Equal("3대대 밑으로 옮기기", chip);
    }

    [Fact]
    public void should_pick_particle_by_final_consonant_when_adjoin_chip()
    {
        var standard = UnitMapTestData.Standard200();
        var roots = UnitMapTestData.MultiRoot();

        Assert.Equal("9중대와 인접 연결 · 양방향", UnitMapText.DropChip(UnitMapDropDecision.Adjoin(standard.IdOf("9중대")), standard.Tree));
        Assert.Equal("제2사단과 인접 연결 · 양방향", UnitMapText.DropChip(UnitMapDropDecision.Adjoin(roots.IdOf("제2사단")), roots.Tree));
    }

    [Fact]
    public void should_show_resolved_reason_when_blocked_chip()
    {
        var fixture = UnitMapTestData.Standard200();
        var reason = UnitDropRules.CanMove(fixture.Tree, fixture.IdOf("8중대"), fixture.IdOf("2대대")).Reason!;

        var chip = UnitMapText.DropChip(UnitMapDropDecision.Blocked(reason, fixture.IdOf("2대대")), fixture.Tree);

        Assert.Equal("'8중대'는 이미 '2대대' 소속입니다.", chip);
    }

    [Fact]
    public void should_have_no_chip_when_position()
    {
        Assert.Null(UnitMapText.DropChip(UnitMapDropDecision.Position(), UnitMapTestData.Mixed().Tree));
    }

    [Theory]
    [InlineData(5, "예하 5 함께")]
    [InlineData(1, "예하 1 함께")]
    [InlineData(0, null)]
    public void should_count_descendants_when_subtree_chip(int count, string? expected)
    {
        Assert.Equal(expected, UnitMapText.SubtreeChip(count));
    }
    #endregion

    #region - 되돌리기 막대 · 실패 · 충돌 (FR-31 · FR-34 · FR-35 · FR-52) -
    [Fact]
    public void should_say_visible_to_everyone_when_shared_position_moved()
    {
        Assert.Equal("‘6중대’ 위치를 옮겼습니다 — 모든 운영자에게 보입니다.", UnitMapText.PositionMovedBar("6중대", sessionOnly: false));
    }

    [Fact]
    public void should_say_disappears_on_close_when_session_only_position_moved()
    {
        var bar = UnitMapText.PositionMovedBar("6중대", sessionOnly: true);

        Assert.StartsWith("‘6중대’ 위치를 옮겼습니다", bar);
        Assert.Contains("이 서버는 배치를 저장하지 않습니다", bar);
        Assert.Contains("창을 닫으면 사라집니다", bar);
        Assert.DoesNotContain("모든 운영자", bar);
    }

    [Fact]
    public void should_resolve_particles_when_reparented_and_adjoined_bars()
    {
        Assert.Equal("‘8중대’를 ‘3대대’ 밑으로 옮겼습니다.", UnitMapText.ReparentedBar("8중대", "3대대"));
        Assert.Equal("‘1연대’를 ‘제1사단’ 밑으로 옮겼습니다.", UnitMapText.ReparentedBar("1연대", "제1사단"));
        Assert.Equal("‘6중대’와 ‘9중대’를 인접 부대로 이었습니다.", UnitMapText.AdjoinedBar("6중대", "9중대"));
        Assert.Equal("‘제1사단’과 ‘제2사단’을 인접 부대로 이었습니다.", UnitMapText.AdjoinedBar("제1사단", "제2사단"));
    }

    [Fact]
    public void should_say_reloaded_when_write_failed()
    {
        Assert.Equal("‘8중대’를 옮기지 못했습니다. 권한이 없습니다(403). 편제를 다시 읽었습니다.",
                     UnitMapText.WriteFailedBar("8중대", "권한이 없습니다(403)."));
        Assert.Equal("‘6중대’ 위치를 저장하지 못했습니다. 서버 응답이 없습니다. 서버 배치를 다시 불러왔습니다.",
                     UnitMapText.LayoutWriteFailedBar("6중대", "서버 응답이 없습니다."));
    }

    [Fact]
    public void should_never_claim_overwrite_when_conflict_or_undo_refused()
    {
        Assert.Equal("다른 운영자가 방금 ‘6중대’ 배치를 바꿨습니다 — 최신 배치를 불러왔습니다.", UnitMapText.LayoutConflictBar("6중대"));
        Assert.Equal("다른 운영자가 ‘6중대’ 배치를 바꿔 되돌리지 않았습니다.", UnitMapText.UndoRefusedBar("6중대"));
    }
    #endregion

    #region - 지도 회신 (FR-45) -
    [Theory]
    [InlineData("7중대", 11, 5, "7중대 장비 16 중 11 을 지도에 표시했습니다 · 5 는 지도에 없음")]
    [InlineData("7중대", 3, 2, "7중대 장비 5 중 3 을 지도에 표시했습니다 · 2 는 지도에 없음")]
    [InlineData("7중대", 2, 3, "7중대 장비 5 중 2 를 지도에 표시했습니다 · 3 은 지도에 없음")]
    [InlineData("7중대", 16, 0, "7중대 장비 16 을 지도에 표시했습니다")]
    [InlineData("7중대", 0, 4, "7중대 장비 4 중 지도에 있는 것이 없습니다")]
    public void should_report_shown_and_missing_when_map_locate_result(string title, int shown, int missing, string expected)
    {
        Assert.Equal(expected, UnitMapText.MapLocateResult(title, shown, missing));
    }
    #endregion

    #region - 확인 오버레이 (FR-32 · SB S6 · S7) -
    [Fact]
    public void should_describe_move_and_descendants_when_confirm_reparent()
    {
        var fixture = UnitMapTestData.Standard200();

        var confirm = UnitMapText.ConfirmReparent(fixture.Tree, fixture.IdOf("8중대"), fixture.IdOf("3대대"));

        Assert.Equal("상위 부대 바꾸기", confirm.Title);
        Assert.Equal(new[]
        {
            "‘8중대’(중대)를 ‘3대대’(대대) 밑으로 옮깁니다.",
            "예하 소초 4곳이 함께 옮겨집니다.",
            "서버에 바로 저장됩니다 · 되돌리기 1회 가능",
        }, confirm.Lines);
        Assert.Equal("옮기기", confirm.OkText);
    }

    [Fact]
    public void should_count_mixed_descendants_without_echelon_when_confirm_reparent()
    {
        var fixture = UnitMapTestData.Standard200();

        var confirm = UnitMapText.ConfirmReparent(fixture.Tree, fixture.IdOf("2대대"), fixture.IdOf("2연대"));

        Assert.Contains("예하 23곳이 함께 옮겨집니다.", confirm.Lines);   // 중대 4 + 소초 19(5~7중대 소초 5 · 8중대 소초 4) — 제대가 섞이면 제대를 적지 않는다
    }

    [Fact]
    public void should_omit_descendant_line_when_leaf_moves()
    {
        var fixture = UnitMapTestData.Standard200();

        var confirm = UnitMapText.ConfirmReparent(fixture.Tree, fixture.IdOf("81소초"), fixture.IdOf("7중대"));

        Assert.Equal(2, confirm.Lines.Count);
    }

    [Fact]
    public void should_describe_both_sides_when_confirm_adjoin()
    {
        var fixture = UnitMapTestData.Standard200();

        var confirm = UnitMapText.ConfirmAdjoin(fixture.Tree, fixture.IdOf("6중대"), fixture.IdOf("9중대"));

        Assert.Equal("인접 부대 연결", confirm.Title);
        Assert.Equal(new[]
        {
            "‘6중대’와 ‘9중대’를 인접 부대로 잇습니다.",
            "양쪽 상세에 함께 표시됩니다(무방향).",
            "보내기 직전 최신 인접 목록을 다시 읽습니다",
        }, confirm.Lines);
        Assert.Equal("연결", confirm.OkText);
    }
    #endregion
}
