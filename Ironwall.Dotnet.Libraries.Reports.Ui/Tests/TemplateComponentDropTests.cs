using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 끌어 놓기 담당 — 커널이 부르는 판정 · 처리. 드래그와 키보드 폴백이 <b>같은 담당</b>을 부르므로
/// 여기를 단언하면 두 경로가 함께 보장된다.
/// </summary>
public class TemplateComponentDropTests
{
    private static (TemplateComponentBoard Board, TemplateComponentDropHandler Drop) Make(bool canEdit = true)
    {
        var board = new TemplateComponentBoard();
        board.Load(ReportSeed.Catalog("a", "b", "c"), ReportSeed.Template(1, "t", "a", "b", "c").Components);
        return (board, new TemplateComponentDropHandler(board, () => canEdit));
    }

    private static DragPayload PayloadOf(TemplateComponentBoard board, params int[] indexes)
    {
        var items = indexes.Select(i => (object)board.Items[i]).ToList();
        // 목록 컨트롤은 판정에 쓰이지 않는다 — WPF 컨트롤을 만들면 STA 가 필요해 헤드리스에서 못 돈다.
        return new DragPayload(null!, items, board.Items[indexes[0]].Display);
    }

    [Fact]
    public void should_accept_a_reorder_drop_on_its_own_zone()
    {
        var (board, drop) = Make();

        Assert.True(drop.CanDrop(PayloadOf(board, 0), new DropTarget(TemplateComponentDropHandler.ZoneKey, null, 2)));
    }

    [Fact]
    public void should_refuse_a_drop_on_another_windows_zone()
    {
        var (board, drop) = Make();

        Assert.False(drop.CanDrop(PayloadOf(board, 0), new DropTarget("device-group", null, 2)));
    }

    [Fact]
    public void should_refuse_a_drop_that_is_not_a_reorder()
    {
        var (board, drop) = Make();

        // InsertionIndex 가 -1 이면 순서 드롭이 아니다 — 이 화면에는 그런 드롭존이 없다.
        Assert.False(drop.CanDrop(PayloadOf(board, 0), new DropTarget(TemplateComponentDropHandler.ZoneKey, null, -1)));
    }

    [Fact]
    public void should_refuse_a_drop_when_editing_is_not_allowed()
    {
        var (board, drop) = Make(canEdit: false);

        Assert.False(drop.CanDrop(PayloadOf(board, 0), new DropTarget(TemplateComponentDropHandler.ZoneKey, null, 2)));
    }

    [Fact]
    public void should_refuse_items_that_came_from_another_list()
    {
        var (board, drop) = Make();
        var foreign = new DragPayload(null!, new List<object> { new TemplateComponentItem("x", "x", null) }, "x");

        Assert.False(drop.CanDrop(foreign, new DropTarget(TemplateComponentDropHandler.ZoneKey, null, 1)));
    }

    [Fact]
    public void should_refuse_an_empty_payload()
    {
        var (_, drop) = Make();
        var empty = new DragPayload(null!, new List<object>(), string.Empty);

        Assert.False(drop.CanDrop(empty, new DropTarget(TemplateComponentDropHandler.ZoneKey, null, 1)));
    }

    [Fact]
    public void should_reorder_the_board_and_announce_it_when_dropped()
    {
        var (board, drop) = Make();
        var announced = 0;
        drop.Reordered += (_, _) => announced++;

        drop.Drop(PayloadOf(board, 2), new DropTarget(TemplateComponentDropHandler.ZoneKey, null, 0));

        Assert.Equal(new[] { "c", "a", "b" }, board.Items.Select(i => i.Id).ToArray());
        Assert.Equal(1, announced);
    }

    [Fact]
    public void should_stay_silent_when_the_drop_changes_nothing()
    {
        var (board, drop) = Make();
        var announced = 0;
        drop.Reordered += (_, _) => announced++;

        drop.Drop(PayloadOf(board, 1), new DropTarget(TemplateComponentDropHandler.ZoneKey, null, 1));

        Assert.Equal(0, announced);
        Assert.False(board.IsDirty);
    }

    [Fact]
    public void should_not_touch_the_board_when_the_drop_is_refused()
    {
        var (board, drop) = Make(canEdit: false);

        drop.Drop(PayloadOf(board, 2), new DropTarget(TemplateComponentDropHandler.ZoneKey, null, 0));

        Assert.Equal(new[] { "a", "b", "c" }, board.Items.Select(i => i.Id).ToArray());
    }
}
