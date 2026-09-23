using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/// <summary>
/// 드래그 판정의 순수 함수 — 데드존 · 삽입 인덱스 · 재배열 · 오토스크롤. WPF 시각 트리에 기대지 않는다
/// (<see cref="Rect"/> 은 값 형식일 뿐이다).
/// </summary>
/// <remarks>
/// UIA 에 드래그 패턴이 없어(.NET 8 WPF) 드래그는 자동화로 단언할 수 없다. 이 함수들의 헤드리스 테스트와
/// 키보드 폴백 경로가 회귀망의 전부다 — 그래서 판정은 Behavior 안에 두지 않고 여기로 뺀다.
/// </remarks>
public static class DragMath
{
    /// <summary>데드존(DIU). 이보다 덜 움직였으면 드래그가 아니라 클릭이다. 새 상수를 만들지 않는다 — 전 앱이 8.0 이다.</summary>
    public const double DeadZone = 8.0;

    /// <summary>경계 오토스크롤이 시작되는 띠의 두께(DIU).</summary>
    public const double AutoScrollEdge = 32.0;

    /// <summary>경계 오토스크롤 최대 속도(px/sec). 프레임당 값이 아니다.</summary>
    public const double AutoScrollMaxSpeed = 480.0;

    /// <summary>눌린 자리에서 (<paramref name="dx"/>, <paramref name="dy"/>) 만큼 움직였을 때 드래그로 볼 것인가. 제곱 비교.</summary>
    public static bool IsDrag(double dx, double dy, double threshold = DeadZone)
        => dx * dx + dy * dy > threshold * threshold;

    /// <summary>
    /// 포인터 y 가 가리키는 삽입 인덱스(0..Count). 각 행의 <b>중점</b>보다 위면 그 행 앞, 아니면 뒤.
    /// </summary>
    /// <param name="rowRects">화면에 실체화된 행들의 실측 rect(위→아래). 균일 높이를 가정하지 않는다.</param>
    /// <param name="y">rect 와 같은 좌표계의 포인터 y.</param>
    public static int InsertionIndex(IReadOnlyList<Rect> rowRects, double y)
    {
        for (var i = 0; i < rowRects.Count; i++)
        {
            var r = rowRects[i];
            if (y < r.Top + r.Height / 2) return i;
        }
        return rowRects.Count;
    }

    /// <summary>삽입선을 그릴 y — 삽입 인덱스의 행 위쪽 변, 맨 끝이면 마지막 행 아래쪽 변.</summary>
    public static double InsertionLineY(IReadOnlyList<Rect> rowRects, int insertionIndex)
    {
        if (rowRects.Count == 0) return 0;
        if (insertionIndex <= 0) return rowRects[0].Top;
        if (insertionIndex >= rowRects.Count) return rowRects[^1].Bottom;
        return rowRects[insertionIndex].Top;
    }

    /// <summary><paramref name="from"/> 행을 삽입 인덱스로 옮기면 순서가 실제로 바뀌는가(제자리 · 바로 아래는 아니다).</summary>
    public static bool IsRealMove(int from, int insertionIndex)
        => insertionIndex != from && insertionIndex != from + 1;

    /// <summary>
    /// <paramref name="from"/> 의 항목을 <paramref name="insertionIndex"/>(옮기기 <b>전</b> 목록 기준 0..Count) 자리로 옮긴다.
    /// </summary>
    public static void Move<T>(IList<T> list, int from, int insertionIndex)
    {
        if (from < 0 || from >= list.Count) throw new ArgumentOutOfRangeException(nameof(from));
        if (insertionIndex < 0 || insertionIndex > list.Count) throw new ArgumentOutOfRangeException(nameof(insertionIndex));
        if (!IsRealMove(from, insertionIndex)) return;

        var item = list[from];
        list.RemoveAt(from);
        list.Insert(insertionIndex > from ? insertionIndex - 1 : insertionIndex, item);
    }

    /// <summary>
    /// 여러 항목을 한꺼번에 옮긴다. 옮겨진 항목들은 고른 순서가 아니라 <b>원래 목록에서의 순서</b>를 지킨다.
    /// </summary>
    public static void MoveMany<T>(IList<T> list, IEnumerable<int> fromIndexes, int insertionIndex)
    {
        if (insertionIndex < 0 || insertionIndex > list.Count) throw new ArgumentOutOfRangeException(nameof(insertionIndex));

        var picked = fromIndexes.Distinct().OrderBy(i => i).ToList();
        if (picked.Count == 0) return;
        if (picked[0] < 0 || picked[^1] >= list.Count) throw new ArgumentOutOfRangeException(nameof(fromIndexes));

        var items = picked.Select(i => list[i]).ToList();
        var target = insertionIndex - picked.Count(i => i < insertionIndex);

        for (var k = picked.Count - 1; k >= 0; k--) list.RemoveAt(picked[k]);
        for (var k = 0; k < items.Count; k++) list.Insert(target + k, items[k]);
    }

    /// <summary>키보드 폴백(Alt+↑↓) — 한 칸 옮긴 뒤의 인덱스. 끝에서는 그대로.</summary>
    public static int StepIndex(int index, int direction, int count)
        => Math.Max(0, Math.Min(count - 1, index + Math.Sign(direction)));

    /// <summary>
    /// 경계 오토스크롤 속도(px/sec). 위쪽 띠면 음수, 아래쪽 띠면 양수, 가운데면 0. 띠 안으로 깊이 들어갈수록 빨라진다.
    /// </summary>
    public static double AutoScrollVelocity(double y, double viewportLength, double edge = AutoScrollEdge, double maxSpeed = AutoScrollMaxSpeed)
    {
        if (viewportLength < edge * 2) return 0;

        if (y < edge) return -maxSpeed * Math.Min(1, (edge - y) / edge);
        var lower = viewportLength - edge;
        if (y > lower) return maxSpeed * Math.Min(1, (y - lower) / edge);
        return 0;
    }

    /// <summary><paramref name="elapsed"/> 동안 스크롤할 양(px). 시간 기반이라 프레임레이트에 기대지 않는다.</summary>
    public static double AutoScrollDelta(double y, double viewportLength, TimeSpan elapsed)
        => AutoScrollVelocity(y, viewportLength) * elapsed.TotalSeconds;

    /// <summary>
    /// 드래그 고스트 툴팁의 사각형 — 커서에서 <paramref name="offsetX"/>·<paramref name="offsetY"/> 만큼 떨어져
    /// 뜨되, <paramref name="bounds"/>(꾸미는 표면의 가용 폭 · 높이, 원점은 커서 좌표계와 같다) 밖으로 넘치면
    /// 반대편(커서 왼쪽 · 위쪽)으로 뒤집는다. 뒤집어도 넘치면(라벨이 표면보다 큰 구석) 안쪽으로 눌러 붙인다.
    /// </summary>
    /// <remarks><paramref name="bounds"/> 의 각 변이 <see cref="double.IsInfinity(double)"/> 이면 그 축은 뒤집지도 누르지도 않는다
    /// — 가용 폭을 모를 때(레이어를 못 찾았을 때)는 예전처럼 커서 오른쪽 아래에 그냥 띄운다.</remarks>
    public static Rect ClampGhostRect(Point cursor, Size size, Size bounds, double offsetX = 12, double offsetY = 10)
        => new(ClampAxis(cursor.X, size.Width, offsetX, bounds.Width), ClampAxis(cursor.Y, size.Height, offsetY, bounds.Height), size.Width, size.Height);

    private static double ClampAxis(double cursor, double extent, double offset, double boundExtent)
    {
        if (double.IsInfinity(boundExtent)) return cursor + offset;

        var preferred = cursor + offset + extent <= boundExtent ? cursor + offset : cursor - offset - extent;
        return Math.Max(0, Math.Min(boundExtent - extent, preferred));
    }
}
