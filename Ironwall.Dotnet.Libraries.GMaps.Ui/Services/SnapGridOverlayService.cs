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

        // 세로선 (왼쪽 기준 정렬)
        var x0 = gridPx - (w % gridPx);
        if (x0 >= gridPx) x0 -= gridPx;
        int vCount = 0;
        for (double x = x0; x <= w && vCount < MAX_GRID_LINES; x += gridPx, vCount++)
            dc.DrawLine(_pen, new Point(x, 0), new Point(x, h));

        // 가로선 (위쪽 기준 정렬)
        var y0 = gridPx - (h % gridPx);
        if (y0 >= gridPx) y0 -= gridPx;
        int hCount = 0;
        for (double y = y0; y <= h && hCount < MAX_GRID_LINES; y += gridPx, hCount++)
            dc.DrawLine(_pen, new Point(0, y), new Point(w, y));
    }
}
