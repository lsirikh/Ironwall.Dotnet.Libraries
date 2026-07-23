using System;
using System.ComponentModel;
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
   Purpose      : 심볼 라벨(제목) 분리 오버레이 — 마커 시각 footprint 하단 + 오프셋 위치에 라벨 렌더.
                  맵레벨 adorn(아이콘 RenderTransform 밖 → 라벨 회전 안 함, 수직 유지). 심볼 이동/줌/팬 추종.
                  Symbol_Label_Decouple(FR-LB-02/04/05/06) + Overlay_Title_ZoomStyle(FR-01~04):
                  footprint는 같은 프레임 자체 투영(이미지=지오바운즈 회전 AABB·라인=RuntimePoints bbox·심볼=모델 W/H)
                  — 이미지/라인의 모델 W/H(컨트롤 역주입값) 의존 제거(1프레임 stale·시작 시 DB오염 해소).
                  오프셋 도메인: 심볼·라인=px 고정 / 이미지=하프익스텐트 비율 U·V(줌 불변 상대위치).
   Note         : AdornerManager 미사용(LabelAdornerService 소유). HitTestCore=라벨박스만(그 외 클릭스루, 불변식#3).
                  좌표=inner공간(FromLatLngToLocal / e.GetPosition(_map), InnerToOuter 금지 불변식#10).
                  기본위치(오프셋 0)=footprint 하단 + 8px 고정 갭(갭은 줌 스케일하지 않음).
                  Dispose서 줌/드래그 구독해제(누수 방지) — 이벤트 null은 캡처 해제 전(P2-02).
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
    private static readonly Pen _leaderPen = FrozenDashPen(Color.FromArgb(200, 0, 170, 255), 1.5d);   // 라벨 드래그 중 점선 리더선

    private const double PadX = 5d, PadY = 2d, IconGap = 8d;

    private Rect _labelRect;                 // OnRender서 갱신 — HitTestCore/드래그 시작 판정
    private bool _labelDragging;
    private Point _grabScreen;               // _map 공간
    private double _origOffsetX, _origOffsetY;   // 심볼/라인 px before
    private double _origOffsetU, _origOffsetV;   // 이미지 U/V before (FR-02)

    private const double IMAGE_LABEL_RADIUS_MULT = 3.0;   // 이미지 드래그 상한 = 3·max(hw,hh) px — 줌 불변·등방(FR-03)
    private const double MIN_IMAGE_FOOTPRINT_PX = 10.0;   // GMapMarkerImageControl.OnRender MinSize와 동일 클램프

    /// <summary>라벨 드래그 완료 — 서비스/VM이 오프셋 DB 영속(FR-LB-05). (marker, beforeOffsetX, beforeOffsetY) — undo before 명시 전달.</summary>
    public event System.Action<IEditableMarker, double, double>? LabelOffsetChanged;

    public LabelAdorner(GMapCustomControl map, IEditableMarker marker, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _marker = marker ?? throw new ArgumentNullException(nameof(marker));
        _log = log;
        IsHitTestVisible = true;      // 라벨박스 위 hit 필요(그 외는 HitTestCore null로 투과)
        Cursor = Cursors.SizeAll;     // adorner는 HitTestCore로 라벨박스 위에서만 이벤트 수신 → 이동커서도 거기서만

        _map.OnMapZoomChanged += OnMapChanged;   // geo-앵커 재렌더(줌/팬 추종)
        _map.OnMapDrag += OnMapChanged;
        if (_marker is INotifyPropertyChanged npc)   // 제목/제목크기/제목표시·가시성 변경 즉시 반영(속성패널)
            npc.PropertyChanged += OnMarkerPropertyChanged;
    }

    private void OnMapChanged() => InvalidateVisual();

    // 마커 속성(Title/TitleSize/ShowTitle/Position/크기/줌·레이어 가시성) 변경 시 라벨 재렌더 → 속성패널 즉시 반영.
    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    /// <summary>아이콘 스크린 중심.</summary>
    private Point IconCenter()
    {
        var ip = _map.FromLatLngToLocal(_marker.Position);
        return new Point(ip.X, ip.Y);
    }

    /// <summary>폭 w·높이 h 사각형을 bearing(도)만큼 회전한 축정렬(AABB) 반치수 — 순수 수식(FR-01/04, 테스트 공유).</summary>
    internal static (double hw, double hh) RotatedAabbHalf(double w, double h, double bearingDeg)
    {
        double rad = bearingDeg * Math.PI / 180.0;
        double c = Math.Abs(Math.Cos(rad)), s = Math.Abs(Math.Sin(rad));
        return ((w * c + h * s) / 2.0, (w * s + h * c) / 2.0);
    }

    /// <summary>앵커(마커 중심) 상대 라벨 중심 — 순수 수식(FR-01/02, 헤드리스 테스트 공유).
    /// 기본위치(오프셋 0)=footprint 하단 + IconGap(8px 고정, 줌 스케일 없음).
    /// isNormalized=true(이미지)면 오프셋을 하프익스텐트 비율로 해석(offX=U·hw, offY=V·hh).</summary>
    internal static Point ComputeLabelCenterRel(double hw, double hh, double offX, double offY, bool isNormalized)
        => isNormalized
            ? new Point(offX * hw, hh + IconGap + offY * hh)
            : new Point(offX, hh + IconGap + offY);

    /// <summary>마커 계열별 시각 footprint 하프익스텐트(px) — 같은 프레임 자체 투영(FR-01).
    /// 이미지: 지오바운즈 투영 + Bearing 회전 AABB(모델 W/H 미사용 — stale/startup/저장오염 격리).
    /// 라인/폴리곤/PidsGroup: RuntimePoints 투영 bbox(비면 모델 W/H 폴백, V-03 가드).
    /// 점 심볼: 모델 W/H(고정 px, 현행 유지 — 시뮬 S그룹 0 FAIL 확증).</summary>
    private (double hw, double hh) VisualHalfExtents()
    {
        if (_marker is IImageEditableMarker img)
        {
            var b = img.ImageBounds;
            var tl = _map.FromLatLngToLocal(b.LocationTopLeft);
            var br = _map.FromLatLngToLocal(b.LocationRightBottom);
            double w = Math.Max(Math.Abs(br.X - tl.X), MIN_IMAGE_FOOTPRINT_PX);
            double h = Math.Max(Math.Abs(br.Y - tl.Y), MIN_IMAGE_FOOTPRINT_PX);
            return RotatedAabbHalf(w, h, _marker.Bearing);
        }
        if (_marker is ILineEditableMarker line && line.RuntimePoints is { Count: > 0 } pts)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in pts)
            {
                var lp = _map.FromLatLngToLocal(p);
                if (lp.X < minX) minX = lp.X;
                if (lp.X > maxX) maxX = lp.X;
                if (lp.Y < minY) minY = lp.Y;
                if (lp.Y > maxY) maxY = lp.Y;
            }
            return (Math.Max((maxX - minX) / 2.0, 1d), Math.Max((maxY - minY) / 2.0, 1d));
        }
        return (_marker.Width / 2.0, _marker.Height / 2.0);
    }

    /// <summary>footprint 하단 기본위치 + 오프셋(심볼·라인=px, 이미지=U/V 비율). 라벨박스 중심 좌표.</summary>
    private Point LabelCenter()
    {
        var ic = IconCenter();
        var (hw, hh) = VisualHalfExtents();
        var rel = _marker is IImageEditableMarker img
            ? ComputeLabelCenterRel(hw, hh, img.LabelOffsetU, img.LabelOffsetV, isNormalized: true)
            : ComputeLabelCenterRel(hw, hh, _marker.LabelOffsetX, _marker.LabelOffsetY, isNormalized: false);
        return new Point(ic.X + rel.X, ic.Y + rel.Y);
    }

    /// <summary>라벨 렌더 가시성 게이트 — OnRender와 헤드리스 테스트가 공유하는 순수 술어.
    /// 제목 표시 = ShowTitle(속성창 조건3) && 줌 && IsLayerEnabled. 레이어 마스터(Visible)는 IsLayerEnabled에
    /// 합성(카테고리 && Visible)되므로 마스터 OFF면 IsLayerEnabled=false로 라벨 자동 숨김.
    /// ShowShape(속성창 모양)와 무관 — ShowShape=false·ShowTitle=true면 제목만 표시(title-only) 보존.</summary>
    internal static bool ShouldRenderLabel(bool isDisposed, string? title, bool showTitle,
        double markerZoom, bool isLayerEnabled, double mapZoom)
    {
        if (isDisposed) return false;
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (!showTitle) return false;                              // 제목 표시 여부(속성창 조건3)
        if (mapZoom < markerZoom || !isLayerEnabled) return false; // 줌 && 마스터(IsLayerEnabled=카테고리&&Visible)
        return true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        try
        {
            _labelRect = Rect.Empty;
            // 라벨 렌더 가시성 — 단일 술어(ShouldRenderLabel)로 통일. 마스터 OFF는 IsLayerEnabled 경유로 숨김.
            if (!ShouldRenderLabel(_marker.IsDisposed, _marker.Title, _marker.ShowTitle,
                    _marker.Zoom, _marker.IsLayerEnabled, _map.Zoom))
                return;
            var title = _marker.Title!;

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

            // 테두리 없이 배경 칩만 — MarkerEditAdorner 편집박스와 혼동(adorner 박스 2개처럼 보임) 방지. 가독성용 배경만 유지.
            dc.DrawRoundedRectangle(_bg, null, box, 3d, 3d);
            dc.DrawText(ft, new Point(box.X + PadX, box.Y + PadY));
        }
        catch (Exception ex) { _log?.Error($"LabelAdorner 렌더 실패: {ex.Message}"); }
    }

    // 라벨박스에서만 hit(그 외 투과, 불변식#3). MarkerEditAdorner 아이콘 핸들과 비겹침(라벨=아이콘 하단 오프셋).
    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => _map.IsEditMode && !_labelRect.IsEmpty && _labelRect.Contains(hitTestParameters.HitPoint)   // 편집모드일 때만 히트(그 외 클릭스루=이동 불가, L2)
            ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null!;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_map.IsEditMode && !_labelRect.IsEmpty && _labelRect.Contains(e.GetPosition(this)))   // 라벨 이동은 맵 편집모드 ON일 때만(L2)
        {
            _labelDragging = true;
            _grabScreen = e.GetPosition(_map);   // 델타는 맵컨트롤 공간(디지털줌 자동 역보정, 불변식#10)
            _origOffsetX = _marker.LabelOffsetX;
            _origOffsetY = _marker.LabelOffsetY;
            if (_marker is IImageEditableMarker imgB) { _origOffsetU = imgB.LabelOffsetU; _origOffsetV = imgB.LabelOffsetV; }   // 이미지 undo before=U/V(FR-02)
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
            double dx = cur.X - _grabScreen.X, dy = cur.Y - _grabScreen.Y;
            if (_marker is IImageEditableMarker imgM)
            {
                // 이미지(FR-02/03): px 델타를 U/V로 역산. 상한=3·max(hw,hh) px — 양변이 2^Δz로 함께 스케일해 줌 불변·등방.
                var (hw, hh) = VisualHalfExtents();
                double nx = _origOffsetU * hw + dx, ny = _origOffsetV * hh + dy;
                double cap = Math.Max(hw, hh) * IMAGE_LABEL_RADIUS_MULT;
                double len = Math.Sqrt(nx * nx + ny * ny);
                if (len > cap && len > 0d) { nx *= cap / len; ny *= cap / len; }
                imgM.LabelOffsetU = nx / Math.Max(1e-6, hw);
                imgM.LabelOffsetV = ny / Math.Max(1e-6, hh);
            }
            else
            {
                double nx = _origOffsetX + dx;
                double ny = _origOffsetY + dy;
                // 상한: 오프셋 벡터 길이. 심볼=반지름(max(W,H)/2)의 배수 — 고정 px 아이콘이라 줌 불변(FR-LB-04).
                // [LineArea_Symbol_Resize FR-07] line/폴리곤은 W/H가 파생·transient라 절대 픽셀 상한(§5-C D3b).
                const double SYMBOL_LABEL_RADIUS_MULT = 4.0;
                const double LINE_LABEL_CAP_PX = 500.0;
                double cap = _marker is GMapSymbols.ILineEditableMarker
                    ? LINE_LABEL_CAP_PX
                    : Math.Max(_marker.Width, _marker.Height) / 2.0 * SYMBOL_LABEL_RADIUS_MULT;
                double len = Math.Sqrt(nx * nx + ny * ny);
                if (len > cap && len > 0d) { nx *= cap / len; ny *= cap / len; }
                _marker.LabelOffsetX = nx;
                _marker.LabelOffsetY = ny;
            }
            InvalidateVisual();
            e.Handled = true;
        }
        base.OnMouseMove(e);
    }

    /// <summary>드래그 완료 통지 — before 쌍의 도메인은 마커 타입 따름(이미지=U/V, 그 외=px). VM/undo가 동일 규약으로 분기(FR-11).</summary>
    private void RaiseOffsetChanged()
    {
        if (_marker is IImageEditableMarker)
            LabelOffsetChanged?.Invoke(_marker, _origOffsetU, _origOffsetV);
        else
            LabelOffsetChanged?.Invoke(_marker, _origOffsetX, _origOffsetY);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_labelDragging)
        {
            _labelDragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
            InvalidateVisual();
            RaiseOffsetChanged();   // lock 밖 발화 — 오프셋 DB 영속 + undo(before=드래그시작 오프셋)
        }
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_labelDragging)
        {
            _labelDragging = false;
            InvalidateVisual();
            RaiseOffsetChanged();   // 캡처 손실 시에도 영속+undo(유실 방지)
        }
        base.OnLostMouseCapture(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _labelDragging = false;     // ReleaseMouseCapture→OnLostMouseCapture 재진입 시 이벤트 발화 차단(P2-02)
        LabelOffsetChanged = null;  // 캡처 해제 전에 구독 제거 — Dispose 중 발화 방지
        if (IsMouseCaptured) ReleaseMouseCapture();
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
        if (_marker is INotifyPropertyChanged npc) npc.PropertyChanged -= OnMarkerPropertyChanged;
    }

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FrozenDashPen(Color c, double t)
    { var p = new Pen(Frozen(new SolidColorBrush(c)), t) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) }; p.Freeze(); return p; }
}
