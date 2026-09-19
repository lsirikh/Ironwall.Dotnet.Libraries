using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 권한 매트릭스 드래그 페인팅의 순수 판정 — UIA 에 드래그 패턴이 없어 회귀망은 이 함수와 키보드 폴백이 전부다.
/// (설계 정본 all-windows-drag-wireframe.html L347-L348 · L422 · L243)
/// </summary>
public class PermissionPaintTests
{
    [Fact]
    public void should_not_pick_an_axis_when_the_pointer_has_not_left_the_pressed_cell()
        => Assert.Equal(PaintAxis.None, PermissionPaintMath.AxisFor(new PermissionCell(2, 1), new PermissionCell(2, 1)));

    [Fact]
    public void should_paint_along_the_row_when_the_pointer_moved_sideways()
        => Assert.Equal(PaintAxis.Row, PermissionPaintMath.AxisFor(new PermissionCell(2, 0), new PermissionCell(2, 3)));

    [Fact]
    public void should_paint_along_the_column_when_the_pointer_moved_downwards()
        => Assert.Equal(PaintAxis.Column, PermissionPaintMath.AxisFor(new PermissionCell(0, 1), new PermissionCell(5, 1)));

    [Fact]
    public void should_prefer_the_row_when_the_movement_is_diagonal_and_equal()
        => Assert.Equal(PaintAxis.Row, PermissionPaintMath.AxisFor(new PermissionCell(0, 0), new PermissionCell(2, 2)));

    [Fact]
    public void should_project_a_diagonal_sweep_onto_the_locked_axis()
    {
        var cells = PermissionPaintMath.Cells(PaintAxis.Row, new PermissionCell(2, 0), new PermissionCell(9, 2), rowCount: 5);

        Assert.Equal(new[] { (2, 0), (2, 1), (2, 2) }, cells.Select(c => (c.Row, c.Verb)));
    }

    [Fact]
    public void should_clamp_the_sweep_to_the_last_row_when_the_pointer_goes_past_the_list()
    {
        var cells = PermissionPaintMath.Cells(PaintAxis.Column, new PermissionCell(1, 3), new PermissionCell(99, 3), rowCount: 4);

        Assert.Equal(new[] { 1, 2, 3 }, cells.Select(c => c.Row));
        Assert.All(cells, c => Assert.Equal(3, c.Verb));
    }

    [Fact]
    public void should_return_only_the_pressed_cell_when_no_axis_is_locked_yet()
    {
        var cells = PermissionPaintMath.Cells(PaintAxis.None, new PermissionCell(1, 2), new PermissionCell(1, 2), rowCount: 4);

        Assert.Single(cells);
    }

    // ── 붓 ────────────────────────────────────────────────────────────
    [Fact]
    public void should_turn_the_whole_row_on_when_the_pressed_cell_was_off()
    {
        var matrix = new FakeMatrix(rows: 3);
        var painter = new PermissionPainter(matrix);

        Assert.True(painter.Begin(new PermissionCell(1, 0)));
        painter.MoveTo(new PermissionCell(1, 3));

        Assert.True(matrix.Get(new PermissionCell(1, 0)));
        Assert.True(matrix.Get(new PermissionCell(1, 3)));
        Assert.False(matrix.Get(new PermissionCell(0, 0)));   // 다른 행은 그대로
    }

    [Fact]
    public void should_turn_the_whole_row_off_when_the_pressed_cell_was_on()
    {
        var matrix = new FakeMatrix(rows: 3);
        matrix.SetAll(true);
        var painter = new PermissionPainter(matrix);

        painter.Begin(new PermissionCell(0, 3));
        painter.MoveTo(new PermissionCell(0, 0));

        Assert.All(Enumerable.Range(0, 4), verb => Assert.False(matrix.Get(new PermissionCell(0, verb))));
    }

    [Fact]
    public void should_skip_cells_the_module_does_not_support()
    {
        var matrix = new FakeMatrix(rows: 3);
        matrix.Disable(new PermissionCell(1, 2));
        var painter = new PermissionPainter(matrix);

        painter.Begin(new PermissionCell(1, 0));
        painter.MoveTo(new PermissionCell(1, 3));

        Assert.True(matrix.Get(new PermissionCell(1, 1)));
        Assert.False(matrix.Get(new PermissionCell(1, 2)));   // 만질 수 없는 칸은 칠해지지 않는다
        Assert.True(matrix.Get(new PermissionCell(1, 3)));
    }

    [Fact]
    public void should_not_start_painting_on_a_cell_the_module_does_not_support()
    {
        var matrix = new FakeMatrix(rows: 2);
        matrix.Disable(new PermissionCell(0, 3));
        var painter = new PermissionPainter(matrix);

        Assert.False(painter.Begin(new PermissionCell(0, 3)));
        Assert.False(painter.IsPainting);
        Assert.False(matrix.Get(new PermissionCell(0, 3)));
    }

    [Fact]
    public void should_restore_every_touched_cell_when_the_sweep_is_cancelled()
    {
        var matrix = new FakeMatrix(rows: 3);
        matrix.Set(new PermissionCell(2, 2), true);
        var painter = new PermissionPainter(matrix);

        painter.Begin(new PermissionCell(2, 0));
        painter.MoveTo(new PermissionCell(2, 3));
        painter.Cancel();

        Assert.False(matrix.Get(new PermissionCell(2, 0)));
        Assert.True(matrix.Get(new PermissionCell(2, 2)));    // 원래 켜져 있던 칸은 켜진 채로 돌아온다
        Assert.False(painter.IsPainting);
    }

    [Fact]
    public void should_never_send_anything_while_painting_because_the_save_is_one_call()
    {
        var matrix = new FakeMatrix(rows: 3);
        var painter = new PermissionPainter(matrix);

        painter.Begin(new PermissionCell(0, 0));
        painter.MoveTo(new PermissionCell(2, 0));
        var changed = painter.Finish();

        Assert.Equal(3, changed);
        Assert.Equal(0, matrix.SaveCount);     // 붓은 서버를 모른다
    }

    /// <summary>시험용 매트릭스 — 행 × 4동작.</summary>
    private sealed class FakeMatrix : IPermissionMatrix
    {
        private readonly bool[,] _values;
        private readonly HashSet<PermissionCell> _disabled = new();

        public FakeMatrix(int rows)
        {
            RowCount = rows;
            _values = new bool[rows, PermissionPaintMath.VerbCount];
        }

        public int RowCount { get; }
        public int SaveCount { get; }

        public void Disable(PermissionCell cell) => _disabled.Add(cell);
        public bool IsEnabled(PermissionCell cell) => !_disabled.Contains(cell);
        public bool Get(PermissionCell cell) => _values[cell.Row, cell.Verb];
        public void Set(PermissionCell cell, bool value) => _values[cell.Row, cell.Verb] = value;

        public void SetAll(bool value)
        {
            for (var r = 0; r < RowCount; r++)
                for (var v = 0; v < PermissionPaintMath.VerbCount; v++) _values[r, v] = value;
        }
    }
}
