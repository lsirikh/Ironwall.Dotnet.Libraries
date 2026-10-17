using System;
using System.Windows;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 격자 스냅 시각화 서비스 — 맵에 고정된(geo-anchored) 픽셀 격자를 DrawingContext에 직접 렌더링.
/// 격자 원점은 고정 지오 앵커의 화면좌표로 위상정렬 → 패닝 시 맵과 함께 이동, 줌은 픽셀크기 유지.
/// </summary>
public class SnapGridOverlayService
{
    // 폭주 방지용 절대 상한(시각 절단 수단 아님). 정상 해상도에선 절대 미발동. (RC-6/FR-15)
    private const int ABS_MAX_LINES = 2000;
    private const double MIN_GRID_PX = 8.0;

    // 격자를 맵에 고정하기 위한 고정 지오 앵커. 패닝해도 지오값 불변, 화면좌표만 재계산되어
    // 격자가 맵과 1:1로 이동한다. (RC-7/FR-16) GPoint(long) 정수라 mod 정밀도 무손실.
    private static readonly PointLatLng AnchorGeo = new PointLatLng(0, 0);

    private Pen? _pen;
    private double _lastGridPx = -1;

    public void InvalidateCache()
    {
        _pen = null;
        _lastGridPx = -1;
    }

    /// <summary>
    /// 격자 원점(화면상 첫 격자선의 x0/y0 ∈ [0, gridPx))을 계산한다.
    /// 고정 지오 앵커의 현재 화면좌표에 위상정렬(phase-align)하여, 패닝 시 격자가 맵과 함께
    /// 이동하고 줌 시 gridPx 픽셀크기는 유지된다. (RC-7/FR-16)
    /// 시각 격자(DrawGrid)와 스냅 수학(MarkerEditAdorner/SnapBoundsCenter)이 반드시
    /// 이 동일 메서드로 원점을 산출해야 '시각=스냅' 단일원천 불변식(RC-1)이 유지된다.
    /// </summary>
    public static (double x0, double y0) ComputeOrigin(GMapControl ctrl, double gridPx)
    {
        var a = ctrl.FromLatLngToLocal(AnchorGeo); // GPoint(long) — 화면 픽셀 (RenderOffset 자동 합성)
        double x0 = ((a.X % gridPx) + gridPx) % gridPx; // [0, gridPx)
        double y0 = ((a.Y % gridPx) + gridPx) % gridPx;
        return (x0, y0);
    }

    /// <summary>
    /// 화면 픽셀 좌표를 격자에 스냅한다. 교점(cross-point) 가중치 &gt; 라인(line) 가중치.
    /// - 교점 반경(rCross) 안: 양축 모두 스냅 → 교점에 흡착(더 강함)
    /// - 라인 반경(rLine) 안: 해당 축만 스냅 → 격자선에 흡착
    /// rCross &gt; rLine 이므로 교점이 라인보다 더 넓게/강하게 끌어당긴다. 데드존 없음(합집합).
    /// </summary>
    /// <returns>(스냅된 x, 스냅된 y, x축 스냅여부, y축 스냅여부)</returns>
    public static (double x, double y, bool snapX, bool snapY) Snap(
        double px, double py, double gridPx, double x0, double y0,
        double rCrossRatio = 0.25, double rLineRatio = 0.15)
    {
        double nx = x0 + Math.Round((px - x0) / gridPx) * gridPx;
        double ny = y0 + Math.Round((py - y0) / gridPx) * gridPx;
        double dx = Math.Abs(px - nx);
        double dy = Math.Abs(py - ny);

        double rLine = Math.Max(gridPx * rLineRatio, 3.0);
        double rCross = gridPx * rCrossRatio;

        // 교점 박스 안이면 양축 강제 스냅(교점 우선), 아니면 축별 라인 스냅.
        bool cross = dx < rCross && dy < rCross;
        bool snapX = cross || dx < rLine;
        bool snapY = cross || dy < rLine;

        return (snapX ? nx : px, snapY ? ny : py, snapX, snapY);
    }

    /// <summary>최소 격자 픽셀 하한을 적용한 유효 gridPx.</summary>
    public static double EffectiveGridPx(double gridPx) => Math.Max(MIN_GRID_PX, gridPx);

    /// <summary>
    /// 격자를 화면에 그린다. GMapCustomControl.OnRender()에서 호출.
    /// </summary>
    public void DrawGrid(DrawingContext dc, GMapCustomControl ctrl, double pixelsPerDip)
    {
        var gridPx = Math.Max(MIN_GRID_PX, ctrl.GridSizePx);

        if (_pen == null || Math.Abs(_lastGridPx - gridPx) > 0.01)
        {
            // 위성지도 위에서 잘 보이도록 반투명 흰색 사용
            var brush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
            brush.Freeze();
            _pen = new Pen(brush, 1.5);
            _pen.Freeze();
            _lastGridPx = gridPx;
        }

        var w = ctrl.ActualWidth;
        var h = ctrl.ActualHeight;

        // 스냅 수학과 동일한 원점 사용 (RC-1: 시각 격자 = 스냅 격자, 맵 고정)
        var (x0, y0) = ComputeOrigin(ctrl, gridPx);

        // 축별로 화면 전체를 덮는 데 필요한 선 수 + 여유. 폭주 방지용 절대 상한만 가드.
        // (RC-6/FR-15: 단일 100선 한도가 폭축 세로선을 우측에서 잘라내던 버그 제거)
        int guardV = Math.Min((int)Math.Ceiling(w / gridPx) + 2, ABS_MAX_LINES);
        int guardH = Math.Min((int)Math.Ceiling(h / gridPx) + 2, ABS_MAX_LINES);

        // 세로선
        int vCount = 0;
        for (double x = x0; x <= w && vCount < guardV; x += gridPx, vCount++)
            dc.DrawLine(_pen, new Point(x, 0), new Point(x, h));

        // 가로선
        int hCount = 0;
        for (double y = y0; y <= h && hCount < guardH; y += gridPx, hCount++)
            dc.DrawLine(_pen, new Point(0, y), new Point(w, y));
    }
}
