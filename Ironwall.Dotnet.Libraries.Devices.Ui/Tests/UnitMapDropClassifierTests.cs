using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-11 (FR-05 · FR-29 · NFR-11) — 드롭의 뜻: 위치 · 상위 바꾸기 · 인접 연결 · 막힘.
/// 판정은 <see cref="UnitDropRules.CanMove"/> · <see cref="UnitDropRules.CanAdjoin"/> 를 부르기만 한다(규칙 사본 금지).
/// 시나리오: SIM-D001~D552(ISSUE-14 자기 위 · ISSUE-15 현 상위 · ISSUE-16 운용 중지 · ISSUE-17 배치 모드).
/// </summary>
public class UnitMapDropClassifierTests
{
    private static readonly UnitMapDropPolicy EditShared = new(CanView: true, CanEdit: true, UnitMapLayoutState.Shared);
    private static readonly UnitMapDropPolicy ViewShared = new(CanView: true, CanEdit: false, UnitMapLayoutState.Shared);
    private static readonly UnitMapDropPolicy ViewSessionOnly = new(CanView: true, CanEdit: false, UnitMapLayoutState.SessionOnly);

    private static readonly UnitMapFixture F = UnitMapTestData.Standard200();

    private static UnitMapDropDecision Drop(string moving, string? hover, bool ctrl = false, UnitMapDropPolicy? policy = null)
        => UnitMapDropClassifier.Classify(F.Tree, F.IdOf(moving), hover is null ? null : F.IdOf(hover), ctrl, policy ?? EditShared);

    #region - 뜻 -
    [Fact]
    public void should_move_position_when_dropped_on_empty_canvas()
    {
        Assert.Equal(UnitMapDropDecision.Position(), Drop("8중대", null));
    }

    [Theory]
    [InlineData("8중대", "3대대")]      // 다른 대대로
    [InlineData("8중대", "1연대")]      // 건너뛰기 허용(연대 밑 중대)
    [InlineData("8중대", "제○○사단")]
    [InlineData("41소초", "5중대")]
    public void should_reparent_when_dropped_on_higher_echelon(string moving, string target)
    {
        Assert.Equal(UnitMapDropDecision.Reparent(F.IdOf(target)), Drop(moving, target));
    }

    [Fact]
    public void should_adjoin_when_dropped_on_same_echelon_not_yet_adjacent()
    {
        Assert.Equal(UnitMapDropDecision.Adjoin(F.IdOf("9중대")), Drop("6중대", "9중대"));
    }

    [Theory]
    [InlineData("8중대", "2대대")]      // 현 상위(ISSUE-15 — 플랜대로 막힘 · 사유)
    [InlineData("2대대", "1연대")]
    public void should_block_with_rule_reason_when_dropped_on_current_parent(string moving, string parent)
    {
        var decision = Drop(moving, parent);

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Equal(F.IdOf(parent), decision.TargetId);
        Assert.Equal(UnitDropRules.CanMove(F.Tree, F.IdOf(moving), F.IdOf(parent)).Reason, decision.Reason);
        Assert.Contains("이미", decision.Reason);
    }

    [Fact]
    public void should_block_when_dropped_on_own_descendant()
    {
        var decision = Drop("2대대", "6중대");

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Equal(UnitDropRules.CanMove(F.Tree, F.IdOf("2대대"), F.IdOf("6중대")).Reason, decision.Reason);
    }

    [Fact]
    public void should_block_when_dropped_on_self()
    {
        var decision = Drop("8중대", "8중대");

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Equal(UnitDropRules.CanAdjoin(F.Tree, F.IdOf("8중대"), F.IdOf("8중대")).Reason, decision.Reason);
    }

    [Fact]
    public void should_block_when_already_adjacent()
    {
        var decision = Drop("6중대", "7중대");

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Equal(UnitDropRules.CanAdjoin(F.Tree, F.IdOf("6중대"), F.IdOf("7중대")).Reason, decision.Reason);
        Assert.Contains("이미 인접", decision.Reason);
    }

    [Theory]
    [InlineData("8중대", "11소초")]
    [InlineData("1연대", "3대대")]
    public void should_block_when_dropped_on_lower_echelon(string moving, string target)
    {
        var decision = Drop(moving, target);

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Equal(UnitDropRules.CanMove(F.Tree, F.IdOf(moving), F.IdOf(target)).Reason, decision.Reason);
    }

    [Theory]
    [InlineData("8중대", "3대대")]
    [InlineData("8중대", "9중대")]
    [InlineData("8중대", "11소초")]
    [InlineData("8중대", "8중대")]
    public void should_always_move_position_when_ctrl_held(string moving, string hover)
    {
        Assert.Equal(UnitMapDropDecision.Position(), Drop(moving, hover, ctrl: true));
    }

    [Fact]
    public void should_allow_suspended_unit_as_target_like_rules()
    {
        // ISSUE-16 — 서버는 운용 중지 부대도 상위 · 인접으로 받는다. 판정은 규칙(IsEnable 미검사)을 그대로 따른다.
        var graph = UnitMapTestData.Graph(new[]
        {
            UnitMapTestData.Node(1, "b01", "1대대", UnitMapTestData.ECHELON_BATTALION),
            UnitMapTestData.Node(2, "b02", "2대대", UnitMapTestData.ECHELON_BATTALION, isEnable: false),
            UnitMapTestData.Node(3, "c01", "1중대", UnitMapTestData.ECHELON_COMPANY, 1),
        });
        var tree = UnitMapTestData.Tree(graph);

        Assert.Equal(UnitMapDropKind.Reparent, UnitMapDropClassifier.Classify(tree, 3, 2, false, EditShared).Kind);
        Assert.Equal(UnitMapDropKind.Adjoin, UnitMapDropClassifier.Classify(tree, 1, 2, false, EditShared).Kind);
    }
    #endregion

    #region - 모르는 제대 -
    [Fact]
    public void should_block_everything_when_moving_unit_echelon_unknown()
    {
        var fixture = UnitMapTestData.UnknownEchelon();
        var brigade = fixture.IdOf("1여단");

        var onEmpty = UnitMapDropClassifier.Classify(fixture.Tree, brigade, null, false, EditShared);
        var withCtrl = UnitMapDropClassifier.Classify(fixture.Tree, brigade, fixture.IdOf("1대대"), true, EditShared);
        var onNode = UnitMapDropClassifier.Classify(fixture.Tree, brigade, fixture.IdOf("제1사단"), false, EditShared);

        Assert.All(new[] { onEmpty, withCtrl, onNode }, d =>
        {
            Assert.Equal(UnitMapDropKind.Blocked, d.Kind);
            Assert.Equal(UnitMapText.UnknownEchelonDrag, d.Reason);
        });
    }

    [Fact]
    public void should_block_with_rule_reason_when_target_echelon_unknown()
    {
        var fixture = UnitMapTestData.UnknownEchelon();
        var company = fixture.IdOf("1중대");
        var brigade = fixture.IdOf("1여단");

        var decision = UnitMapDropClassifier.Classify(fixture.Tree, company, brigade, false, EditShared);

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Equal(UnitDropRules.CanMove(fixture.Tree, company, brigade).Reason, decision.Reason);
    }

    [Theory]
    [InlineData(UnitMapLevel.L0, "8중대", false)]
    [InlineData(UnitMapLevel.L1, "8중대", true)]
    [InlineData(UnitMapLevel.L2, "8중대", true)]
    public void should_allow_drag_only_on_l1_l2_when_echelon_known(UnitMapLevel level, string name, bool expected)
    {
        Assert.Equal(expected, UnitMapDropClassifier.CanDrag(F.Tree, F.IdOf(name), level));
    }

    [Fact]
    public void should_not_drag_unknown_echelon_or_missing_unit()
    {
        var fixture = UnitMapTestData.UnknownEchelon();

        Assert.False(UnitMapDropClassifier.CanDrag(fixture.Tree, fixture.IdOf("1여단"), UnitMapLevel.L2));
        Assert.False(UnitMapDropClassifier.CanDrag(fixture.Tree, 999, UnitMapLevel.L2));
        Assert.False(UnitMapDropClassifier.CanDrag(null, 1, UnitMapLevel.L2));
    }
    #endregion

    #region - 권한 (FR-05) -
    [Fact]
    public void should_block_position_with_no_permission_when_shared_and_view_only()
    {
        var decision = Drop("8중대", null, policy: ViewShared);

        Assert.Equal(UnitMapDropDecision.Blocked(UnitMapText.NoPermission), decision);
        Assert.Equal("권한이 없습니다", decision.Reason);
    }

    [Fact]
    public void should_allow_position_when_session_only_and_view_only()
    {
        Assert.Equal(UnitMapDropDecision.Position(), Drop("8중대", null, policy: ViewSessionOnly));
        Assert.Equal(UnitMapDropDecision.Position(), Drop("8중대", "3대대", ctrl: true, policy: ViewSessionOnly));
    }

    [Theory]
    [InlineData("8중대", "3대대")]
    [InlineData("6중대", "9중대")]
    public void should_require_edit_for_reparent_and_adjoin(string moving, string target)
    {
        foreach (var policy in new[] { ViewShared, ViewSessionOnly })
        {
            var decision = Drop(moving, target, policy: policy);

            Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
            Assert.Equal(UnitMapText.NoPermission, decision.Reason);
            Assert.Equal(F.IdOf(target), decision.TargetId);    // 막힘도 그 노드에 해치를 친다(숨기지 않는다)
        }
    }

    [Fact]
    public void should_block_everything_when_cannot_view()
    {
        var policy = new UnitMapDropPolicy(CanView: false, CanEdit: false, UnitMapLayoutState.SessionOnly);

        Assert.Equal(UnitMapText.NoPermission, Drop("8중대", null, policy: policy).Reason);
        Assert.Equal(UnitMapText.NoPermission, Drop("8중대", "3대대", policy: policy).Reason);
    }
    #endregion

    #region - 배치 상태 (ISSUE-17) -
    [Theory]
    [InlineData(UnitMapLayoutState.ReadFailed)]
    [InlineData(UnitMapLayoutState.VersionMismatch)]
    [InlineData(UnitMapLayoutState.Loading)]
    public void should_block_position_when_layout_cannot_be_written(UnitMapLayoutState state)
    {
        var policy = new UnitMapDropPolicy(true, true, state);
        var expected = state switch
        {
            UnitMapLayoutState.ReadFailed => UnitMapText.LayoutReadFailedBlocked,
            UnitMapLayoutState.VersionMismatch => UnitMapText.LayoutVersionBlocked,
            _ => UnitMapText.LayoutLoadingBlocked,
        };

        Assert.Equal(UnitMapDropDecision.Blocked(expected), Drop("8중대", null, policy: policy));
        Assert.Equal(UnitMapDropDecision.Blocked(expected), Drop("8중대", "3대대", ctrl: true, policy: policy));
    }

    [Theory]
    [InlineData(UnitMapLayoutState.ReadFailed)]
    [InlineData(UnitMapLayoutState.VersionMismatch)]
    [InlineData(UnitMapLayoutState.Loading)]
    [InlineData(UnitMapLayoutState.SessionOnly)]
    public void should_keep_reparent_and_adjoin_when_layout_state_blocks_position(UnitMapLayoutState state)
    {
        // FR-51 — 상위 바꾸기 · 인접 연결은 배치 모드와 무관하게 정상 동작
        var policy = new UnitMapDropPolicy(true, true, state);

        Assert.Equal(UnitMapDropKind.Reparent, Drop("8중대", "3대대", policy: policy).Kind);
        Assert.Equal(UnitMapDropKind.Adjoin, Drop("6중대", "9중대", policy: policy).Kind);
    }
    #endregion

    #region - 규칙과 일치 (사본 금지) -
    [Fact]
    public void should_agree_with_unit_drop_rules_for_every_pair_when_standard_200()
    {
        // 같은 입력에서 판정기와 규칙의 결과가 한 쌍도 어긋나지 않는다 — 200 × 200 전수.
        var ids = F.Tree.Ordered.Select(n => n.Id).ToList();
        foreach (var moving in ids)
        {
            var mover = F.Tree.Find(moving)!;
            foreach (var hover in ids)
            {
                var decision = UnitMapDropClassifier.Classify(F.Tree, moving, hover, false, EditShared);
                var target = F.Tree.Find(hover)!;
                var sameEchelon = mover.Echelon == target.Echelon;
                var verdict = sameEchelon
                    ? UnitDropRules.CanAdjoin(F.Tree, moving, hover)
                    : UnitDropRules.CanMove(F.Tree, moving, hover);

                Assert.Equal(verdict.IsAllowed, decision.IsAllowed);
                if (verdict.IsAllowed)
                    Assert.Equal(sameEchelon ? UnitMapDropKind.Adjoin : UnitMapDropKind.Reparent, decision.Kind);
                else
                    Assert.Equal(verdict.Reason, decision.Reason);
                Assert.Equal(hover, decision.TargetId);
            }
        }
    }

    [Fact]
    public void should_block_with_rule_reason_when_tree_not_loaded()
    {
        var decision = UnitMapDropClassifier.Classify(null, 1, 2, false, EditShared);

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Equal(UnitDropRules.CanMove(null, 1, 2).Reason, decision.Reason);
    }

    [Fact]
    public void should_block_with_rule_reason_when_moving_unit_missing()
    {
        var decision = UnitMapDropClassifier.Classify(F.Tree, 9999, null, false, EditShared);

        Assert.Equal(UnitMapDropKind.Blocked, decision.Kind);
        Assert.Equal(UnitDropRules.CanMove(F.Tree, 9999, null).Reason, decision.Reason);
    }
    #endregion

    #region - 요청 · 히트와 함께 -
    [Fact]
    public void should_read_request_fields_when_classifying_drop_request()
    {
        var request = new UnitMapDropRequest(F.IdOf("8중대"), 40, -12, F.IdOf("3대대"), Ctrl: false);

        Assert.Equal(UnitMapDropDecision.Reparent(F.IdOf("3대대")), UnitMapDropClassifier.Classify(F.Tree, request, EditShared));
        Assert.Equal(UnitMapDropDecision.Position(), UnitMapDropClassifier.Classify(F.Tree, request with { Ctrl = true }, EditShared));
    }

    [Fact]
    public void should_treat_drop_on_own_origin_as_position_when_hit_test_excludes_dragged_subtree()
    {
        // ISSUE-14 — 자기 · 예하 위(원래 자리 잔상)에 놓으면: 히트가 그들을 빼므로 '빈 곳' = 위치
        var layout = UnitMapLayout.Compute(F.Tree);
        var hit = UnitMapHitTest.Build(F.Tree.Ordered.Select(n => new UnitMapHitItem(n.Id, layout.Positions[n.Id], n.Echelon)), UnitMapLevel.L2, 1.0);
        var moving = F.IdOf("6중대");
        var exclude = UnitMapHitTest.DragExclusion(F.Tree, moving);

        var hoverOnSelf = hit.HitTest(layout.Positions[moving], exclude);
        var hoverOnChild = hit.HitTest(layout.Positions[F.IdOf("62소초")], exclude);

        Assert.Null(hoverOnSelf);
        Assert.Null(hoverOnChild);
        Assert.Equal(UnitMapDropDecision.Position(), UnitMapDropClassifier.Classify(F.Tree, moving, hoverOnSelf, false, EditShared));
    }
    #endregion
}
