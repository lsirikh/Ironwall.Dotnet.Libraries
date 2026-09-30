namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 타일 끌어 순서 바꾸기 판정(순수, drag-first-ux 규칙). UI 없이 시험한다.
/// 삽입 위치(insert index)는 "원래 목록 기준으로 이 칸 앞" — 0..count.
/// </summary>
internal static class TileReorder
{
    /// <summary>
    /// 사내 공통 데드존(DIU) — <c>Utils\Behaviors\Drag\DragMath.DeadZone</c> 와 같은 값 · 같은 식.
    /// 호스트 exe 는 Utils(Caliburn · MDIX 의존)를 참조하지 않으므로 값을 그대로 옮긴다(새 값 아님).
    /// </summary>
    public const double DeadZone = 8.0;

    /// <summary>눌린 점에서 데드존을 넘었는가(제곱 비교, 데드존 미만 = 클릭).</summary>
    public static bool IsDrag(double dx, double dy) => dx * dx + dy * dy > DeadZone * DeadZone;

    /// <summary>놓을 칸 · 그 칸의 왼쪽 절반인지로 삽입 위치를 구한다. 칸이 없으면 -1.</summary>
    public static int InsertIndex(int targetIndex, bool beforeTarget, int count)
    {
        if (targetIndex < 0 || targetIndex >= count) return -1;
        return beforeTarget ? targetIndex : targetIndex + 1;
    }

    /// <summary>삽입 위치를 옮긴 뒤의 최종 인덱스로. 제자리면 <paramref name="from"/> 그대로.</summary>
    public static int FinalIndex(int from, int insertIndex)
        => insertIndex > from ? insertIndex - 1 : insertIndex;

    /// <summary>제자리가 아닌 실제 이동인가.</summary>
    public static bool IsMove(int from, int insertIndex, int count)
        => from >= 0 && from < count && insertIndex >= 0 && insertIndex <= count && FinalIndex(from, insertIndex) != from;

    /// <summary>목록에서 from 을 빼 삽입 위치에 넣은 새 순서(원본 불변).</summary>
    public static IReadOnlyList<T> Move<T>(IReadOnlyList<T> items, int from, int insertIndex)
    {
        if (!IsMove(from, insertIndex, items.Count)) return items.ToArray();
        var list = items.ToList();
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(FinalIndex(from, insertIndex), item);
        return list;
    }

    /// <summary>키보드 폴백(Alt+←/→): 한 칸 이동의 삽입 위치. 범위 밖이면 -1.</summary>
    public static int KeyboardInsertIndex(int from, int delta, int count)
    {
        int to = from + delta;
        if (from < 0 || from >= count || to < 0 || to >= count || delta == 0) return -1;
        return delta > 0 ? to + 1 : to;
    }
}
