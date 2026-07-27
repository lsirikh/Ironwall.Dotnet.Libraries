using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/// <summary>측정 모드 — 길이/넓이.</summary>
public enum MeasureKind { Length, Area }

/****************************************************************************
   Purpose      : 측정 툴 임시 오버레이 — 지오점 폴리라인(길이)/닫힌 다각형+채움(넓이) 렌더,
                  커서까지 점선 미리보기, 정점, 구간 중점 거리라벨, 면적 중심 라벨.
                  Measure_Tools FR-03. DB 미저장·마커 미생성(임시). 계산=위경도(MeasureMath).
   Theme        : 색은 전부 앱 테마 토큰(Tactical Command) TryFindResource로 해석 — 라이트/다크 대응.
                  채움=TintAccentBrush(시안틴트), 라벨칩=SurfaceTranslucentBrush, 선/정점=PrimaryBrush.
                  수치 리드아웃은 어도너가 아닌 MapView HUD(MapFloatingPanelStyle)에서 표시(테마 자동스왑).
   Note         : HitTestCore=null(완전 클릭스루, 불변식#3) — 클릭은 맵으로 통과해 MeasureController가 점 추가.
                  지오앵커 재렌더(OnMapZoomChanged/OnMapDrag 구독), Dispose 구독해제.
   Created On   : 2026-07-24 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class MeasureAdorner : Adorner, IDisposable
{
    private readonly GMapControl _map;
    private readonly ILogService? _log;
    private readonly List<PointLatLng> _geoPoints = new();
    private Point? _currentMouse;          // _map(스크린) 좌표
    private bool _disposed;

    private const double PointRadius = 4.5, ChipPadX = 6d, ChipPadY = 3d;

    public MeasureKind Kind { get; set; } = MeasureKind.Length;

    public MeasureAdorner(GMapControl map, MeasureKind kind, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        Kind = kind;
        _log = log;
        IsHitTestVisible = false;   // 완전 클릭스루 — 클릭이 맵으로 통과(불변식#3)
        _map.OnMapZoomChanged += OnMapChanged;
        _map.OnMapDrag += OnMapChanged;
    }

    private void OnMapChanged() => InvalidateVisual();

    #region - 공개 API (컨트롤러가 구동) -
    public int PointCount => _geoPoints.Count;
    /// <summary>유효 측정 — 길이=2점+, 넓이=3점+.</summary>
    public bool IsValid => _geoPoints.Count >= (Kind == MeasureKind.Area ? 3 : 2);
    public IReadOnlyList<PointLatLng> Points => _geoPoints;

    public void AddPoint(PointLatLng p) { _geoPoints.Add(p); InvalidateVisual(); }
    public bool RemoveLastPoint()
    {
        if (_geoPoints.Count == 0) return false;
        _geoPoints.RemoveAt(_geoPoints.Count - 1);
        InvalidateVisual();
        return true;
    }
    public void UpdateMouse(Point? screen) { _currentMouse = screen; InvalidateVisual(); }
    public void Freeze() { _currentMouse = null; InvalidateVisual(); }   // 완료 시 미리보기 제거
    public void Clear() { _geoPoints.Clear(); _currentMouse = null; InvalidateVisual(); }

    /// <summary>미리보기 마우스를 임시 정점으로 포함한 라이브 지오점(리드아웃 계산용).</summary>
    public IReadOnlyList<PointLatLng> LivePoints()
    {
        if (_currentMouse is not Point m) return _geoPoints;
        var pts = new List<PointLatLng>(_geoPoints)
        {
            _map.FromLocalToLatLng((int)Math.Round(m.X), (int)Math.Round(m.Y))
        };
        return pts;
    }
    #endregion

    #region - 렌더 -
    protected override void OnRender(DrawingContext dc)
    {
        if (_disposed || _geoPoints.Count == 0) return;
        try
        {
            var primary = Resolve("PrimaryBrush", Color.FromRgb(0x22, 0xB8, 0xD9));
            var fill = Resolve("TintAccentBrush", Color.FromArgb(0x33, 0x22, 0xB8, 0xD9)); // 시안 틴트(미리제작)
            var vtxCore = Resolve("SurfaceAltBrush", Color.FromRgb(0x1E, 0x28, 0x32));

            var line = new Pen(primary, 2.5) { LineJoin = PenLineJoin.Round }; line.Freeze();
            var preview = new Pen(primary, 2) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) }; preview.Freeze();
            var closePreview = new Pen(Translucent(primary, 0x66), 1.5) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) }; closePreview.Freeze();
            var vtxPen = new Pen(primary, 2); vtxPen.Freeze();

            var screen = new List<Point>(_geoPoints.Count);
            foreach (var g in _geoPoints) { var l = _map.FromLatLngToLocal(g); screen.Add(new Point(l.X, l.Y)); }

            bool area = Kind == MeasureKind.Area;
            bool closed = area && screen.Count >= 3;

            // 1) 넓이=닫힌 다각형 채움(3점+) / 길이=폴리라인
            if (closed)
            {
                var geo = new StreamGeometry();
                using (var c = geo.Open())
                {
                    c.BeginFigure(screen[0], isFilled: true, isClosed: true);
                    for (int i = 1; i < screen.Count; i++) c.LineTo(screen[i], isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(fill, line, geo);
            }
            else
            {
                for (int i = 1; i < screen.Count; i++) dc.DrawLine(line, screen[i - 1], screen[i]);
            }

            // 2) 커서까지 점선 미리보기 + (넓이) 닫는 변 점선
            if (_currentMouse is Point m && screen.Count > 0)
            {
                dc.DrawLine(preview, screen[^1], m);
                if (area && screen.Count >= 2) dc.DrawLine(closePreview, m, screen[0]);
            }

            // 3) 정점
            foreach (var p in screen) dc.DrawEllipse(vtxCore, vtxPen, p, PointRadius, PointRadius);

            // 4) 구간 중점 거리 라벨(확정 구간)
            for (int i = 1; i < screen.Count; i++)
            {
                double segM = MeasureMath.DistanceMeters(_geoPoints[i - 1], _geoPoints[i]);
                DrawChip(dc, MidPoint(screen[i - 1], screen[i]), MeasureFormat.Distance(segM), primary, small: true);
            }

            // 5) 미리보기 구간 거리 라벨(마지막→커서, muted)
            if (_currentMouse is Point mp && screen.Count > 0)
            {
                var g = _map.FromLocalToLatLng((int)Math.Round(mp.X), (int)Math.Round(mp.Y));
                double segM = MeasureMath.DistanceMeters(_geoPoints[^1], g);
                DrawChip(dc, MidPoint(screen[^1], mp), MeasureFormat.Distance(segM), primary, small: true, muted: true);
            }

            // 6) 넓이 중심 면적 라벨(닫힌 상태 — 헤드라인 강조)
            if (closed)
            {
                double a = MeasureMath.AreaSquareMeters(_geoPoints);
                DrawChip(dc, Centroid(screen), MeasureFormat.Area(a), primary, small: false, emphasize: true);
            }
        }
        catch (Exception ex) { _log?.Error($"MeasureAdorner 렌더 실패: {ex.Message}"); }
    }

    /// <summary>맵 위 라벨 칩 — SurfaceTranslucentBrush 배경 + 토큰 텍스트. emphasize=면적 헤드라인(시안 텍스트+테두리).</summary>
    private void DrawChip(DrawingContext dc, Point center, string text, Brush accent, bool small, bool muted = false, bool emphasize = false)
    {
        double dpi = VisualTreeHelper.GetDpi(_map).PixelsPerDip;
        var bg = Resolve("SurfaceTranslucentBrush", Color.FromArgb(0xCC, 0x16, 0x1D, 0x26));
        var border = Resolve("BorderBrush", Color.FromRgb(0x2C, 0x3A, 0x48));
        Brush fg = emphasize ? accent
                 : muted ? Resolve("TextMutedBrush", Color.FromRgb(0x84, 0x93, 0xA2))
                 : Resolve("TextPrimaryBrush", Color.FromRgb(0xE6, 0xED, 0xF3));

        var ft = Ft(text, small ? 10.5 : 12.5, fg, dpi, bold: emphasize, mono: true);
        double w = ft.WidthIncludingTrailingWhitespace + ChipPadX * 2, h = ft.Height + ChipPadY * 2;
        var box = new Rect(center.X - w / 2, center.Y - h / 2, w, h);
        var pen = emphasize ? new Pen(accent, 1) : new Pen(border, 0.8);
        dc.DrawRoundedRectangle(bg, pen, box, 4, 4);
        dc.DrawText(ft, new Point(box.X + ChipPadX, box.Y + ChipPadY));
    }

    private FormattedText Ft(string s, double size, Brush brush, double dpi, bool bold = false, bool mono = false)
        => new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily(mono ? "Consolas" : "Segoe UI"),
                FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            size, brush, dpi);

    private static Point MidPoint(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    private static Point Centroid(IReadOnlyList<Point> pts)
    {
        double x = 0, y = 0; foreach (var p in pts) { x += p.X; y += p.Y; }
        return new Point(x / pts.Count, y / pts.Count);
    }

    /// <summary>앱 테마 토큰 브러시 해석(라이트/다크 대응) — 없으면 다크 폴백.</summary>
    private Brush Resolve(string key, Color fallback)
    {
        if (_map.TryFindResource(key) is Brush b) return b;
        var s = new SolidColorBrush(fallback); s.Freeze(); return s;
    }
    private static Brush Translucent(Brush b, byte alpha)
    {
        var c = (b as SolidColorBrush)?.Color ?? Colors.Gray;
        var t = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B)); t.Freeze(); return t;
    }

    // 완전 클릭스루 — 클릭이 맵으로 통과해 MeasureController가 점 추가(불변식#3).
    protected override HitTestResult? HitTestCore(PointHitTestParameters p) => null;
    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
        _geoPoints.Clear();
    }
}
