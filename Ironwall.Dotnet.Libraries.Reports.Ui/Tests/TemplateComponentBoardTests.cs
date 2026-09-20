using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 템플릿 구성 순서 — 드래그의 판정을 UI 에서 분리한 순수 모델(드래그 규칙: 판정은 순수 함수 + 헤드리스 시험).
/// UIA 에 드래그 패턴이 없어(.NET 8 WPF) 제스처 자체는 단언할 수 없다 — 그래서 여기가 회귀망이다.
/// </summary>
public class TemplateComponentBoardTests
{
    private static TemplateComponentBoard BoardWith(string[] catalog, params string[] saved)
    {
        var board = new TemplateComponentBoard();
        board.Load(ReportSeed.Catalog(catalog), ReportSeed.Template(1, "t", saved).Components);
        return board;
    }

    private static string[] Ids(TemplateComponentBoard board) => board.Items.Select(i => i.Id).ToArray();

    [Fact]
    public void should_put_saved_components_first_in_their_saved_order_when_loaded()
    {
        var board = BoardWith(new[] { "a", "b", "c", "d" }, "c", "a");

        Assert.Equal(new[] { "c", "a", "b", "d" }, Ids(board));
        Assert.True(board.Items[0].IsEnabled);
        Assert.True(board.Items[1].IsEnabled);
        Assert.False(board.Items[2].IsEnabled);
    }

    [Fact]
    public void should_keep_a_saved_component_visible_when_the_server_catalog_no_longer_has_it()
    {
        // 말없이 빼면 [적용] 때 조용히 사라진다 — 보이게 두고 켜 둔다.
        var board = BoardWith(new[] { "a", "b" }, "ghost", "a");

        Assert.Contains("ghost", Ids(board));
        Assert.True(board.Items.Single(i => i.Id == "ghost").IsEnabled);
    }

    [Fact]
    public void should_not_be_dirty_when_just_loaded()
    {
        var board = BoardWith(new[] { "a", "b", "c" }, "a", "b");

        Assert.False(board.IsDirty);
        Assert.Equal("a,b", board.Signature);
        Assert.Equal("a,b", board.BaselineSignature);
    }

    [Fact]
    public void should_become_dirty_when_an_enabled_component_is_moved()
    {
        var board = BoardWith(new[] { "a", "b", "c" }, "a", "b");

        Assert.True(board.Move(new[] { 1 }, 0));      // b 를 맨 앞으로

        Assert.Equal(new[] { "b", "a", "c" }, Ids(board));
        Assert.True(board.IsDirty);
        Assert.Equal("b,a", board.Signature);
    }

    [Fact]
    public void should_not_become_dirty_when_a_disabled_component_is_moved()
    {
        // 끈 줄의 자리는 서버로 나가지 않는다 — 보낼 것이 같으므로 미적용으로 세지 않는다.
        var board = BoardWith(new[] { "a", "b", "c" }, "a");

        Assert.True(board.Move(new[] { 2 }, 1));      // c(꺼짐) 를 b(꺼짐) 앞으로

        Assert.Equal(new[] { "a", "c", "b" }, Ids(board));
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_become_dirty_when_a_component_is_checked_on()
    {
        var board = BoardWith(new[] { "a", "b" }, "a");

        board.Items.Single(i => i.Id == "b").IsEnabled = true;

        Assert.True(board.IsDirty);
        Assert.Equal("a,b", board.Signature);
    }

    [Fact]
    public void should_report_no_move_when_the_row_is_dropped_back_where_it_was()
    {
        var board = BoardWith(new[] { "a", "b", "c" }, "a", "b", "c");

        Assert.False(board.Move(new[] { 1 }, 1));     // 제자리
        Assert.False(board.Move(new[] { 1 }, 2));     // 바로 아래 = 제자리
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_keep_original_order_of_the_picked_rows_when_several_are_moved_together()
    {
        var board = BoardWith(new[] { "a", "b", "c", "d" }, "a", "b", "c", "d");

        Assert.True(board.Move(new[] { 0, 2 }, 4));   // a 와 c 를 맨 뒤로

        Assert.Equal(new[] { "b", "d", "a", "c" }, Ids(board));
    }

    [Fact]
    public void should_refuse_an_out_of_range_insertion_without_changing_anything()
    {
        var board = BoardWith(new[] { "a", "b" }, "a", "b");

        Assert.False(board.Move(new[] { 0 }, 9));
        Assert.False(board.Move(new[] { 5 }, 0));
        Assert.Equal(new[] { "a", "b" }, Ids(board));
    }

    [Fact]
    public void should_restore_order_and_checks_when_reverted()
    {
        var board = BoardWith(new[] { "a", "b", "c" }, "a", "b");
        board.Move(new[] { 1 }, 0);
        board.Items.Single(i => i.Id == "c").IsEnabled = true;
        Assert.True(board.IsDirty);

        board.Revert();

        Assert.Equal(new[] { "a", "b", "c" }, Ids(board));
        Assert.False(board.Items.Single(i => i.Id == "c").IsEnabled);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_renumber_order_from_zero_over_enabled_components_only()
    {
        var board = BoardWith(new[] { "a", "b", "c" }, "a", "b");
        board.Move(new[] { 1 }, 0);                   // b, a, c

        var config = board.ToConfig();

        Assert.Equal(2, config.Count);
        Assert.Equal("b", config[0].Id);
        Assert.Equal(0, config[0].Order);
        Assert.Equal("a", config[1].Id);
        Assert.Equal(1, config[1].Order);
        Assert.All(config, c => Assert.True(c.Enabled));
    }

    [Fact]
    public void should_stop_being_dirty_when_the_baseline_is_marked_after_a_save()
    {
        var board = BoardWith(new[] { "a", "b" }, "a", "b");
        board.Move(new[] { 1 }, 0);
        Assert.True(board.IsDirty);

        board.MarkBaseline();

        Assert.False(board.IsDirty);
        Assert.Equal("b,a", board.BaselineSignature);
    }

    [Fact]
    public void should_move_one_row_down_through_the_shared_entry_point()
    {
        var edit = EditWith("a", "b", "c");
        var second = edit.Board.Items[1];

        Assert.True(edit.MoveComponent(second, 1));

        Assert.Equal(new[] { "a", "c", "b" }, edit.Board.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void should_move_one_row_up_through_the_shared_entry_point()
    {
        var edit = EditWith("a", "b", "c");
        var second = edit.Board.Items[1];

        Assert.True(edit.MoveComponent(second, -1));

        Assert.Equal(new[] { "b", "a", "c" }, edit.Board.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void should_do_nothing_when_the_first_row_is_moved_up()
    {
        var edit = EditWith("a", "b", "c");

        Assert.False(edit.MoveComponent(edit.Board.Items[0], -1));
        Assert.Equal(new[] { "a", "b", "c" }, edit.Board.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void should_do_nothing_when_the_last_row_is_moved_down()
    {
        var edit = EditWith("a", "b", "c");

        Assert.False(edit.MoveComponent(edit.Board.Items[^1], 1));
        Assert.Equal(new[] { "a", "b", "c" }, edit.Board.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void should_refuse_to_move_anything_when_editing_is_not_allowed()
    {
        var edit = EditWith("a", "b", "c");
        edit.CanEdit = false;

        Assert.False(edit.MoveComponent(edit.Board.Items[1], -1));
        Assert.Equal(new[] { "a", "b", "c" }, edit.Board.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void should_refuse_a_row_that_is_not_on_this_board()
    {
        var edit = EditWith("a", "b");

        Assert.False(edit.MoveComponent(new TemplateComponentItem("x", "x", null), -1));
    }

    /// <summary>진짜 편집 뷰모델을 세운다 — ▲▼ · Alt 키 · 드래그가 <b>같은 진입점</b>을 쓰는지 보기 위해.</summary>
    private static ReportTemplateEditViewModel EditWith(params string[] ids)
    {
        var api = new FakeReportApiService();
        api.Components.AddRange(ReportSeed.Catalog(ids));
        var edit = new ReportTemplateEditViewModel(new Caliburn.Micro.EventAggregator(), new FakeLogService(), api);
        edit.Attach(new Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles.ConsoleDetailPresenter());
        edit.LoadAsync(ReportSeed.Template(1, "t", ids)).GetAwaiter().GetResult();
        return edit;
    }

    [Fact]
    public void should_match_the_kernel_reorder_math_when_moving_one_row_down()
    {
        // 키보드 폴백(Alt+↓)의 삽입 인덱스 규약: 마지막 줄의 한 칸 뒤 = index + 2.
        var board = BoardWith(new[] { "a", "b", "c" }, "a", "b", "c");

        Assert.True(DragMath.IsRealMove(0, 2));
        Assert.True(board.Move(new[] { 0 }, 2));

        Assert.Equal(new[] { "b", "a", "c" }, Ids(board));
    }
}
