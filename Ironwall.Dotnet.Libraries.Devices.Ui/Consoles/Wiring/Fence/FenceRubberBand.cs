using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 선택 사각형(러버밴드) 적중 — <b>순수 함수</b>(fence-wiring-editor FR-04 · FR-05 · NFR-01). 사각형이 <b>걸친</b>(닿은) 것을 모두 고른다.
/// 좌표계는 부르는 쪽이 맞춘다(세계 좌표끼리).
/// </summary>
public static class FenceRubberBand
{
    /// <summary>두 점(누른 곳 · 지금)이 만드는 사각형 — 어느 방향으로 끌어도 같다.</summary>
    public static Rect FromPoints(Point a, Point b)
        => new(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));

    /// <summary>사각형에 걸친 것의 키 — 입력 차례를 지킨다. 빈 사각형(넓이 0)도 선 위에 걸친 것은 고른다.</summary>
    public static IReadOnlyList<int> Hits(IEnumerable<(int Key, Rect Bounds)> items, Rect band)
        => (items ?? Enumerable.Empty<(int, Rect)>())
            .Where(t => !t.Bounds.IsEmpty && Touches(t.Bounds, band))
            .Select(t => t.Key)
            .Distinct()
            .ToList();

    /// <summary>
    /// 선택 결과 — 더하기(Ctrl)면 원래 선택 + 적중(원래 차례 뒤에 새 것), 아니면 적중만.
    /// </summary>
    public static IReadOnlyList<int> Merge(IReadOnlyList<int> current, IReadOnlyList<int> hits, bool additive)
        => additive ? (current ?? Array.Empty<int>()).Concat(hits ?? Array.Empty<int>()).Distinct().ToList() : (hits ?? Array.Empty<int>()).ToList();

    private static bool Touches(Rect a, Rect b)
        => a.Left <= b.Right && b.Left <= a.Right && a.Top <= b.Bottom && b.Top <= a.Bottom;
}
