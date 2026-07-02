using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : 심볼 라벨(제목) 분리 오버레이 — 마커 아이콘 중심 + LabelOffset 위치에 라벨 렌더.
                  맵레벨 adorn(아이콘 RenderTransform 밖 → 라벨 회전 안 함, 수직 유지). 심볼 이동/줌/팬 추종.
                  Phase 4: 라벨 박스 드래그로 오프셋 이동(1.5×반경 상한) + 드래그 중 점선 리더선 + 오프셋 영속.
                  Symbol_Label_Decouple (FR-LB-02/04/05/06).
   Note         : AdornerManager 미사용(LabelAdornerService 소유). HitTestCore=라벨박스만(그 외 클릭스루, 불변식#3).
                  좌표=inner공간(FromLatLngToLocal / e.GetPosition(_map), InnerToOuter 금지 불변식#10).
                  라벨박스는 아이콘 하단 오프셋 → MarkerEditAdorner 아이콘 핸들과 비겹침(공존, FR-LB-06).
                  Dispose서 줌/드래그 구독해제(누수 방지).
   Created On   : 2026-07-02 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class LabelAdorner : Adorner, IDisposable
{
    private readonly GMapCustomControl _map;
    private readonly IEditableMarker _marker;
    private readonly ILogService? _log;
    private bool _disposed;

    private static readonly Brush _bg = Frozen(new SolidColorBrush(Color.FromArgb(205, 28, 30, 34)));
    private static readonly Brush _fg = Frozen(new SolidColorBrush(Color.FromArgb(240, 240, 244, 248)));
    private static readonly Pen _border = FrozenPen(Color.FromArgb(160, 0, 170, 255), 1d);
    private static readonly Pen _leaderPen = FrozenDashPen(Color.FromArgb(200, 0, 170, 255), 1.5d);   // 라벨 드래그 중 점선 리더선

    private const double PadX = 5d, PadY = 2d, IconGap = 8d;

    private Rect _labelRect;                 // OnRender서 갱신 — HitTestCore/드래그 시작 판정
    private bool _labelDragging;
    private Point _grabScreen;               // _map 공간
    private double _origOffsetX, _origOffsetY;

    /// <summary>라벨 드래그 완료 — 서비스/VM이 오프셋 DB 영속(FR-LB-05).</summary>
    public event System.Action<IEditableMarker>? LabelOffsetChanged;

    public LabelAdorner(GMapCustomControl map, IEditableMarker marker, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _marker = marker ?? throw new ArgumentNullException(nameof(marker));
        _log = log;
        IsHitTestVisible = true;      // 라벨박스 위 hit 필요(그 외는 HitTestCore null로 투과)
        Cursor = Cursors.SizeAll;     // adorner는 HitTestCore로 라벨박스 위에서만 이벤트 수신 → 이동커서도 거기서만

        _map.OnMapZoomChanged += OnMapChanged;   // geo-앵커 재렌더(줌/팬 추종)
        _map.OnMapDrag += OnMapChanged;
    }

    private void OnMapChanged() => InvalidateVisual();

    /// <summary>아이콘 스크린 중심.</summary>
    private Point IconCenter()
    {
        var ip = _map.FromLatLngToLocal(_marker.Position);
        return new Point(ip.X, ip.Y);
    }

    /// <summary>아이콘 중심 하단 기본위치 + LabelOffset. 라벨박스 중심 좌표.</summary>
    private Point LabelCenter()
    {
        var ic = IconCenter();
        double belowY = _marker.Height / 2.0 + IconGap;   // 오프셋 0 = 아이콘 바로 아래(기존 템플릿 느낌)
        return new Point(ic.X + _marker.LabelOffsetX, ic.Y + belowY + _marker.LabelOffsetY);
    }

    protected override void OnRender(DrawingContext dc)
    {
        try
        {
            _labelRect = Rect.Empty;
            if (_marker.IsDisposed) return;
            var title = _marker.Title;
            if (string.IsNullOrWhiteSpace(title)) return;
            if (!_marker.ShowTitle) return;   // 기존 라벨 가시성 규칙(ShowTitle) 존중

            double dpi = VisualTreeHelper.GetDpi(_map).PixelsPerDip;
            var ft = new FormattedText(title, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), Math.Max(9d, _marker.TitleSize), _fg, dpi)
            { MaxTextWidth = 200d, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };

            var c = LabelCenter();
            double w = ft.WidthIncludingTrailingWhitespace + PadX * 2d;
            double h = ft.Height + PadY * 2d;
            var box = new Rect(c.X - w / 2d, c.Y - h / 2d, w, h);
            _labelRect = box;

            // 라벨만 드래그 중일 때만 점선 리더선(아이콘↔라벨) — 심볼 이동 시엔 라벨이 오프셋 유지로 따라올 뿐, 선 없음(FR-LB-04)
            if (_labelDragging)
                dc.DrawLine(_leaderPen, IconCenter(), c);

            dc.DrawRoundedRectangle(_bg, _border, box, 3d, 3d);
            dc.DrawText(ft, new Point(box.X + PadX, box.Y + PadY));
        }
        catch (Exception ex) { _log?.Error($"LabelAdorner 렌더 실패: {ex.Message}"); }
    }

    // 라벨박스에서만 hit(그 외 투과, 불변식#3). MarkerEditAdorner 아이콘 핸들과 비겹침(라벨=아이콘 하단 오프셋).
    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => !_labelRect.IsEmpty && _labelRect.Contains(hitTestParameters.HitPoint)
            ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null!;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!_labelRect.IsEmpty && _labelRect.Contains(e.GetPosition(this)))
        {
            _labelDragging = true;
            _grabScreen = e.GetPosition(_map);   // 델타는 맵컨트롤 공간(디지털줌 자동 역보정, 불변식#10)
            _origOffsetX = _marker.LabelOffsetX;
            _origOffsetY = _marker.LabelOffsetY;
            CaptureMouse();
            e.Handled = true;
            InvalidateVisual();
        }
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_labelDragging)
        {
            var cur = e.GetPosition(_map);
            double nx = _origOffsetX + (cur.X - _grabScreen.X);
            double ny = _origOffsetY + (cur.Y - _grabScreen.Y);
            // 상한: 오프셋 벡터 길이 ≤ max(W,H)/2 × 1.5 (너무 멀어져 안 보임 방지, FR-LB-04)
            double cap = Math.Max(_marker.Width, _marker.Height) / 2.0 * 1.5;
            double len = Math.Sqrt(nx * nx + ny * ny);
            if (len > cap && len > 0d) { nx *= cap / len; ny *= cap / len; }
            _marker.LabelOffsetX = nx;
            _marker.LabelOffsetY = ny;
            InvalidateVisual();
            e.Handled = true;
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_labelDragging)
        {
            _labelDragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
            InvalidateVisual();
            LabelOffsetChanged?.Invoke(_marker);   // lock 밖 발화 — 오프셋 DB 영속(서비스/VM)
        }
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_labelDragging)
        {
            _labelDragging = false;
            InvalidateVisual();
            LabelOffsetChanged?.Invoke(_marker);   // 캡처 손실 시에도 영속(유실 방지)
        }
        base.OnLostMouseCapture(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (IsMouseCaptured) ReleaseMouseCapture();
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
        LabelOffsetChanged = null;
    }

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FrozenPen(Color c, double t) { var p = new Pen(Frozen(new SolidColorBrush(c)), t); p.Freeze(); return p; }
    private static Pen FrozenDashPen(Color c, double t)
    { var p = new Pen(Frozen(new SolidColorBrush(c)), t) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) }; p.Freeze(); return p; }
}
