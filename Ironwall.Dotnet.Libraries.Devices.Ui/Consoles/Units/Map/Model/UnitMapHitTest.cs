using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 부대 관계도 히트 판정 — 균등 격자 색인 · 월드 점 → 노드 id (FR-13 · FR-29)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>히트 색인에 넣을 노드 하나 — Δ 가 입혀진 월드 중심과 제대(L0 틀 크기).</summary>
public readonly record struct UnitMapHitItem(int UnitId, Point World, EnumUnitEchelon? Echelon);

/// <summary>
/// 월드 점 → 그 아래 노드. 누른 곳(제스처 — FR-13)과 놓은 곳(드롭 판정 — FR-29, <b>포인터 아래</b>)을 가른다.
/// </summary>
/// <remarks>
/// <para><b>시각 트리를 걷지 않는다</b> — 노드 200개 · 이동마다 부르는 판정이라 <c>VisualTreeHelper.HitTest</c> 대신
/// 균등 격자(<see cref="CellSize"/> 월드) 색인으로 찾는다. 셸 오버레이(<c>PART_OverlayBox</c>)를 집는 함정과도 무관하다.</para>
/// <para><b>도형은 화면에서 고정 크기</b>(FR-21)라 판정은 화면 DIU 로 한다 — 월드 차이 × 배율. 그래서 색인은
/// 단계 · 배율마다 새로 만든다(줌이 끝날 때 한 번 — 끄는 동안에는 배율이 바뀌지 않는다).</para>
/// <para>겹치면 <b>나중에 그린 것</b>이 위다(사용자가 옮겨 겹친 노드 — FR-21). 끄는 동안에는 끌리는 부대와 그 예하를
/// <see cref="DragExclusion"/> 으로 빼서 넘긴다 — 자기 자리 위에 놓으면 '빈 곳'(위치)이 된다(시나리오 ISSUE-14).</para>
/// <para>L0 틀 크기표는 부대 기호(<c>UnitSymbolGeometry.FrameSize</c> — 레인 C)와 같은 PRD §3.3 값이다.
/// 히트는 그 둘레에 <see cref="L0HitPadding"/> 을 더한다(8×6 은 겨누기 어렵다 — SB <c>box()</c>).</para>
/// <para><b>스레드</b>: 만든 뒤 읽기 전용 — UI 스레드에서 만들고 쓴다.</para>
/// </remarks>
public sealed class UnitMapHitTest
{
    /// <summary>격자 한 칸(월드 단위).</summary>
    public const double CellSize = 256;

    /// <summary>L0 틀 둘레에 더하는 히트 여유(화면 DIU).</summary>
    public const double L0HitPadding = 3;

    // L1 모양(화면 DIU, 노드 중심 기준) — 틀 30×20 · 표지 7 · 이름 줄(PRD §3.3 "세로 범위 −19 ~ +26").
    private const double L1FrameHalfWidth = 22;     // 틀 ±15 + 모서리 괄호 · ★ · ▲ 자리
    private const double L1Top = -19;
    private const double L1NameTop = 12;
    private const double L1Bottom = 26;
    private const double L1NameInset = 8;           // 이름 폭 = 칸 × 배율 − 8 (FR-20)

    // L2 카드 132×56.
    private const double L2HalfWidth = 66;
    private const double L2HalfHeight = 28;

    // 월드 ↔ 화면 왕복(÷배율 × 배율)의 반올림 오차 — 가장자리 점이 1e-14 차이로 빠지지 않게.
    private const double EdgeTolerance = 1e-9;

    private readonly UnitMapHitItem[] _items;
    private readonly Dictionary<(int X, int Y), List<int>> _cells;

    private UnitMapHitTest(UnitMapHitItem[] items, Dictionary<(int, int), List<int>> cells, UnitMapLevel level, double scale)
    {
        _items = items;
        _cells = cells;
        Level = level;
        Scale = scale;
    }

    /// <summary>색인을 만든 단계.</summary>
    public UnitMapLevel Level { get; }

    /// <summary>색인을 만든 배율.</summary>
    public double Scale { get; }

    /// <summary>색인에 든 노드 수.</summary>
    public int Count => _items.Length;

    /// <summary>
    /// 색인을 만든다. <paramref name="itemsInDrawOrder"/> 는 <b>그리는 순서</b>(앞 = 아래)여야 한다.
    /// </summary>
    public static UnitMapHitTest Build(IEnumerable<UnitMapHitItem> itemsInDrawOrder, UnitMapLevel level, double scale)
    {
        ArgumentNullException.ThrowIfNull(itemsInDrawOrder);
        if (!(scale > 0) || double.IsInfinity(scale)) throw new ArgumentOutOfRangeException(nameof(scale), scale, "배율은 양수여야 합니다.");

        var items = new List<UnitMapHitItem>(itemsInDrawOrder).ToArray();
        var cells = new Dictionary<(int, int), List<int>>();

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (!double.IsFinite(item.World.X) || !double.IsFinite(item.World.Y)) continue;

            var box = NodeBounds(level, item.Echelon, scale);
            var (x0, y0) = CellOf(new Point(item.World.X + box.Left / scale, item.World.Y + box.Top / scale));
            var (x1, y1) = CellOf(new Point(item.World.X + box.Right / scale, item.World.Y + box.Bottom / scale));
            for (var cx = x0; cx <= x1; cx++)
                for (var cy = y0; cy <= y1; cy++)
                {
                    if (!cells.TryGetValue((cx, cy), out var list)) cells[(cx, cy)] = list = new List<int>(4);
                    list.Add(index);
                }
        }
        return new UnitMapHitTest(items, cells, level, scale);
    }

    /// <summary>
    /// <paramref name="world"/> 아래 노드 id. 없으면 <c>null</c>(빈 곳). <paramref name="exclude"/> 의 id 는 없는 것으로 본다.
    /// </summary>
    public int? HitTest(Point world, IReadOnlySet<int>? exclude = null)
    {
        if (!double.IsFinite(world.X) || !double.IsFinite(world.Y)) return null;
        if (!_cells.TryGetValue(CellOf(world), out var candidates)) return null;

        // 색인은 그리는 순서로 쌓였다 — 뒤에서부터 보면 처음 맞는 것이 맨 위다.
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var item = _items[candidates[i]];
            if (exclude != null && exclude.Contains(item.UnitId)) continue;

            var offset = new Vector((world.X - item.World.X) * Scale, (world.Y - item.World.Y) * Scale);
            if (Contains(Level, item.Echelon, Scale, offset)) return item.UnitId;
        }
        return null;
    }

    /// <summary>
    /// 노드 모양을 감싸는 <b>화면</b> 사각형(DIU, 노드 중심 기준) — 색인 칸을 고를 때와 전체 보기 여백(<c>GraphViewport.Fit</c>)에 쓴다.
    /// </summary>
    public static Rect NodeBounds(UnitMapLevel level, EnumUnitEchelon? echelon, double scale)
    {
        switch (level)
        {
            case UnitMapLevel.L2:
                return new Rect(-L2HalfWidth, -L2HalfHeight, L2HalfWidth * 2, L2HalfHeight * 2);
            case UnitMapLevel.L1:
                var half = Math.Max(L1FrameHalfWidth, L1NameHalfWidth(scale));
                return new Rect(-half, L1Top, half * 2, L1Bottom - L1Top);
            default:
                var frame = L0FrameSize(echelon);
                var halfW = frame.Width / 2 + L0HitPadding;
                var halfH = frame.Height / 2 + L0HitPadding;
                return new Rect(-halfW, -halfH, halfW * 2, halfH * 2);
        }
    }

    /// <summary>
    /// 노드 중심에서 <paramref name="screenOffset"/>(화면 DIU) 떨어진 점이 노드 모양 안인가. 가장자리는 안이다.
    /// L1 은 틀 칸(−19 ~ +12, ±22)과 이름 줄(+12 ~ +26, ± 이름 폭/2)의 합집합이다 — 이름 줄 폭만큼 틀 옆 빈 곳을 먹지 않게.
    /// </summary>
    public static bool Contains(UnitMapLevel level, EnumUnitEchelon? echelon, double scale, Vector screenOffset)
    {
        var dx = Math.Max(0, Math.Abs(screenOffset.X) - EdgeTolerance);
        var dy = screenOffset.Y;
        var ady = Math.Max(0, Math.Abs(dy) - EdgeTolerance);
        switch (level)
        {
            case UnitMapLevel.L2:
                return dx <= L2HalfWidth && ady <= L2HalfHeight;
            case UnitMapLevel.L1:
                if (dy < L1Top - EdgeTolerance || dy > L1Bottom + EdgeTolerance) return false;
                if (dx <= L1FrameHalfWidth) return true;
                return dy >= L1NameTop - EdgeTolerance && dx <= L1NameHalfWidth(scale);
            default:
                var bounds = NodeBounds(UnitMapLevel.L0, echelon, scale);
                return dx <= bounds.Right && ady <= bounds.Bottom;
        }
    }

    /// <summary>L0 틀 크기(화면 DIU) — 크기가 제대를 말한다(PRD §3.3). 모르는 제대는 중대 크기.</summary>
    public static Size L0FrameSize(EnumUnitEchelon? echelon) => echelon switch
    {
        EnumUnitEchelon.Division => new Size(20, 13),
        EnumUnitEchelon.Regiment => new Size(17, 11),
        EnumUnitEchelon.Battalion => new Size(14, 9),
        EnumUnitEchelon.Outpost => new Size(8, 6),
        _ => new Size(12, 8),
    };

    /// <summary>끄는 동안 판정에서 뺄 부대 — 끌리는 부대와 그 예하 전부.</summary>
    public static IReadOnlySet<int> DragExclusion(UnitTreeModel tree, int draggedUnitId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var set = new HashSet<int> { draggedUnitId };
        foreach (var id in tree.DescendantIds(draggedUnitId)) set.Add(id);
        return set;
    }

    private static double L1NameHalfWidth(double scale) => (UnitMapLayout.SlotWidth * scale - L1NameInset) / 2;

    private static (int X, int Y) CellOf(Point world)
        => ((int)Math.Floor(world.X / CellSize), (int)Math.Floor(world.Y / CellSize));
}
