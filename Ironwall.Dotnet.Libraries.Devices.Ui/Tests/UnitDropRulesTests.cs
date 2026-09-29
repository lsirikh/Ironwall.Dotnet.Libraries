using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// N-11 FR-06 · FR-09 · FR-12 — 무엇을 어디에 놓을 수 있는가. 서버가 422 로 거절할 것을
/// <b>네트워크에 나가기 전에</b> 전부 막는다(와이어프레임 §5-2 L356-364).
/// </summary>
[Collection("CaliburnIoC")]   // 컬렉션 수를 늘리면 정적 IoC 를 바꾸는 이웃과 겹칠 확률이 올라간다 — 같이 직렬화한다.
public class UnitDropRulesTests
{
    #region - Fixture -
    private static UnitListDto Node(int id, string code, string echelon, int? parentId = null)
        => new() { Id = id, Code = code, Name = code, EchelonRaw = echelon, ParentId = parentId };

    /// <summary>사단1 → 연대2 → 대대3 → 중대5 · 중대6 → 소초7(6 밑) · 대대8(1 밑, 연대 건너뛰기).</summary>
    private static UnitTreeModel Tree() => UnitTreeBuilder.Build(new UnitGraphDto
    {
        Nodes = new List<UnitListDto>
        {
            Node(1, "d01", "Division"),
            Node(2, "r01", "Regiment", 1),
            Node(3, "b01", "Battalion", 2),
            Node(5, "c05", "Company", 3),
            Node(6, "c06", "Company", 3),
            Node(7, "o07", "Outpost", 6),
            Node(8, "b08", "Battalion", 1),
            Node(9, "u09", "Platoon"),           // 이 판본이 모르는 제대
            Node(10, "c10", "Company", 8),
        },
        Edges = new UnitGraphEdgesDto
        {
            Hierarchy = new List<List<int>> { new() { 1, 2 }, new() { 2, 3 }, new() { 3, 5 }, new() { 3, 6 }, new() { 6, 7 }, new() { 1, 8 }, new() { 8, 10 } },
            Adjacency = new List<List<int>> { new() { 5, 6 } },
        },
    });
    #endregion

    #region - 상위 바꾸기 -
    [Theory]
    [InlineData(6, 2)]      // 중대 → 연대 (건너뛰기 허용)
    [InlineData(6, 1)]      // 중대 → 사단 (두 단계 건너뛰기)
    [InlineData(6, 8)]      // 중대 → 다른 대대
    [InlineData(7, 5)]      // 소초 → 다른 중대
    public void should_allow_move_when_target_is_strictly_higher_echelon(int moving, int target)
        => Assert.True(UnitDropRules.CanMove(Tree(), moving, target).IsAllowed);

    [Theory]
    [InlineData(6, 5)]      // 중대 → 중대(같은 제대)
    [InlineData(5, 7)]      // 중대 → 소초(하위 제대, 자손 아님)
    public void should_block_move_when_target_is_not_a_higher_echelon(int moving, int target)
    {
        var verdict = UnitDropRules.CanMove(Tree(), moving, target);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("더 높은 제대", verdict.Reason);
    }

    [Fact]
    public void should_block_move_when_target_is_itself()
    {
        var verdict = UnitDropRules.CanMove(Tree(), 6, 6);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("자기 자신", verdict.Reason);
    }

    [Fact]
    public void should_block_move_when_target_is_a_descendant()
    {
        // 제대 규칙만으로도 막히지만, 제대를 모르는 노드가 섞였을 때를 대비해 자손 검사를 따로 둔다.
        var verdict = UnitDropRules.CanMove(Tree(), 3, 7);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("하위 부대", verdict.Reason);
    }

    [Fact]
    public void should_block_move_when_target_is_already_the_parent()
    {
        var verdict = UnitDropRules.CanMove(Tree(), 6, 3);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("이미", verdict.Reason);
    }

    [Fact]
    public void should_allow_move_to_root_when_node_has_a_parent()
        => Assert.True(UnitDropRules.CanMove(Tree(), 6, null).IsAllowed);

    [Fact]
    public void should_block_move_to_root_when_node_is_already_a_root()
    {
        var verdict = UnitDropRules.CanMove(Tree(), 1, null);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("이미 최상위", verdict.Reason);
    }

    [Fact]
    public void should_block_move_when_an_echelon_is_unknown_to_this_build()
    {
        Assert.False(UnitDropRules.CanMove(Tree(), 9, 1).IsAllowed);     // 옮기는 쪽을 모름
        Assert.False(UnitDropRules.CanMove(Tree(), 6, 9).IsAllowed);     // 받는 쪽을 모름
    }

    [Fact]
    public void should_block_move_when_the_node_is_not_in_the_tree()
    {
        Assert.False(UnitDropRules.CanMove(Tree(), 404, 1).IsAllowed);
        Assert.False(UnitDropRules.CanMove(Tree(), 6, 404).IsAllowed);
        Assert.False(UnitDropRules.CanMove(null, 6, 1).IsAllowed);
    }
    #endregion

    #region - 인접 -
    [Fact]
    public void should_allow_adjacency_when_both_are_the_same_echelon()
        => Assert.True(UnitDropRules.CanAdjoin(Tree(), 10, 5).IsAllowed);           // 둘 다 중대, 아직 이어지지 않았다

    [Fact]
    public void should_block_adjacency_when_echelons_differ()
    {
        var verdict = UnitDropRules.CanAdjoin(Tree(), 7, 6);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("같은 제대", verdict.Reason);
    }

    [Fact]
    public void should_block_adjacency_when_it_is_the_same_unit()
    {
        var verdict = UnitDropRules.CanAdjoin(Tree(), 6, 6);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("자기 자신", verdict.Reason);
    }

    [Fact]
    public void should_block_adjacency_when_the_pair_already_exists()
    {
        var verdict = UnitDropRules.CanAdjoin(Tree(), 5, 6);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("이미 인접", verdict.Reason);
    }

    [Fact]
    public void should_merge_adjacency_as_a_full_set_when_adding_or_removing()
    {
        // 서버가 전삭제 후 재생성하므로 '추가' 도 현재 전체를 함께 보내야 한다.
        Assert.Equal(new[] { 3, 5, 9 }, UnitDropRules.MergeAdjacency(new[] { 9, 5, 5 }, selfId: 6, add: 3));
        Assert.Equal(new[] { 9 }, UnitDropRules.MergeAdjacency(new[] { 5, 9 }, selfId: 6, remove: 5));
        Assert.Empty(UnitDropRules.MergeAdjacency(new[] { 6 }, selfId: 6));          // 자기 자신은 빠진다
        Assert.Empty(UnitDropRules.MergeAdjacency(null, selfId: 6));
    }
    #endregion

    [Fact]
    public void should_spell_every_known_echelon_in_korean()
    {
        var texts = System.Enum.GetValues<Ironwall.Dotnet.Libraries.Enums.EnumUnitEchelon>()
                               .Select(UnitDropRules.EchelonText)
                               .ToList();

        Assert.Equal(new[] { "사단", "연대", "대대", "중대", "소초" }, texts);
    }
}
