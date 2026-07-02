using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : 심볼 라벨(제목) 분리 오버레이 — 마커 아이콘 중심 + LabelOffset 위치에 라벨 렌더.
                  맵레벨 adorn(아이콘 RenderTransform 밖 → 라벨 회전 안 함, 수직 유지). 심볼 이동/줌/팬 추종.
                  Symbol_Label_Decouple Phase 2 (FR-LB-02). Phase 4에서 드래그+점선+상한 추가 예정.
   Note         : AdornerManager 미사용(LabelAdornerService 소유). HitTestCore=null(Phase2 렌더전용·클릭스루, 불변식#3).
                  좌표=inner공간(FromLatLngToLocal, InnerToOuter 금지 불변식#10). Dispose서 줌/드래그 구독해제(누수).
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

    private const double PadX = 5d, PadY = 2d, IconGap = 8d;

    public LabelAdorner(GMapCustomControl map, IEditableMarker marker, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _marker = marker ?? throw new ArgumentNullException(nameof(marker));
        _log = log;
        IsHitTestVisible = false;   // Phase2 렌더 전용 — 클릭스루(불변식#3). 드래그는 Phase4.

        _map.OnMapZoomChanged += OnMapChanged;   // geo-앵커 재렌더(줌/팬 추종)
        _map.OnMapDrag += OnMapChanged;
    }

    private void OnMapChanged() => InvalidateVisual();

    /// <summary>아이콘 중심(스크린) 하단 기본위치 + LabelOffset. 라벨박스 중심 좌표.</summary>
    private Point LabelCenter()
    {
        var ip = _map.FromLatLngToLocal(_marker.Position);
        double belowY = _marker.Height / 2.0 + IconGap;   // 오프셋 0 = 아이콘 바로 아래(기존 템플릿 느낌)
        return new Point(ip.X + _marker.LabelOffsetX, ip.Y + belowY + _marker.LabelOffsetY);
    }

    protected override void OnRender(DrawingContext dc)
    {
        try
        {
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
            dc.DrawRoundedRectangle(_bg, _border, box, 3d, 3d);
            dc.DrawText(ft, new Point(box.X + PadX, box.Y + PadY));
        }
        catch (Exception ex) { _log?.Error($"LabelAdorner 렌더 실패: {ex.Message}"); }
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) => null!;   // Phase2 클릭스루

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
    }

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FrozenPen(Color c, double t) { var p = new Pen(Frozen(new SolidColorBrush(c)), t); p.Freeze(); return p; }
}
