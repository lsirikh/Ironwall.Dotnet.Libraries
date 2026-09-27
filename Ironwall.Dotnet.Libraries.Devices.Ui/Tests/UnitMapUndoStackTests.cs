using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-15 — 관계도 되돌리기 표(배치 20 + 편제 1)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-F117 · F119 · F123 · F127 · K121 · K122.
****************************************************************************/
public class UnitMapUndoStackTests
{
    private static UnitMapPositionUndo Move(int unitId, bool sessionOnly = false)
        => new(unitId, null, new Vector(unitId, unitId), null, new[] { unitId }, sessionOnly);

    [Fact]
    public void should_push_out_the_oldest_position_when_the_21st_arrives()
    {
        var stack = new UnitMapUndoStack();
        for (var i = 1; i <= 21; i++) stack.Push(Move(i));

        Assert.Equal(UnitMapUndoStack.LayoutCapacity, stack.LayoutCount);
        Assert.DoesNotContain(stack.Entries, e => e is UnitMapPositionUndo { UnitId: 1 });
        Assert.Equal(21, ((UnitMapPositionUndo)stack.Peek()!).UnitId);
        Assert.Equal(2, ((UnitMapPositionUndo)stack.Entries.Last()).UnitId);   // 가장 오래된 남은 것
    }

    [Fact]
    public void should_keep_only_the_last_server_operation_when_reparent_and_adjacency_repeat()
    {
        var stack = new UnitMapUndoStack();
        stack.Push(new UnitMapReparentUndo(7, 2, 3));
        stack.Push(Move(9));
        stack.Push(new UnitMapAdjacencyUndo(7, 8, Added: true));

        var server = stack.Entries.Where(e => !e.IsLayout).ToList();
        Assert.IsType<UnitMapAdjacencyUndo>(Assert.Single(server));
        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void should_not_count_server_operations_against_the_layout_capacity()
    {
        var stack = new UnitMapUndoStack();
        for (var i = 1; i <= 20; i++) stack.Push(Move(i));
        stack.Push(new UnitMapReparentUndo(99, 1, 2));

        Assert.Equal(20, stack.LayoutCount);
        Assert.Equal(21, stack.Count);
    }

    [Fact]
    public void should_undo_in_reverse_order_across_kinds_when_ctrl_z_repeats()
    {
        // SIM-F123 — 위치 A → 상위 B → 위치 C, Ctrl+Z ×3 = C · B · A.
        var stack = new UnitMapUndoStack();
        var a = Move(1);
        var b = new UnitMapReparentUndo(2, 5, 6);
        var c = Move(3);
        stack.Push(a);
        stack.Push(b);
        stack.Push(c);

        var order = new List<UnitMapUndoEntry>();
        while (stack.Peek() is { } top)
        {
            order.Add(top);
            stack.CompleteUndo(top, succeeded: true);
        }

        Assert.Equal(new UnitMapUndoEntry[] { c, b, a }, order);
    }

    [Fact]
    public void should_keep_the_entry_when_undo_fails()
    {
        var stack = new UnitMapUndoStack();
        var entry = Move(4);
        stack.Push(entry);

        stack.CompleteUndo(entry, succeeded: false);

        Assert.Same(entry, stack.Peek());
        Assert.Same(entry, stack.Bar);
    }

    [Fact]
    public void should_survive_a_reload_and_drop_only_entries_whose_units_vanished()
    {
        var stack = new UnitMapUndoStack();
        stack.Push(Move(1));
        stack.Push(new UnitMapReparentUndo(2, 5, 6));
        stack.Push(Move(3));

        var removed = stack.Prune(id => id != 2);          // 부대 2 가 다른 곳에서 삭제됨(SIM-F119)

        Assert.IsType<UnitMapReparentUndo>(Assert.Single(removed));
        Assert.Equal(new[] { 3, 1 }, stack.Entries.Cast<UnitMapPositionUndo>().Select(e => e.UnitId));
        Assert.Empty(stack.Prune(_ => true));              // 다시 읽어도 멀쩡하면 아무것도 빠지지 않는다
    }

    [Fact]
    public void should_mark_session_only_entries_when_pushed_in_session_only_mode()
    {
        var stack = new UnitMapUndoStack();
        stack.Push(Move(1, sessionOnly: true));

        Assert.True(stack.Bar!.IsSessionOnly);
        Assert.False(new UnitMapReparentUndo(1, 2, 3).IsSessionOnly);
    }

    [Fact]
    public void should_replace_the_bar_item_when_the_next_operation_arrives()
    {
        var stack = new UnitMapUndoStack();
        var first = Move(1);
        var second = new UnitMapAdjacencyUndo(1, 2, Added: true);

        stack.Push(first);
        Assert.Same(first, stack.Bar);
        stack.Push(second);

        Assert.Same(second, stack.Bar);                    // SIM-F127 — 타이머가 아니라 다음 조작이 교체한다
    }

    [Fact]
    public void should_hide_the_bar_but_keep_entries_when_dismissed()
    {
        var stack = new UnitMapUndoStack();
        stack.Push(Move(1));

        stack.Dismiss();

        Assert.Null(stack.Bar);
        Assert.Equal(1, stack.Count);                      // Ctrl+Z 는 여전히 된다
    }

    [Fact]
    public void should_clear_the_bar_when_its_entry_is_undone()
    {
        var stack = new UnitMapUndoStack();
        var older = Move(1);
        var newer = Move(2);
        stack.Push(older);
        stack.Push(newer);

        stack.CompleteUndo(newer, succeeded: true);

        Assert.Null(stack.Bar);
        Assert.Same(older, stack.Peek());
    }

    [Fact]
    public void should_list_reset_units_when_layout_reset_is_recorded()
    {
        var reset = new UnitMapLayoutResetUndo(new Dictionary<int, Vector> { [5] = new(1, 1), [2] = new(2, 2) }, All: true, null, false);

        Assert.True(reset.IsLayout);
        Assert.Equal(new[] { 2, 5 }, reset.UnitIds);
    }
}
