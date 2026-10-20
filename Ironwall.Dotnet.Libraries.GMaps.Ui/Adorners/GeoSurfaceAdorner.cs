using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : 지도 위 전체를 덮어 입력을 받는 그리기 표면(「건물 그리기」 · 「꾸미기」)의 공용 바탕 —
                  지오앵커 재렌더(줌 · 끌기 · 회전 통지) · 지도 전체 히트 · 휠은 지도로 · 위경도 → 화면 · 도형 그리기 · 테마 토큰 찾기.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
****************************************************************************/
public abstract class GeoSurfaceAdorner : Adorner, IDisposable
{
    protected readonly GMapControl _map;
    protected readonly ILogService? _log;
    protected bool _disposed;

    protected GeoSurfaceAdorner(GMapControl map, ILogService? log) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;
        Cursor = Cursors.Cross;
        _map.OnMapZoomChanged += OnMapChanged;
        _map.OnMapDrag += OnMapChanged;
        (_map as GMapCustoms.GMapCustomControl)?.SubscribeViewport(OnViewportSnapshot);
    }

    private void OnMapChanged() => InvalidateVisual();
    private void OnViewportSnapshot(GMapCustoms.MapViewportSnapshot _) => InvalidateVisual();

    /// <summary>위경도(X = 경도, Y = 위도) → 지도 화면 점.</summary>
    protected Point ScreenPoint(UbPoint geo)
    {
        var l = _map.FromLatLngToLocal(new PointLatLng(geo.Y, geo.X));
        return new Point(l.X, l.Y);
    }

    protected UbPoint At(MouseEventArgs e) { var p = e.GetPosition(_map); return new UbPoint(p.X, p.Y); }

    protected override HitTestResult HitTestCore(PointHitTestParameters p) => new PointHitTestResult(this, p.HitPoint);

    /// <summary>휠은 지도로 — 그리는 동안에도 줌은 된다.</summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        _map.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = _map });
        e.Handled = true;
    }

    /// <summary>화면 점들을 잇는 선(closed = 닫힌 도형, fill 은 닫혔을 때만).</summary>
    protected static void DrawPath(DrawingContext dc, IReadOnlyList<Point> pts, bool closed, Brush? fill, Pen pen)
    {
        if (pts.Count < 2) return;
        var geo = new StreamGeometry();
        using (var c = geo.Open())
        {
            c.BeginFigure(pts[0], isFilled: closed && fill != null, isClosed: closed);
            for (int i = 1; i < pts.Count; i++) c.LineTo(pts[i], isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        dc.DrawGeometry(fill, pen, geo);
    }

    /// <summary>테마 토큰 브러시(없으면 대신 색).</summary>
    protected Brush Resolve(string key, Color fallback)
    {
        if (_map.TryFindResource(key) is Brush b) return b;
        var s = new SolidColorBrush(fallback); s.Freeze(); return s;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
        (_map as GMapCustoms.GMapCustomControl)?.UnsubscribeViewport(OnViewportSnapshot);
    }
}
