using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도의 선 층 — DrawingVisual 하나에 계층선 · 인접선을 그린다 (D-1 · FR-24 · FR-25 · FR-27 · NFR-01 · NFR-07)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 관계도의 선 층. 선은 수백 개라 요소로 두지 않고 <see cref="DrawingVisual"/> <b>하나</b>에 그린다(D-1 · NFR-01).
/// </summary>
/// <remarks>
/// <para>팬은 이 층을 담은 컨테이너의 <c>TranslateTransform</c> 만 바꾼다 — 여기서는 다시 그리지 않는다(NFR-02).
/// 다시 그리는 때는 장면 · 배율 · 선택 · 테마가 바뀔 때뿐이고, 그때마다 <see cref="RenderCount"/> 가 1 오른다.</para>
/// <para>색은 <b>그릴 때마다</b> <see cref="FrameworkElement.TryFindResource"/> 로 토큰을 다시 찾는다 — 한 번 찾아 두면
/// 테마 전환 뒤 옛 색에 고착된다(NFR-07, <c>LineDrawingAdorner</c> 실증 버그). 펜도 매번 새로 만들고 얼리지 않는다.</para>
/// <para>굵기: 계층 1.5 · 인접 2.2(둥근 점선 <c>0,2</c>) · 선택 부대에 닿는 선은 +1(색 불변, FR-27).
/// 이미 배정된 점선 어휘(그룹 선택 4·3 · 드로잉 5·3) · 앰버는 쓰지 않는다(NFR-06).</para>
/// </remarks>
public sealed class UnitMapLineLayer : FrameworkElement
{
    public const string HIERARCHY_TOKEN = "TextMutedBrush";
    public const string ADJACENCY_TOKEN = "StatusInfoBrush";
    public const double HIERARCHY_THICKNESS = 1.5;
    public const double ADJACENCY_THICKNESS = 2.2;
    public const double EMPHASIS_EXTRA = 1.0;

    private readonly DrawingVisual _visual = new();

    public UnitMapLineLayer()
    {
        AddVisualChild(_visual);
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    /// <summary>다시 그린 횟수 — 성능 카운터(NFR-02 · NFR-04 · NFR-07 시험).</summary>
    public int RenderCount { get; private set; }

    /// <summary>마지막으로 그린 계층선 수.</summary>
    public int DrawnHierarchy { get; private set; }

    /// <summary>마지막으로 그린 인접선 수.</summary>
    public int DrawnAdjacency { get; private set; }

    /// <summary>마지막으로 그린 선 중 강조(굵기 +1)한 수.</summary>
    public int DrawnEmphasized { get; private set; }

    /// <summary>마지막으로 쓴 계층선 브러시 — 토큰 재해석 시험용.</summary>
    public Brush? LastHierarchyBrush { get; private set; }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index)
        => index == 0 ? _visual : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>선 전부를 다시 그린다. <paramref name="edges"/> 는 캔버스 좌표(월드 × 배율).</summary>
    public void Redraw(IReadOnlyList<UnitMapEdge> edges, UnitMapLayers layers)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(layers);

        var hierarchyBrush = ResolveBrush(HIERARCHY_TOKEN);
        var adjacencyBrush = ResolveBrush(ADJACENCY_TOKEN);
        LastHierarchyBrush = hierarchyBrush;

        // 같은 펜끼리 기하 하나로 묶는다 — DrawGeometry 4번이면 끝난다(선 수백 개).
        var hierarchy = new StreamGeometry();
        var hierarchyEmphasized = new StreamGeometry();
        var adjacency = new StreamGeometry();
        var adjacencyEmphasized = new StreamGeometry();
        int drawnHierarchy = 0, drawnAdjacency = 0, emphasized = 0;

        using (var h = hierarchy.Open())
        using (var he = hierarchyEmphasized.Open())
        using (var a = adjacency.Open())
        using (var ae = adjacencyEmphasized.Open())
        {
            foreach (var edge in edges)
            {
                if (edge.Points.Count < 2) continue;
                var isHierarchy = edge.Kind == UnitMapEdgeKind.Hierarchy;
                if (isHierarchy ? !layers.Hierarchy : !layers.Adjacency) continue;

                var sink = (isHierarchy, edge.IsEmphasized) switch
                {
                    (true, false) => h,
                    (true, true) => he,
                    (false, false) => a,
                    _ => ae,
                };

                sink.BeginFigure(edge.Points[0], isFilled: false, isClosed: false);
                if (edge.IsCurve && edge.Points.Count == 3)
                    sink.QuadraticBezierTo(edge.Points[1], edge.Points[2], isStroked: true, isSmoothJoin: true);
                else
                    for (var i = 1; i < edge.Points.Count; i++) sink.LineTo(edge.Points[i], isStroked: true, isSmoothJoin: false);

                if (isHierarchy) drawnHierarchy++; else drawnAdjacency++;
                if (edge.IsEmphasized) emphasized++;
            }
        }

        using (var dc = _visual.RenderOpen())
        {
            dc.DrawGeometry(null, HierarchyPen(hierarchyBrush, HIERARCHY_THICKNESS), hierarchy);
            dc.DrawGeometry(null, HierarchyPen(hierarchyBrush, HIERARCHY_THICKNESS + EMPHASIS_EXTRA), hierarchyEmphasized);
            dc.DrawGeometry(null, AdjacencyPen(adjacencyBrush, ADJACENCY_THICKNESS), adjacency);
            dc.DrawGeometry(null, AdjacencyPen(adjacencyBrush, ADJACENCY_THICKNESS + EMPHASIS_EXTRA), adjacencyEmphasized);
        }

        DrawnHierarchy = drawnHierarchy;
        DrawnAdjacency = drawnAdjacency;
        DrawnEmphasized = emphasized;
        RenderCount++;
    }

    /// <summary>토큰을 지금 찾는다. 없으면(디자이너 · 토큰 없는 호스트) 전경색 — 정적 브러시로 떨어지지 않는다.</summary>
    private Brush ResolveBrush(string token)
        => TryFindResource(token) as Brush
           ?? TryFindResource(SystemColors.GrayTextBrushKey) as Brush
           ?? new SolidColorBrush(SystemColors.GrayTextColor);

    private static Pen HierarchyPen(Brush brush, double thickness)
        => new(brush, thickness) { LineJoin = PenLineJoin.Miter, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };

    /// <summary>둥근 점선 — 대시 <c>0,2</c>(굵기 배수) · 둥근 끝. 그룹 선택 · 러버밴드 · 드로잉 · 드롭 어휘와 겹치지 않는다.</summary>
    private static Pen AdjacencyPen(Brush brush, double thickness)
        => new(brush, thickness)
        {
            DashStyle = new DashStyle(new[] { 0.0, 2.0 }, 0),
            DashCap = PenLineCap.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
}
