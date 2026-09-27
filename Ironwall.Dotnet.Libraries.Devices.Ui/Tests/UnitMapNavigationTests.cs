using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System.Collections.Generic;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-08 — 관계도 화살표 선택 이동(FR-36)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-K001 · K007 · K013 · K019 · K025 · K152 · K153 · K155.
                  편제는 섞임 픽스처 — 1대대(10) › 1중대(11: 11소초 14 · 12소초 15) · 2중대(12) · 3중대(13: 31소초 16).
                  위치는 시험이 직접 준다(자동 배치 결과에 기대지 않는다 — 탐색은 "받은 위치" 로만 판정한다).
****************************************************************************/
public class UnitMapNavigationTests
{
    private static readonly UnitMapFixture Org = UnitMapTestData.Mixed();

    private static Dictionary<int, Point> Positions() => new()
    {
        [10] = new Point(200, 0),
        [11] = new Point(0, 160),
        [12] = new Point(200, 160),
        [13] = new Point(400, 160),
        [14] = new Point(0, 320),     // 1중대 아래 세로 한 줄
        [15] = new Point(0, 450),
        [16] = new Point(400, 320),
    };

    private static int? Next(int? current, UnitMapKeyCommand key, int? mine = null, Dictionary<int, Point>? positions = null)
        => UnitMapNavigation.Next(Org.Tree, positions ?? Positions(), current, key, mine);

    [Fact]
    public void should_go_to_parent_when_up()
    {
        Assert.Equal(10, Next(11, UnitMapKeyCommand.Up));
        Assert.Equal(11, Next(14, UnitMapKeyCommand.Up));
    }

    [Fact]
    public void should_stay_when_up_at_a_root()
    {
        Assert.Equal(10, Next(10, UnitMapKeyCommand.Up));                      // SIM-K152
    }

    [Fact]
    public void should_stay_when_up_at_an_orphan_whose_parent_is_outside_the_graph()
    {
        var orphan = UnitMapTestData.OrphanParent();
        var id = orphan.IdOf("9중대");

        Assert.Equal(id, UnitMapNavigation.Next(orphan.Tree, new Dictionary<int, Point> { [id] = new(0, 0) }, id, UnitMapKeyCommand.Up, null));
    }

    [Fact]
    public void should_go_to_first_child_by_code_when_down()
    {
        Assert.Equal(11, Next(10, UnitMapKeyCommand.Down));
        Assert.Equal(14, Next(11, UnitMapKeyCommand.Down));
    }

    [Fact]
    public void should_stay_when_down_at_a_leaf()
    {
        Assert.Equal(12, Next(12, UnitMapKeyCommand.Down));
    }

    [Fact]
    public void should_move_to_same_depth_neighbour_by_screen_x_when_left_or_right()
    {
        Assert.Equal(12, Next(11, UnitMapKeyCommand.Right));
        Assert.Equal(13, Next(12, UnitMapKeyCommand.Right));
        Assert.Equal(11, Next(12, UnitMapKeyCommand.Left));
    }

    [Fact]
    public void should_stay_at_the_edge_when_no_neighbour_that_way()
    {
        Assert.Equal(13, Next(13, UnitMapKeyCommand.Right));
        Assert.Equal(11, Next(11, UnitMapKeyCommand.Left));
    }

    [Fact]
    public void should_follow_moved_positions_not_tree_order_when_delta_applied()
    {
        // SIM-K153 — 사용자 Δ 로 3중대를 1중대 왼쪽에 옮기면 '이웃' 은 화면 x 순이다.
        var moved = Positions();
        moved[13] = new Point(-100, 160);

        Assert.Equal(13, Next(11, UnitMapKeyCommand.Left, positions: moved));
        Assert.Equal(11, Next(13, UnitMapKeyCommand.Right, positions: moved));
    }

    [Fact]
    public void should_break_x_ties_by_y_when_neighbours_share_a_column()
    {
        Assert.Equal(15, Next(14, UnitMapKeyCommand.Right));                  // 같은 x(세로 한 줄) → 아래 칸
        Assert.Equal(16, Next(15, UnitMapKeyCommand.Right));
        Assert.Equal(14, Next(15, UnitMapKeyCommand.Left));
    }

    [Fact]
    public void should_jump_to_my_unit_when_home()
    {
        Assert.Equal(13, Next(11, UnitMapKeyCommand.Home, mine: 13));         // SIM-K025
        Assert.Equal(13, Next(null, UnitMapKeyCommand.Home, mine: 13));
    }

    [Fact]
    public void should_stay_when_home_without_a_known_unit_of_mine()
    {
        Assert.Equal(11, Next(11, UnitMapKeyCommand.Home, mine: null));        // SIM-K155
        Assert.Equal(11, Next(11, UnitMapKeyCommand.Home, mine: 999));
    }

    [Fact]
    public void should_select_the_first_root_when_an_arrow_comes_without_selection()
    {
        Assert.Equal(10, Next(null, UnitMapKeyCommand.Down));
        Assert.Equal(10, Next(777, UnitMapKeyCommand.Right));                  // 선택이 다른 곳에서 지워졌다
    }

    [Fact]
    public void should_leave_selection_unchanged_for_keys_that_are_not_navigation()
    {
        Assert.Equal(12, Next(12, UnitMapKeyCommand.Enter));
        Assert.Equal(12, Next(12, UnitMapKeyCommand.Undo));
    }

    [Fact]
    public void should_fall_back_to_tree_order_when_positions_are_missing()
    {
        Assert.Equal(12, Next(11, UnitMapKeyCommand.Right, positions: new Dictionary<int, Point>()));
    }

    [Fact]
    public void should_return_null_when_the_graph_is_empty()
    {
        Assert.Null(UnitMapNavigation.Next(Consoles.Units.Model.UnitTreeModel.Empty, new Dictionary<int, Point>(), null, UnitMapKeyCommand.Down, null));
    }
}
