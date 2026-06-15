using System;
using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 격자 스냅 시각화 서비스 — 화면 픽셀 기준 균일 격자를 DrawingContext에 직접 렌더링
/// </summary>
public class SnapGridOverlayService
{
    private const int MAX_GRID_LINES = 100;
    private const double MIN_GRID_PX = 8.0;

    private Pen? _pen;
    private double _lastGridPx = -1;

    public void InvalidateCache()
    {
        _pen = null;
        _lastGridPx = -1;
    }

    /// <summary>
    /// 격자 원점(좌상단 첫 선의 x0/y0)을 계산한다. 시각 격자(DrawGrid)와 스냅 수학이
    /// 반드시 동일한 원점을 쓰도록 단일 진실원천으로 사용한다. (RC-1 해소)
    /// </summary>
    public static (double x0, double y0) ComputeOrigin(double gridPx, double w, double h)
    {
        double x0 = gridPx - (w % gridPx);
        if (x0 >= gridPx) x0 -= gridPx;
        double y0 = gridPx - (h % gridPx);
        if (y0 >= gridPx) y0 -= gridPx;
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

        // 스냅 수학과 동일한 원점 사용 (RC-1: 시각 격자 = 스냅 격자)
        var (x0, y0) = ComputeOrigin(gridPx, w, h);

        // 세로선 (왼쪽 기준 정렬)
        int vCount = 0;
        for (double x = x0; x <= w && vCount < MAX_GRID_LINES; x += gridPx, vCount++)
            dc.DrawLine(_pen, new Point(x, 0), new Point(x, h));

        // 가로선 (위쪽 기준 정렬)
        int hCount = 0;
        for (double y = y0; y <= h && hCount < MAX_GRID_LINES; y += gridPx, hCount++)
            dc.DrawLine(_pen, new Point(0, y), new Point(w, y));
    }
}
