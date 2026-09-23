using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 조치보고 문구 순서 끌어 놓기 판정 — 순수 함수. 드래그 · Alt+↑/↓ 키보드 폴백이 같은
/// <see cref="ActionReportTemplateDropHandler.Drop"/> 을 부르므로, 여기를 단언하면 두 경로가 함께 보장된다.
/// </summary>
public class ActionReportTemplateDropTests
{
    private static (ActionReportTemplateBoard Board, ActionReportTemplateDropHandler Drop) Make(bool canEdit = true)
    {
        var board = new ActionReportTemplateBoard();
        board.Load(new[]
        {
            ActionReportTemplateSeed.Template(1, "a", 0),
            ActionReportTemplateSeed.Template(2, "b", 1),
            ActionReportTemplateSeed.Template(3, "c", 2),
        });
        return (board, new ActionReportTemplateDropHandler(board, () => canEdit));
    }

    private static DragPayload PayloadOf(ActionReportTemplateBoard board, params int[] indexes)
    {
        var items = indexes.Select(i => (object)board.Items[i]).ToList();
        // 목록 컨트롤은 판정에 쓰이지 않는다 — WPF 컨트롤을 만들면 STA 가 필요해 헤드리스에서 못 돈다.
        return new DragPayload(null!, items, board.Items[indexes[0]].Display);
    }

    [Fact]
    public void should_accept_a_reorder_drop_on_its_own_zone()
    {
        var (board, drop) = Make();

        Assert.True(drop.CanDrop(PayloadOf(board, 0), new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 2)));
    }

    [Fact]
    public void should_refuse_a_drop_on_another_windows_zone()
    {
        var (board, drop) = Make();

        Assert.False(drop.CanDrop(PayloadOf(board, 0), new DropTarget("report-template-component", null, 2)));
    }

    [Fact]
    public void should_refuse_a_drop_that_is_not_a_reorder()
    {
        var (board, drop) = Make();

        Assert.False(drop.CanDrop(PayloadOf(board, 0), new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, -1)));
    }

    [Fact]
    public void should_refuse_a_drop_when_editing_is_not_allowed()
    {
        var (board, drop) = Make(canEdit: false);

        Assert.False(drop.CanDrop(PayloadOf(board, 0), new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 2)));
    }

    [Fact]
    public void should_refuse_items_that_came_from_another_list()
    {
        var (board, drop) = Make();
        var foreign = new DragPayload(null!, new List<object> { new ActionReportTemplateItem(99, "foreign", 0) }, "foreign");

        Assert.False(drop.CanDrop(foreign, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 1)));
    }

    [Fact]
    public void should_reorder_the_board_and_announce_the_previous_order_when_dropped()
    {
        var (board, drop) = Make();
        IReadOnlyList<int>? previous = null;
        drop.Reordered += (_, e) => previous = e.PreviousOrderIds;

        drop.Drop(PayloadOf(board, 2), new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 0));

        Assert.Equal(new[] { 3, 1, 2 }, board.Items.Select(i => i.Id).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, previous);
    }

    [Fact]
    public void should_stay_silent_when_the_drop_changes_nothing()
    {
        var (board, drop) = Make();
        var announced = 0;
        drop.Reordered += (_, _) => announced++;

        drop.Drop(PayloadOf(board, 1), new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 1));

        Assert.Equal(0, announced);
    }

    [Fact]
    public void should_not_touch_the_board_when_the_drop_is_refused()
    {
        var (board, drop) = Make(canEdit: false);

        drop.Drop(PayloadOf(board, 2), new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 0));

        Assert.Equal(new[] { 1, 2, 3 }, board.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void should_build_a_full_order_reorder_payload_indexed_from_zero()
    {
        var (board, _) = Make();
        board.Move(new[] { 2 }, 0);   // c,a,b

        var payload = board.ToReorderPayload();

        Assert.Equal(3, payload.Count);   // 부분이 아니라 화면에 보이는 전체 목록
        Assert.Equal(new[] { 3, 1, 2 }, payload.Select(p => p.Id).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, payload.Select(p => p.DisplayOrder).ToArray());
    }

    [Fact]
    public void should_build_an_undo_payload_from_the_previous_order()
    {
        var previous = new List<int> { 3, 1, 2 };

        var payload = ActionReportTemplateBoard.ToReorderPayload(previous);

        Assert.Equal(new[] { 3, 1, 2 }, payload.Select(p => p.Id).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, payload.Select(p => p.DisplayOrder).ToArray());
    }
}
