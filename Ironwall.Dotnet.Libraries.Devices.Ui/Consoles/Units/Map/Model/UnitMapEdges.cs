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
/// 자식이 있고 그 자식이 모두 자식이 없으면 묶음. 척추는 부모 칸의 왼쪽 + 4 에 서되 가지가 <see cref="MAX_BRANCH"/> 를
/// 넘지 않게 묶음 쪽으로 당긴다(<see cref="SpineX"/>). <b>자식이 하나뿐인 묶음은 척추를 쓰지 않는다</b> — 그 자식은 부모 바로
/// 아래에 서므로 곧은 세로선이다(2026-09-28 1:1 갈고리 결함).</para>
/// <para>그 밖의 계층선은 <see cref="Route"/> — 부모 아래 가운데 → 가족 공통 버스 → 자식 위 가운데. 끌어 옮겨 자식이 부모 아래에
/// 있지 않으면 두 노드 사이 통로로 돈다(선이 노드를 가로지르지 않는다).</para>
/// <para>위치는 이미 Δ 가 입혀진 월드 좌표를 받는다 — 이 함수는 배치를 모른다.</para>
/// </remarks>
public static class UnitMapEdges
{
    /// <summary>척추가 칸 왼쪽에서 들어오는 거리(화면 DIU).</summary>
    public const double SPINE_INSET = 4.0;

    /// <summary>척추 → 노드 가지의 최대 길이(화면 DIU) — 넘으면 척추를 묶음 쪽으로 당긴다.</summary>
    public const double MAX_BRANCH = 24.0;

    /// <summary>돌아가는 선이 두 노드 "사이" 통로를 쓰려면 필요한 틈의 절반 — 틈이 이 두 배보다 좁으면 바깥으로 돈다.</summary>
    public const double LANE_HALF_GAP = 2.0;

    /// <summary>돌아가는 선이 노드에서 띄우는 거리.</summary>
    public const double DETOUR_CLEARANCE = 12.0;

    /// <summary>가운데가 이만큼 가까우면 곧은 세로선으로 긋는다(부동소수 · 반올림 흔들림).</summary>
    public const double STRAIGHT_EPSILON = 0.5;

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

            // 한 가족의 버스 높이 — 부모 아래와 "자동 배치의 자식 줄 위" 사이의 가운데. 형제마다 따로 재지 않는다:
            // L0 은 제대마다 닻이 달라(제대 건너뜀 형제) 형제별 가운데가 어긋나 버스가 겹줄로 번졌다.
            var childTop = 0.0;
            var childHalf = 0.0;
            foreach (var childId in parent.ChildIds)
            {
                if (tree.Find(childId) is not { } c) continue;
                var a = AnchorOf(level, c.Echelon);
                childTop = Math.Max(childTop, a.Top);
                childHalf = Math.Max(childHalf, a.Left);
            }
            var bus = bottom + (pp.Y + tierHeight * scale - childTop - bottom) / 2;

            // 세로 한 줄 묶음(끝 부대 자식이 둘 이상)만 척추를 쓴다. 자식이 하나면 부모 바로 아래라 곧은 세로선이다 —
            // 척추로 돌리면 "아래 → 왼쪽 → 아래 → 오른쪽" 갈고리가 되어 옆에서 붙은 것처럼 보였다(1:1 결함).
            var comb = parent.ChildIds.Count >= 2 && IsColumnParent(tree, parent);
            var spine = comb ? SpineX(pp.X, childHalf, scale, cellWidth) : double.NaN;

            foreach (var childId in parent.ChildIds)
            {
                var child = tree.Find(childId);
                if (child == null || !world.TryGetValue(childId, out var childWorld)) continue;

                var cp = Screen(childWorld);
                var ca = AnchorOf(level, child.Echelon);

                // 옮겨서(Δ) 척추 오른쪽 · 버스 아래를 벗어난 자식은 척추에 매달지 않는다 — 가지가 거꾸로 제 노드를 가로지른다.
                // 자동 배치에서는 늘 매단다: 가장 좁은 칸(L2 @0.72 · L0 @0.10)에서 가지가 2 · 0 이 되어도 거꾸로는 아니다.
                var points = comb && cp.X - ca.Left >= spine - STRAIGHT_EPSILON && cp.Y - ca.Top > bus && bus > bottom
                    ? new[]
                    {
                        new Point(pp.X, bottom),
                        new Point(pp.X, bus),
                        new Point(spine, bus),
                        new Point(spine, cp.Y),
                        new Point(cp.X - ca.Left, cp.Y),
                    }
                    : Route(pp, pa, cp, ca, bus);

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

    /// <summary>
    /// 척추 x — 칸 왼쪽 + 4(FR-24). 단 가지가 <see cref="MAX_BRANCH"/> 보다 길어지면 묶음 쪽으로 당긴다:
    /// 배율이 커질수록 칸이 넓어져 척추가 노드에서 멀리 떨어져 떠 보였다(배율 1.6 에서 가지 90).
    /// 칸 밖으로는 나가지 않는다(이웃 칸의 노드와 겹치지 않게).
    /// </summary>
    /// <param name="parentX">부모 중심 x(캔버스 좌표) — 묶음 자식도 같은 x 에 선다.</param>
    /// <param name="childHalf">묶음 자식 닻의 왼쪽 거리 중 큰 값.</param>
    public static double SpineX(double parentX, double childHalf, double scale, double cellWidth = 200.0)
        => Math.Max(parentX - cellWidth / 2 * scale + SPINE_INSET, parentX - childHalf - MAX_BRANCH);

    /// <summary>
    /// 계층 꺾은선 하나 — 부모 <b>아래 가운데</b>에서 나가 자식 <b>위 가운데</b>로 들어간다(직교 선분만).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>자식이 부모 아래에 있으면: 아래 → 버스 높이 → 옆 → 아래. 가운데가 같으면 <b>곧은 세로선 한 줄</b>(점 2개).
    /// 버스는 <paramref name="bus"/>(가족 공통)가 두 노드 사이 틈 안이면 그 높이, 아니면 틈의 가운데.</item>
    /// <item>자식이 부모 아래에 없으면(끌어 옮겨 위 · 옆으로 올라감): 부모 아래로 조금 나가 두 노드 <b>사이</b>(겹치면 바깥) 세로 통로로
    /// 돌아 자식 위로 들어간다 — 선이 두 노드를 가로지르지 않는다.</item>
    /// </list>
    /// 드래그 미리보기(상위 후보 → 끌리는 사본)도 같은 경로를 쓴다.
    /// </remarks>
    /// <param name="parent">부모 중심(캔버스 좌표).</param>
    /// <param name="parentAnchor">부모 닻.</param>
    /// <param name="child">자식 중심(캔버스 좌표).</param>
    /// <param name="childAnchor">자식 닻.</param>
    /// <param name="bus">버스 높이 희망값. 없으면 두 닻 사이 가운데.</param>
    public static IReadOnlyList<Point> Route(Point parent, UnitMapAnchor parentAnchor, Point child, UnitMapAnchor childAnchor, double? bus = null)
    {
        var bottom = parent.Y + parentAnchor.Bottom;
        var top = child.Y - childAnchor.Top;
        var gap = top - bottom;

        if (gap > 0)
        {
            if (Math.Abs(parent.X - child.X) < STRAIGHT_EPSILON)
                return new[] { new Point(parent.X, bottom), new Point(parent.X, top) };

            // 가족 버스가 틈 안이면 그대로(형제가 한 줄을 나눠 쓴다 — 멀리 아래로 옮긴 자식도 같은 버스에서 내려간다).
            // 틈 밖이면(자식을 버스보다 위로 올렸다) 틈의 가운데.
            var y = bus is double b && b > bottom && b < top ? b : bottom + gap / 2;
            return new[]
            {
                new Point(parent.X, bottom),
                new Point(parent.X, y),
                new Point(child.X, y),
                new Point(child.X, top),
            };
        }

        // 자식이 부모 아래에 없다 — 두 노드 사이(겹치면 자식 쪽 바깥)의 세로 통로로 돈다.
        double parentLeft = parent.X - parentAnchor.Left, parentRight = parent.X + parentAnchor.Left;
        double childLeft = child.X - childAnchor.Left, childRight = child.X + childAnchor.Left;
        double lane;
        if (childLeft - parentRight >= 2 * LANE_HALF_GAP) lane = (parentRight + childLeft) / 2;
        else if (parentLeft - childRight >= 2 * LANE_HALF_GAP) lane = (childRight + parentLeft) / 2;
        else lane = child.X >= parent.X ? Math.Max(parentRight, childRight) + DETOUR_CLEARANCE
                                        : Math.Min(parentLeft, childLeft) - DETOUR_CLEARANCE;

        var below = bottom + DETOUR_CLEARANCE;
        var above = top - DETOUR_CLEARANCE;
        return new[]
        {
            new Point(parent.X, bottom),
            new Point(parent.X, below),
            new Point(lane, below),
            new Point(lane, above),
            new Point(child.X, above),
            new Point(child.X, top),
        };
    }
}
