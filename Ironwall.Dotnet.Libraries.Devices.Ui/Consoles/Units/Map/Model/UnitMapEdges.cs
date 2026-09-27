using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 부대 관계도의 선 기하 — 계층 꺾은선 · 척추 · 인접 호 (FR-24 · FR-25 · FR-27)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

public enum UnitMapEdgeKind
{
    /// <summary>계층선 — 꺾은 실선 1.5 <c>TextMutedBrush</c>.</summary>
    Hierarchy = 0,

    /// <summary>인접선 — 둥근 점선 2.2 <c>StatusInfoBrush</c>.</summary>
    Adjacency = 1,
}

/// <summary>
/// 선 하나. 좌표는 <b>캔버스 좌표</b>(월드 × 배율) — 팬은 선 층을 담은 컨테이너의 <c>TranslateTransform</c> 이 더한다(D-2).
/// </summary>
/// <param name="Kind">계층 · 인접.</param>
/// <param name="FromId">계층이면 부모, 인접이면 작은 id.</param>
/// <param name="ToId">계층이면 자식, 인접이면 큰 id.</param>
/// <param name="Points">꺾은선이면 꼭짓점들, 곡선이면 [시작, 조절점, 끝](2차 베지어).</param>
/// <param name="IsCurve">2차 베지어인가.</param>
/// <param name="IsEmphasized">선택 부대에 닿는다 — 굵기 +1(색 불변, FR-27).</param>
public sealed record UnitMapEdge(UnitMapEdgeKind Kind, int FromId, int ToId, IReadOnlyList<Point> Points, bool IsCurve, bool IsEmphasized);

/// <summary>노드 중심에서 선이 붙는 자리까지의 거리(화면 DIU — 단계마다 고정, 배율과 무관).</summary>
public readonly record struct UnitMapAnchor(double Top, double Bottom, double Left);

/// <summary>
/// 관계도의 선 — 순수 함수(NFR-11). 캔버스의 선 층(<c>DrawingVisual</c> 하나)이 이 결과를 그대로 그린다.
/// </summary>
/// <remarks>
/// <para>세로 한 줄 묶음(자식이 모두 끝 부대)의 판정은 자동 배치(<c>UnitMapLayout</c> — FR-06)와 같은 규칙이다:
/// 자식이 있고 그 자식이 모두 자식이 없으면 묶음. 척추는 부모 칸의 왼쪽 + 4 에 선다.</para>
/// <para>위치는 이미 Δ 가 입혀진 월드 좌표를 받는다 — 이 함수는 배치를 모른다.</para>
/// </remarks>
public static class UnitMapEdges
{
    /// <summary>척추가 칸 왼쪽에서 들어오는 거리(화면 DIU).</summary>
    public const double SPINE_INSET = 4.0;

    /// <summary>같은 줄 인접 호가 닻 위로 띄우는 틈.</summary>
    public const double ARC_LIFT = 2.0;

    /// <summary>인접 호의 휨 — 거리 × 0.18 을 12 ~ 38 로 자른다.</summary>
    public const double BOW_RATIO = 0.18;
    public const double BOW_MIN = 12.0;
    public const double BOW_MAX = 38.0;

    /// <summary>같은 줄로 보는 y 차(캔버스 좌표).</summary>
    private const double SAME_ROW_EPSILON = 1.0;

    /// <summary>그 단계 · 제대의 선 닻(SB <c>anc()</c>).</summary>
    public static UnitMapAnchor AnchorOf(UnitMapLevel level, EnumUnitEchelon? echelon)
    {
        switch (level)
        {
            case UnitMapLevel.L2:
                return new UnitMapAnchor(28, 28, 66);
            case UnitMapLevel.L1:
                return new UnitMapAnchor(21, 28, 15);        // 위: 표지 위 · 아래: 짧은 이름 밑
            default:
                var frame = UnitSymbolGeometry.FrameSize(UnitMapLevel.L0, echelon);
                return new UnitMapAnchor(frame.Height / 2 + 1, frame.Height / 2 + 1, frame.Width / 2);
        }
    }

    /// <summary>인접 호의 휨(DIU).</summary>
    public static double AdjacencyBow(double distance)
        => Math.Max(BOW_MIN, Math.Min(BOW_MAX, Math.Abs(distance) * BOW_RATIO));

    /// <summary>
    /// 선 전부를 만든다 — 계층선(부모 → 자식) 먼저, 인접선(한 쌍 1회, <c>[low, high]</c>) 뒤.
    /// </summary>
    /// <param name="tree">편제.</param>
    /// <param name="world">Δ 가 입혀진 월드 위치. 위치가 없는 부대에 닿는 선은 그리지 않는다.</param>
    /// <param name="scale">배율.</param>
    /// <param name="level">단계(닻 크기).</param>
    /// <param name="selectedId">선택 부대 — 닿는 선을 강조.</param>
    /// <param name="cellWidth">자동 배치의 칸 폭(월드). 척추 위치에 쓴다.</param>
    /// <param name="tierHeight">자동 배치의 층 간격(월드). 묶음 첫 자식의 위를 정한다.</param>
    public static IReadOnlyList<UnitMapEdge> Build(
        UnitTreeModel tree,
        IReadOnlyDictionary<int, Point> world,
        double scale,
        UnitMapLevel level,
        int? selectedId = null,
        double cellWidth = 200.0,
        double tierHeight = 160.0)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(world);

        var edges = new List<UnitMapEdge>();
        if (tree.Count == 0) return edges;

        Point Screen(Point w) => new(w.X * scale, w.Y * scale);
        bool Touches(int a, int b) => selectedId is int s && (a == s || b == s);

        // ① 계층선 — 트리 순서(깊이 우선 · 코드 순)라 결정적이다.
        foreach (var parent in tree.Ordered)
        {
            if (parent.ChildIds.Count == 0 || !world.TryGetValue(parent.Id, out var parentWorld)) continue;

            var pp = Screen(parentWorld);
            var pa = AnchorOf(level, parent.Echelon);
            var bottom = pp.Y + pa.Bottom;
            var column = IsColumnParent(tree, parent);

            foreach (var childId in parent.ChildIds)
            {
                var child = tree.Find(childId);
                if (child == null || !world.TryGetValue(childId, out var childWorld)) continue;

                var cp = Screen(childWorld);
                var ca = AnchorOf(level, child.Echelon);
                IReadOnlyList<Point> points;

                if (column)
                {
                    // 척추 = 부모 칸 왼쪽 + 4. 가운데 높이는 부모 아래와 "자동 배치의 첫 자식 위" 사이.
                    var spine = pp.X - cellWidth / 2 * scale + SPINE_INSET;
                    var firstTop = pp.Y + tierHeight * scale - ca.Top;
                    var middle = bottom + (firstTop - bottom) / 2;
                    points = new[]
                    {
                        new Point(pp.X, bottom),
                        new Point(pp.X, middle),
                        new Point(spine, middle),
                        new Point(spine, cp.Y),
                        new Point(cp.X - ca.Left, cp.Y),
                    };
                }
                else
                {
                    var top = cp.Y - ca.Top;
                    var middle = (bottom + top) / 2;
                    points = new[]
                    {
                        new Point(pp.X, bottom),
                        new Point(pp.X, middle),
                        new Point(cp.X, middle),
                        new Point(cp.X, top),
                    };
                }

                edges.Add(new UnitMapEdge(UnitMapEdgeKind.Hierarchy, parent.Id, childId, points, false, Touches(parent.Id, childId)));
            }
        }

        // ② 인접선 — 무방향이라 작은 id 쪽에서만 낸다.
        foreach (var node in tree.Ordered.OrderBy(n => n.Id))
        {
            if (!world.TryGetValue(node.Id, out var aWorld)) continue;

            foreach (var otherId in node.AdjacentIds)
            {
                if (otherId <= node.Id || !world.TryGetValue(otherId, out var bWorld)) continue;
                var other = tree.Find(otherId);
                if (other == null) continue;

                var a = Screen(aWorld);
                var b = Screen(bWorld);
                Point start, control, end;

                if (Math.Abs(a.Y - b.Y) < SAME_ROW_EPSILON)
                {
                    // 같은 줄 — 두 노드 위로 뜬 호(닻 위 + 2), 휨은 거리 비례.
                    var y0 = a.Y - AnchorOf(level, node.Echelon).Top - ARC_LIFT;
                    var bow = AdjacencyBow(b.X - a.X);
                    start = new Point(a.X, y0);
                    end = new Point(b.X, y0);
                    control = new Point((a.X + b.X) / 2, y0 - bow);
                }
                else
                {
                    // 다른 줄 — 중심끼리, 조절점은 수직 이등분선 위(위쪽으로 휜다). 노드가 선 위에 그려져 끝을 덮는다.
                    start = a;
                    end = b;
                    var chord = b - a;
                    var normal = new Vector(-chord.Y, chord.X);
                    normal.Normalize();
                    if (normal.Y > 0) normal = -normal;
                    var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                    control = mid + normal * AdjacencyBow(chord.Length);
                }

                edges.Add(new UnitMapEdge(UnitMapEdgeKind.Adjacency, node.Id, otherId, new[] { start, control, end }, true, Touches(node.Id, otherId)));
            }
        }

        return edges;
    }

    /// <summary>자식이 있고 모두 끝 부대인가 — 세로 한 줄 묶음(FR-06).</summary>
    public static bool IsColumnParent(UnitTreeModel tree, UnitTreeNode node)
        => node.ChildIds.Count > 0 && node.ChildIds.All(id => tree.Find(id) is { HasChildren: false });
}
