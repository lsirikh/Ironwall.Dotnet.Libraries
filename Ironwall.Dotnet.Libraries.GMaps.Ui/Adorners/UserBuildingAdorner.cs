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

/// <summary>「건물 그리기」 표면이 부르는 쪽(MapViewModel) — 그릴 것 · 마우스.</summary>
public interface IUserBuildingDrawHost
{
    /// <summary>그리는 윤곽(없으면 null).</summary>
    Map3DUserBuildingDraft? UserBuildingDraft { get; }
    /// <summary>그 영역에 저장된 건물(고치는 중인 건물은 빼고 흐리게 그린다).</summary>
    IReadOnlyList<Map3DUserBuildingItem> UserBuildingItems { get; }
    /// <summary>고치는 중인 건물 uid(새로 그리면 null).</summary>
    string? UserBuildingEditingUid { get; }
    void UserBuildingPointerDown(UbPoint screen, int clickCount);
    void UserBuildingPointerMove(UbPoint screen);
    void UserBuildingPointerUp(UbPoint screen);
    void UserBuildingRightClick(UbPoint screen);
    void UserBuildingPointerCancel();
}

/****************************************************************************
   Purpose      : 「건물 그리기」 지도 표면 — 지도 위를 덮어 왼쪽 누르기 · 끌기 · 놓기 · 오른쪽 클릭을 받아 MapViewModel 로 넘기고,
                  그리는 윤곽(선 · 채움 · 꼭짓점 · 선분 가운데 손잡이 · 다음 선 미리보기)과 저장된 건물(흐린 점선)을 그린다.
   Note         : 측정 어도너(MeasureAdorner)와 같은 지오앵커 재렌더(줌 · 끌기 · 회전 통지). 측정과 달리 히트테스트를 받는다 —
                  꼭짓점 끌기가 지도 끌기(팬) · 마커 고르기보다 먼저여야 한다. 휠은 지도로 넘겨 줌은 그대로 된다.
                  색은 테마 토큰(PrimaryBrush · TintAccentBrush · SurfaceAltBrush · TextMutedBrush) — 라이트/다크.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class UserBuildingAdorner : Adorner, IDisposable
{
    private readonly GMapControl _map;
    private readonly IUserBuildingDrawHost _host;
    private readonly ILogService? _log;
    private bool _disposed;

    private const double VertexRadius = 5.0, MidRadius = 3.5;

    public UserBuildingAdorner(GMapControl map, IUserBuildingDrawHost host, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _host = host;
        _log = log;
        Cursor = Cursors.Cross;
        _map.OnMapZoomChanged += OnMapChanged;
        _map.OnMapDrag += OnMapChanged;
        (_map as GMapCustoms.GMapCustomControl)?.SubscribeViewport(OnViewportSnapshot);
    }

    private void OnMapChanged() => InvalidateVisual();
    private void OnViewportSnapshot(GMapCustoms.MapViewportSnapshot _) => InvalidateVisual();

    private UbPoint Screen(UbPoint geo)
    {
        var l = _map.FromLatLngToLocal(new PointLatLng(geo.Y, geo.X));
        return new UbPoint(l.X, l.Y);
    }

    private static Point P(UbPoint p) => new(p.X, p.Y);

    // ── 입력(지도 위 전체) ──

    protected override HitTestResult HitTestCore(PointHitTestParameters p) => new PointHitTestResult(this, p.HitPoint);

    private UbPoint At(MouseEventArgs e) { var p = e.GetPosition(_map); return new UbPoint(p.X, p.Y); }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        CaptureMouse();
        _host.UserBuildingPointerDown(At(e), e.ClickCount);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _host.UserBuildingPointerMove(At(e));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        _host.UserBuildingPointerUp(At(e));
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        _host.UserBuildingRightClick(At(e));
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (e.LeftButton == MouseButtonState.Pressed) _host.UserBuildingPointerCancel();   // 끄는 중 캡처를 잃음 — 끌기 전으로
    }

    /// <summary>휠은 지도로 — 그리는 동안에도 줌은 된다.</summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        _map.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = _map });
        e.Handled = true;
    }

    // ── 그리기 ──

    protected override void OnRender(DrawingContext dc)
    {
        if (_disposed) return;
        try
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(new Size(_map.ActualWidth, _map.ActualHeight)));   // 히트 받는 바탕
            var primary = Resolve("PrimaryBrush", Color.FromRgb(0x22, 0xB8, 0xD9));
            var fill = Resolve("TintAccentBrush", Color.FromArgb(0x33, 0x22, 0xB8, 0xD9));
            var core = Resolve("SurfaceAltBrush", Color.FromRgb(0x1E, 0x28, 0x32));
            var muted = Resolve("TextMutedBrush", Color.FromRgb(0x84, 0x93, 0xA2));

            // 저장된 건물 — 흐린 점선(눌러서 고치기)
            var savedPen = new Pen(muted, 1.5) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }; savedPen.Freeze();
            foreach (var it in _host.UserBuildingItems)
                if (it.Uid != _host.UserBuildingEditingUid) DrawPolygon(dc, it.Outline, closed: true, fill: null, savedPen);

            if (_host.UserBuildingDraft is not { } d || d.Points.Count == 0) return;
            var line = new Pen(primary, 2.5) { LineJoin = PenLineJoin.Round }; line.Freeze();
            DrawPolygon(dc, d.Points, d.IsClosed, d.IsClosed ? fill : null, line);

            // 다음 선 미리보기(열린 윤곽) — 마지막 점 → 커서, 커서 → 첫 점(닫는 선)
            if (!d.IsClosed && d.Hover is { } h)
            {
                var preview = new Pen(primary, 1.5) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) }; preview.Freeze();
                dc.DrawLine(preview, P(Screen(d.Points[^1])), P(h));
                if (d.Points.Count >= 2) dc.DrawLine(preview, P(h), P(Screen(d.Points[0])));
            }
            // 선분 가운데 손잡이(닫힌 윤곽 — 끌면 점이 생긴다)
            var midPen = new Pen(primary, 1.2); midPen.Freeze();
            foreach (var (_, mid) in d.MidpointsScreen()) dc.DrawEllipse(core, midPen, P(mid), MidRadius, MidRadius);
            // 꼭짓점 — 고른 · 끄는 점은 두껍게(색만이 아니라 두께로도), 첫 점은 닫기 자리라 크게
            for (int i = 0; i < d.Points.Count; i++)
            {
                bool strong = i == d.SelectedVertex || i == d.DraggingVertex;
                var pen = new Pen(primary, strong ? 3.5 : 2); pen.Freeze();
                double r = i == 0 && !d.IsClosed ? VertexRadius + 2 : VertexRadius;
                dc.DrawEllipse(strong ? primary : core, pen, P(Screen(d.Points[i])), r, r);
            }
        }
        catch (Exception ex) { _log?.Error($"UserBuildingAdorner 렌더 실패: {ex.Message}"); }
    }

    private void DrawPolygon(DrawingContext dc, IReadOnlyList<UbPoint> pts, bool closed, Brush? fill, Pen pen)
    {
        if (pts.Count < 2) return;
        var geo = new StreamGeometry();
        using (var c = geo.Open())
        {
            c.BeginFigure(P(Screen(pts[0])), isFilled: closed && fill != null, isClosed: closed);
            for (int i = 1; i < pts.Count; i++) c.LineTo(P(Screen(pts[i])), isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        dc.DrawGeometry(fill, pen, geo);
    }

    private Brush Resolve(string key, Color fallback)
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
