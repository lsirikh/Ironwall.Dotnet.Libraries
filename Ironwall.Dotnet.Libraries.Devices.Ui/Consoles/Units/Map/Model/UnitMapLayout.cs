using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 부대 관계도 자동 배치 + 사용자 Δ — 화면 없이 도는 순수 함수 (FR-06 · FR-07 · FR-21)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 공유 배치의 지금 상태 — 배치 상태 문구(FR-11 · 시나리오 ISSUE-7)와 위치 드롭 판정(FR-29 · ISSUE-17)이 함께 쓴다.
/// </summary>
/// <remarks>지원 판정(FR-50) 결과를 이 값으로 옮기는 것은 배치 동기화(<c>UnitMapLayoutSync</c> — 레인 B)의 몫이다.</remarks>
public enum UnitMapLayoutState
{
    /// <summary>창을 열고 <c>GET /api/units/layout</c> 응답을 기다린다 — 자동 배치로 먼저 그린다(NFR-01). 위치 쓰기는 응답 뒤.</summary>
    Loading = 0,

    /// <summary>서버 공유 문서(200) — 모든 운영자가 같은 그림. 위치 쓰기는 <c>units:edit</c>.</summary>
    Shared = 1,

    /// <summary>서버가 배치를 지원하지 않는다(404 · <c>ENDPOINT_REMOVED</c>) — 메모리에서만(FR-51). 위치 쓰기는 <c>units:view</c>.</summary>
    SessionOnly = 2,

    /// <summary>읽기 실패(타임아웃 · 5xx · 403) — 자동 배치로 그리고 쓰기 금지, [다시 시도].</summary>
    ReadFailed = 3,

    /// <summary>문서의 <c>layout_version</c> 이 이 클라의 판과 다르다 — Δ 미적용 · 쓰기 금지(FR-07).</summary>
    VersionMismatch = 4,
}

/// <summary>노드 하나가 차지한 칸(월드 단위).</summary>
/// <param name="Left">칸 왼쪽 x. 세로 줄 자식은 부모 칸을 그대로 쓴다(선 층이 척추 x = <c>Left + 4</c> 를 여기서 얻는다).</param>
/// <param name="Span">칸 수 — 끝 부대 · 세로 줄 부모는 1, 그 밖은 자식 칸의 합.</param>
/// <param name="IsColumnChild">부모 아래 <b>세로 한 줄</b>로 쌓인 끝 부대인가.</param>
public readonly record struct UnitMapSlot(double Left, int Span, bool IsColumnChild);

/// <summary>
/// 자동 배치 + Δ 의 결과 — 모두 <b>월드 단위</b>(배율 100% 의 DIU)이고 노드 <b>중심</b> 좌표다.
/// </summary>
public sealed class UnitMapLayoutResult
{
    internal UnitMapLayoutResult(
        IReadOnlyDictionary<int, Point> autoPositions,
        IReadOnlyDictionary<int, Point> positions,
        IReadOnlyDictionary<int, UnitMapSlot> slots,
        int slotCount,
        Rect bounds,
        bool isVersionMismatch)
    {
        AutoPositions = autoPositions;
        Positions = positions;
        Slots = slots;
        SlotCount = slotCount;
        Bounds = bounds;
        IsVersionMismatch = isVersionMismatch;
    }

    public static UnitMapLayoutResult Empty { get; } = new(
        new Dictionary<int, Point>(), new Dictionary<int, Point>(), new Dictionary<int, UnitMapSlot>(), 0, Rect.Empty, false);

    /// <summary>Δ 를 입히기 전 자동 위치(Δ 의 기준 — 끌기 확정 때 새 Δ = 놓은 자리 − 이 값 − 조상 Δ 합).</summary>
    public IReadOnlyDictionary<int, Point> AutoPositions { get; }

    /// <summary>그릴 위치 — 자동 위치 + 조상 Δ 합 + 자기 Δ(FR-07). 판이 다르면 자동 위치 그대로.</summary>
    public IReadOnlyDictionary<int, Point> Positions { get; }

    /// <summary>노드마다 차지한 칸(자동 배치 기준 — Δ 와 무관).</summary>
    public IReadOnlyDictionary<int, UnitMapSlot> Slots { get; }

    /// <summary>전체 칸 수 — 월드 폭 = 칸 수 × <see cref="UnitMapLayout.SlotWidth"/>.</summary>
    public int SlotCount { get; }

    /// <summary><see cref="Positions"/> 의 점 경계(노드 도형 크기는 뺐다 — 도형은 화면에서 고정이라 배율과 따로 더한다). 노드가 없으면 <see cref="Rect.Empty"/>.</summary>
    public Rect Bounds { get; }

    /// <summary>배치 문서의 <c>layout_version</c> 이 이 알고리즘의 판과 다르다 — Δ 를 입히지 않았고 쓰기도 막아야 한다(FR-07 · D-6).</summary>
    public bool IsVersionMismatch { get; }
}

/// <summary>
/// 부대 관계도의 <b>자동 배치</b>(위 → 아래)와 사용자 배치 Δ 적용.
/// </summary>
/// <remarks>
/// <para><b>칸</b>: 한 부대가 차지하는 칸 수 = 끝 부대이거나 <b>자식이 모두 끝 부대면 1칸</b>(자식은 부모 아래 세로 한 줄,
/// 간격 <see cref="ColumnPitch"/>), 아니면 자식 칸의 합. 부모는 자식 칸의 가운데. 층 간격 <see cref="LayerHeight"/>(FR-06 · SB S2 스크립트).</para>
/// <para><b>순서</b>: 편제 트리(<see cref="UnitTreeBuilder"/>)가 정한 순서를 그대로 쓴다 — 형제 = 코드 오름차순,
/// 뿌리 = 제대 순위 → 코드(편제 밖 상위를 가리키는 노드도 뿌리). 트리 레일과 관계도가 같은 왼→오 순서가 되도록
/// 규칙 사본을 두지 않는다(시나리오 ISSUE-48 · SIM-L050).</para>
/// <para><b>Δ</b>: 노드별 어긋남(월드 단위). 그 부대와 예하 전부가 따라 움직인다(조상 Δ 의 합 + 자기 Δ).
/// 편제에 없는 id 는 무시하고, 문서의 판이 <see cref="LayoutVersion"/> 과 다르면 Δ 를 입히지 않는다.</para>
/// <para>상수는 "각 단계의 <b>나가는</b> 경계에서도 노드가 겹치지 않는다" 를 만족한다(FR-21 — 시험이 잠근다).
/// 이 알고리즘을 바꾸면 <see cref="LayoutVersion"/> 을 올린다(공유 Δ 의 기준이 바뀐다 — D-6).</para>
/// </remarks>
public static class UnitMapLayout
{
    /// <summary>한 칸 폭 W(월드).</summary>
    public const double SlotWidth = 200;

    /// <summary>층 간격 H(월드).</summary>
    public const double LayerHeight = 160;

    /// <summary>세로 줄 간격 P(월드).</summary>
    public const double ColumnPitch = 130;

    /// <summary>이 알고리즘의 판 번호 — 공유 배치 문서의 <c>layout_version</c> 과 대조한다.</summary>
    public const int LayoutVersion = 1;

    /// <summary>
    /// 자동 배치를 계산하고 <paramref name="deltas"/> 를 입힌다.
    /// </summary>
    /// <param name="tree">편제 트리(콘솔이 이미 조립한 것).</param>
    /// <param name="deltas">부대 id → Δ(월드 단위). 없으면 자동 배치만.</param>
    /// <param name="deltaLayoutVersion">Δ 를 담은 배치 문서의 <c>layout_version</c>.</param>
    public static UnitMapLayoutResult Compute(UnitTreeModel tree, IReadOnlyDictionary<int, Vector>? deltas = null, int deltaLayoutVersion = LayoutVersion)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var isVersionMismatch = deltaLayoutVersion != LayoutVersion;
        if (tree.Count == 0 && !isVersionMismatch) return UnitMapLayoutResult.Empty;

        // ① 칸 · 자동 위치(Δ 와 무관) → ② Δ 적용(판이 같고 Δ 가 있을 때만 새 사전을 만든다).
        var slots = new Dictionary<int, UnitMapSlot>(tree.Count);
        var auto = new Dictionary<int, Point>(tree.Count);
        var slotCount = PlaceAll(tree, slots, auto);

        var positions = isVersionMismatch || deltas is not { Count: > 0 }
            ? auto
            : ApplyDeltas(tree, auto, deltas);

        return new UnitMapLayoutResult(auto, positions, slots, slotCount, BoundsOf(positions), isVersionMismatch);
    }

    #region - 칸 · 자동 위치 -
    /// <summary>뿌리마다 왼→오로 칸을 이어 붙인다. 돌려주는 값은 전체 칸 수.</summary>
    private static int PlaceAll(UnitTreeModel tree, Dictionary<int, UnitMapSlot> slots, Dictionary<int, Point> auto)
    {
        var spans = new Dictionary<int, int>(tree.Count);
        var left = 0;

        // Ordered 는 깊이 우선 · 뿌리는 트리 순서다 — 부모 없는 노드를 그 순서대로 뿌리로 쓴다.
        foreach (var node in tree.Ordered)
        {
            if (node.ParentId is not null || auto.ContainsKey(node.Id)) continue;
            Place(tree, node, left, 0, spans, slots, auto);
            left += SpanOf(tree, node, spans);
        }

        // 망가진 간선으로 어느 뿌리에도 닿지 못한 노드(트리 빌더가 뒤에 붙인 것)도 잃지 않는다.
        foreach (var node in tree.Ordered)
        {
            if (auto.ContainsKey(node.Id)) continue;
            Place(tree, node, left, 0, spans, slots, auto);
            left += SpanOf(tree, node, spans);
        }
        return left;
    }

    private static void Place(UnitTreeModel tree, UnitTreeNode node, int left, double y,
                              Dictionary<int, int> spans, Dictionary<int, UnitMapSlot> slots, Dictionary<int, Point> auto)
    {
        var span = SpanOf(tree, node, spans);
        var x = (left + span / 2.0) * SlotWidth;
        auto[node.Id] = new Point(x, y);
        slots[node.Id] = new UnitMapSlot(left * SlotWidth, span, IsColumnChild: false);

        if (IsColumnParent(tree, node))
        {
            for (var i = 0; i < node.ChildIds.Count; i++)
            {
                var childId = node.ChildIds[i];
                if (auto.ContainsKey(childId)) continue;
                auto[childId] = new Point(x, y + LayerHeight + i * ColumnPitch);
                slots[childId] = new UnitMapSlot(left * SlotWidth, 1, IsColumnChild: true);
            }
            return;
        }

        var childLeft = left;
        foreach (var childId in node.ChildIds)
        {
            var child = tree.Find(childId);
            if (child == null || auto.ContainsKey(childId)) continue;
            Place(tree, child, childLeft, y + LayerHeight, spans, slots, auto);
            childLeft += SpanOf(tree, child, spans);
        }
    }

    /// <summary>칸 수 — 끝 부대 · 세로 줄 부모 = 1, 그 밖 = 자식 칸의 합(메모).</summary>
    private static int SpanOf(UnitTreeModel tree, UnitTreeNode node, Dictionary<int, int> spans)
    {
        if (spans.TryGetValue(node.Id, out var cached)) return cached;

        var span = 0;
        if (!node.HasChildren || IsColumnParent(tree, node))
        {
            span = 1;
        }
        else
        {
            foreach (var childId in node.ChildIds)
                if (tree.Find(childId) is { } child) span += SpanOf(tree, child, spans);
            span = Math.Max(1, span);
        }
        spans[node.Id] = span;
        return span;
    }

    /// <summary>자식이 있고 <b>모두</b> 끝 부대인가 — 그러면 자식을 세로 한 줄로 쌓는다.</summary>
    private static bool IsColumnParent(UnitTreeModel tree, UnitTreeNode node)
    {
        if (!node.HasChildren) return false;
        foreach (var childId in node.ChildIds)
            if (tree.Find(childId) is { HasChildren: true }) return false;
        return true;
    }
    #endregion

    #region - Δ -
    /// <summary>자동 위치 + 조상 Δ 합 + 자기 Δ. 편제에 없는 id 의 Δ 는 쓰이지 않는다(찾는 쪽이 편제 노드뿐이다).</summary>
    private static Dictionary<int, Point> ApplyDeltas(UnitTreeModel tree, Dictionary<int, Point> auto, IReadOnlyDictionary<int, Vector> deltas)
    {
        var accumulated = new Dictionary<int, Vector>(tree.Count);
        var positions = new Dictionary<int, Point>(auto.Count);

        // Ordered 는 깊이 우선이라 부모의 누적 Δ 가 자식보다 먼저 정해진다.
        foreach (var node in tree.Ordered)
        {
            var sum = node.ParentId is int parentId && accumulated.TryGetValue(parentId, out var parentSum) ? parentSum : default;
            if (deltas.TryGetValue(node.Id, out var own) && double.IsFinite(own.X) && double.IsFinite(own.Y)) sum += own;
            accumulated[node.Id] = sum;
            positions[node.Id] = auto[node.Id] + sum;
        }
        return positions;
    }
    #endregion

    private static Rect BoundsOf(IReadOnlyDictionary<int, Point> positions)
    {
        if (positions.Count == 0) return Rect.Empty;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in positions.Values)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }
}
