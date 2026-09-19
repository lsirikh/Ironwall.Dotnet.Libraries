namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;

/// <summary>드래그 페인팅이 번지는 방향. 처음 데드존을 넘은 방향으로 <b>고정</b>된다.</summary>
public enum PaintAxis
{
    /// <summary>아직 정해지지 않음(누른 칸 하나).</summary>
    None,
    /// <summary>같은 행의 칸들(가로).</summary>
    Row,
    /// <summary>같은 열의 칸들(세로).</summary>
    Column,
}

/// <summary>매트릭스의 한 칸.</summary>
/// <param name="Row">모듈 행.</param>
/// <param name="Verb">동작 열 — 0 조회 · 1 편집 · 2 삭제 · 3 제어(목업 L1209 순서).</param>
public readonly record struct PermissionCell(int Row, int Verb);

/// <summary>
/// 권한 매트릭스 드래그 페인팅의 <b>순수 판정</b> — 어느 축으로 번지고 어떤 칸들이 칠해지는가.
/// WPF 에 기대지 않는다(UIA 에 드래그 패턴이 없어 회귀망은 이 함수와 키보드 폴백이 전부다).
/// (설계 정본 all-windows-drag-wireframe.html L347-L348 · L422)
/// </summary>
public static class PermissionPaintMath
{
    /// <summary>동작 열 수(조회 · 편집 · 삭제 · 제어).</summary>
    public const int VerbCount = 4;

    /// <summary>
    /// 누른 칸에서 지금 칸까지의 축. 가로로 더 멀리 갔으면 <see cref="PaintAxis.Row"/>, 세로면 <see cref="PaintAxis.Column"/>.
    /// 같은 칸이면 아직 정해지지 않았다(<see cref="PaintAxis.None"/>).
    /// </summary>
    public static PaintAxis AxisFor(PermissionCell start, PermissionCell current)
    {
        var dRow = Math.Abs(current.Row - start.Row);
        var dVerb = Math.Abs(current.Verb - start.Verb);
        if (dRow == 0 && dVerb == 0) return PaintAxis.None;
        return dVerb >= dRow ? PaintAxis.Row : PaintAxis.Column;
    }

    /// <summary>
    /// 칠할 칸들(누른 칸 포함). 축이 정해지지 않았으면 누른 칸 하나뿐이고,
    /// 축 밖으로 벗어난 좌표는 축 위로 투영한다 — 대각선으로 움직여도 한 줄만 칠한다.
    /// </summary>
    public static IReadOnlyList<PermissionCell> Cells(PaintAxis axis, PermissionCell start, PermissionCell current, int rowCount)
    {
        if (rowCount <= 0) return Array.Empty<PermissionCell>();
        if (start.Row < 0 || start.Row >= rowCount) return Array.Empty<PermissionCell>();
        if (start.Verb < 0 || start.Verb >= VerbCount) return Array.Empty<PermissionCell>();

        if (axis == PaintAxis.None) return new[] { start };

        var cells = new List<PermissionCell>();
        if (axis == PaintAxis.Row)
        {
            var to = Clamp(current.Verb, 0, VerbCount - 1);
            var (lo, hi) = Order(start.Verb, to);
            for (var verb = lo; verb <= hi; verb++) cells.Add(new PermissionCell(start.Row, verb));
        }
        else
        {
            var to = Clamp(current.Row, 0, rowCount - 1);
            var (lo, hi) = Order(start.Row, to);
            for (var row = lo; row <= hi; row++) cells.Add(new PermissionCell(row, start.Verb));
        }
        return cells;
    }

    private static (int Low, int High) Order(int a, int b) => a <= b ? (a, b) : (b, a);

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}

/// <summary>페인팅이 만지는 매트릭스 — 화면 없이 시험할 수 있게 좁혀 둔 통로.</summary>
public interface IPermissionMatrix
{
    int RowCount { get; }
    /// <summary>그 칸을 만질 수 있는가(그 모듈에 그 동작이 없으면 거짓).</summary>
    bool IsEnabled(PermissionCell cell);
    bool Get(PermissionCell cell);
    void Set(PermissionCell cell, bool value);
}

/// <summary>
/// 드래그 페인팅의 상태기계 — 누른 칸을 뒤집어 <b>붓 값</b>을 정하고, 쓰는 동안 같은 행 · 열에 같은 값을 칠한다.
/// </summary>
/// <remarks>
/// <para>서버 호출은 0이다 — 칠한 결과는 [적용](= 저장 전체 교체 1회) 때만 나간다.</para>
/// <para><see cref="Cancel"/>(Esc)은 <b>칠하기 시작 전 값</b>으로 전부 되돌린다.</para>
/// <para>비활성 칸은 건너뛴다(칠해지지도, 붓이 되지도 않는다).</para>
/// </remarks>
public sealed class PermissionPainter
{
    private readonly IPermissionMatrix _matrix;
    private readonly Dictionary<PermissionCell, bool> _before = new();
    private PermissionCell _start;
    private bool _brush;

    public PermissionPainter(IPermissionMatrix matrix)
        => _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));

    public bool IsPainting { get; private set; }
    public PaintAxis Axis { get; private set; } = PaintAxis.None;

    /// <summary>이번 페인팅이 실제로 바꾼 칸 수.</summary>
    public int ChangedCount => _before.Count(p => _matrix.Get(p.Key) != p.Value);

    /// <summary>칸을 눌렀다 — 그 칸을 뒤집고 그 값을 붓으로 삼는다. 만질 수 없는 칸이면 시작하지 않는다.</summary>
    public bool Begin(PermissionCell cell)
    {
        if (IsPainting) return false;
        if (cell.Row < 0 || cell.Row >= _matrix.RowCount) return false;
        if (cell.Verb < 0 || cell.Verb >= PermissionPaintMath.VerbCount) return false;
        if (!_matrix.IsEnabled(cell)) return false;

        _before.Clear();
        _start = cell;
        _brush = !_matrix.Get(cell);
        Axis = PaintAxis.None;
        IsPainting = true;
        PaintTo(cell);
        return true;
    }

    /// <summary>쓸고 지나간다 — 축은 처음 정해진 뒤 바뀌지 않는다.</summary>
    public void MoveTo(PermissionCell cell)
    {
        if (!IsPainting) return;
        if (Axis == PaintAxis.None) Axis = PermissionPaintMath.AxisFor(_start, cell);
        PaintTo(cell);
    }

    /// <summary>놓았다.</summary>
    public int Finish()
    {
        var changed = ChangedCount;
        IsPainting = false;
        Axis = PaintAxis.None;
        _before.Clear();
        return changed;
    }

    /// <summary>Esc · 캡처 상실 — 칠하기 시작 전으로 되돌린다.</summary>
    public void Cancel()
    {
        if (!IsPainting)
        {
            _before.Clear();
            return;
        }
        foreach (var (cell, value) in _before) _matrix.Set(cell, value);
        IsPainting = false;
        Axis = PaintAxis.None;
        _before.Clear();
    }

    private void PaintTo(PermissionCell current)
    {
        foreach (var cell in PermissionPaintMath.Cells(Axis, _start, current, _matrix.RowCount))
        {
            if (!_matrix.IsEnabled(cell)) continue;
            if (!_before.ContainsKey(cell)) _before[cell] = _matrix.Get(cell);
            if (_matrix.Get(cell) != _brush) _matrix.Set(cell, _brush);
        }
    }
}
