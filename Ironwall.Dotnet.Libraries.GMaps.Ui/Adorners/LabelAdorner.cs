using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
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

    private bool _widthResizing;             // FR-13 폭 조절 드래그(edge-pinned)
    private bool _resizeRightEdge;           // true=우측 가장자리 드래그
    private double _origMaxWidth;            // 폭 조절 before (원자 undo용)

    // ── 렌더 캐시(FR-08) — 마커 N개×줌/팬 연타 시 FormattedText/Typeface/Brush 재생성 제거(P1-01).
    //    무효화: _textProps 속성 변경(OnMarkerPropertyChanged)·DPI 변경(OnRender 비교)·색 변경. Dispose서 해제. ──
    private FormattedText? _cachedText;
    private double _cachedDpi;
    private Brush? _cachedFg, _cachedBg;
    private EnumColorType _cachedFgColor, _cachedBgColor;   // v2.5: 색=EnumColorType(FillColor 파이프라인)
    private static readonly System.Collections.Generic.Dictionary<(string fam, bool b, bool i), Typeface> _typefaceCache = new();
    private static readonly object _typefaceGate = new();   // 정적 공유 캐시 — Typeface는 불변 값 객체(architect Q5)

    private const double IMAGE_LABEL_RADIUS_MULT = 3.0;   // 이미지 드래그 상한 = 3·max(hw,hh) px — 줌 불변·등방(FR-03)
    private const double MIN_IMAGE_FOOTPRINT_PX = 10.0;   // GMapMarkerImageControl.OnRender MinSize와 동일 클램프
    internal const double WIDTH_MIN = 40d, WIDTH_MAX = 800d;   // FR-13 폭 클램프
    internal const double DEFAULT_MAX_WIDTH = 200d;            // 종전 하드코딩 MaxTextWidth(NFR-01 기본값)

    /// <summary>라벨 드래그 완료 — 서비스/VM이 오프셋 DB 영속(FR-LB-05). (marker, beforeOffsetX, beforeOffsetY) — undo before 명시 전달.
    /// before 도메인은 마커 타입 따름(이미지=U/V, 그 외=px).</summary>
    public event System.Action<IEditableMarker, double, double>? LabelOffsetChanged;

    /// <summary>폭 조절 완료 — (marker, before오프셋A, before오프셋B, before최대폭). edge-pinned 리사이즈는 오프셋도 함께 바꾸므로
    /// (오프셋,폭) 쌍을 원자 undo·영속(FR-13 — 2차 검증 W그룹). 오프셋 도메인은 마커 타입 따름(이미지=U/V).</summary>
    public event System.Action<IEditableMarker, double, double, double>? LabelWidthChanged;

    public LabelAdorner(GMapCustomControl map, IEditableMarker marker, ILogService? log = null) : base(map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _marker = marker ?? throw new ArgumentNullException(nameof(marker));
        _log = log;
        IsHitTestVisible = true;      // 라벨박스 위 hit 필요(그 외는 HitTestCore null로 투과)
        Cursor = Cursors.SizeAll;     // adorner는 HitTestCore로 라벨박스 위에서만 이벤트 수신 → 이동커서도 거기서만

        _map.OnMapZoomChanged += OnMapChanged;   // geo-앵커 재렌더(줌/팬 추종)
        _map.OnMapDrag += OnMapChanged;
        _map.OnPositionChanged += OnMapPositionChanged;   // 프로그램적/정착 뷰포트 이동(홈 이동·앵커 확정)도 추종 — 첫 페인트 회귀 갭 차단
        if (_marker is INotifyPropertyChanged npc)   // 제목/제목크기/제목표시·가시성 변경 즉시 반영(속성패널)
            npc.PropertyChanged += OnMarkerPropertyChanged;
    }

    private void OnMapChanged() => InvalidateVisual();
    // OnPositionChanged는 PointLatLng 인자를 받으므로 별도 래퍼(줌/드래그와 달리 파라미터 있음).
    private void OnMapPositionChanged(GMap.NET.PointLatLng _) => InvalidateVisual();

    /// <summary>라벨 렌더에 영향을 주는 속성만 재렌더(FR-08 필터 — 2차 검증 N그룹 확정 목록).
    /// Width/Height는 FR-01 이후 값은 안 읽지만 라인/이미지 '지오메트리 변경 신호'로 유지(V-08).
    /// Bearing/ImageBounds는 이미지 회전·이동/리사이즈 시 footprint AABB가 변하므로 필수.</summary>
    private static readonly System.Collections.Generic.HashSet<string> _renderProps = new(StringComparer.Ordinal)
    {
        "Title", "TitleSize", "ShowTitle",
        "TitleColor", "TitleBackground", "TitleFontFamily", "TitleBold", "TitleItalic", "TitleMaxWidth",
        "Position", "Bearing", "ImageBounds", "Width", "Height", "Zoom", "IsLayerEnabled", "Visible",
        "LabelOffsetX", "LabelOffsetY", "LabelOffsetU", "LabelOffsetV",
    };

    /// <summary>텍스트 캐시를 무효화해야 하는 속성(글립/측정 변경) — FR-08 무효화 조건.</summary>
    private static readonly System.Collections.Generic.HashSet<string> _textProps = new(StringComparer.Ordinal)
    {
        "Title", "TitleSize", "TitleColor", "TitleFontFamily", "TitleBold", "TitleItalic", "TitleMaxWidth",
    };

    // 마커 속성 변경 시 라벨 재렌더 — 무관 속성(IsSelected/Fill·StrokeColor 등)은 필터로 제외(P1-02 폭주 방지).
    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var name = e.PropertyName;
        if (string.IsNullOrEmpty(name)) { _cachedText = null; InvalidateVisual(); return; }   // 전체 갱신 통지는 보수적으로
        if (!_renderProps.Contains(name)) return;
        if (_textProps.Contains(name)) _cachedText = null;
        if (name is "TitleColor" or "TitleBackground") { _cachedFg = null; _cachedBg = null; }
        InvalidateVisual();
    }

    /// <summary>아이콘 스크린 중심.
    /// [Rotation 회전+팬 진동 수정] 회전 드래그 중 벤더는 매 프레임 ForceUpdateOverlays로 아이콘을
    /// '레이아웃'(Canvas.SetLeft) 이동시키고(GMapControl OnMouseMove: IsRotated 분기), 라벨은
    /// '렌더' 재투영으로 움직여 두 패스가 프레임마다 교번 어긋남 → 라벨 진동. point 심볼은
    /// 아이콘 Shape의 실제 배치 중심을 직접 읽어(TranslatePoint) 같은 원천에 앵커 — 스큐 구조적 제거.
    /// (마커 Offset=(−W/2,−H/2)라 Shape 중심=Position 투영과 동치. 라인/이미지 footprint 계열은
    /// 기하가 별도 재투영이라 기존 투영 유지 — 혼합 원천 방지.)</summary>
    private Point IconCenter()
    {
        if (_marker is not ILineEditableMarker
            && _marker is GMap.NET.WindowsPresentation.GMapMarker gm && gm.Shape is FrameworkElement fe
            && fe.IsLoaded && fe.ActualWidth > 0 && fe.ActualHeight > 0)
        {
            try
            {
                return fe.TranslatePoint(new Point(fe.ActualWidth / 2.0, fe.ActualHeight / 2.0), _map);
            }
            catch { /* 비주얼 트리 분리 과도기 — 투영 폴백 */ }
        }
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
        // [Rotation FR-14 / R-23] 익스텐트는 '회전 불변'이어야 한다 — 종전 축차/bbox 방식은
        // FromLatLngToLocal이 맵 회전을 포함해 θ에 따라 값이 변했고, 그 값으로 나눈 U/V 정규화가
        // DB에 영속되어 회전 상태에서 잡은 라벨이 다른 θ에서 튀었다(45° 부근 U 폭주).
        if (_marker is IImageEditableMarker img)
        {
            // 인접 모서리 유클리드 거리 = 회전 불변(회전은 등거리 변환). θ=0에선 종전 축차와 동일.
            var b = img.ImageBounds;
            var tl = _map.FromLatLngToLocal(b.LocationTopLeft);
            var tr = _map.FromLatLngToLocal(new GMap.NET.PointLatLng(b.Lat, b.Lng + b.WidthLng));
            var bl = _map.FromLatLngToLocal(new GMap.NET.PointLatLng(b.Lat - b.HeightLat, b.Lng));
            double w = Math.Max(Dist(tl, tr), MIN_IMAGE_FOOTPRINT_PX);
            double h = Math.Max(Dist(tl, bl), MIN_IMAGE_FOOTPRINT_PX);
            return RotatedAabbHalf(w, h, _marker.Bearing);
        }
        if (_marker is ILineEditableMarker line && line.RuntimePoints is { Count: > 0 } pts)
        {
            // 투영점을 맵 회전 역행렬로 되돌린 뒤 bbox — θ 무관 동일 footprint(비회전 시 Identity).
            var inv = _map.RotationMatrixValue; inv.Invert();
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in pts)
            {
                var lp = _map.FromLatLngToLocal(p);
                var up = inv.Transform(new Point(lp.X, lp.Y));
                if (up.X < minX) minX = up.X;
                if (up.X > maxX) maxX = up.X;
                if (up.Y < minY) minY = up.Y;
                if (up.Y > maxY) maxY = up.Y;
            }
            return (Math.Max((maxX - minX) / 2.0, 1d), Math.Max((maxY - minY) / 2.0, 1d));
        }
        return (_marker.Width / 2.0, _marker.Height / 2.0);

        static double Dist(GMap.NET.GPoint a, GMap.NET.GPoint b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>오프셋 정규화 대상 — footprint가 줌마다 스케일하는 마커(이미지 + 라인/폴리곤/PidsGroup).
    /// 점 심볼만 고정 px(고정 크기 아이콘이라 px가 줌 불변). 정규화면 오프셋=하프익스텐트 비율이라
    /// 드래그해 둔 위치가 줌해도 그룹 대비 고정된다(OQ-1 해소).</summary>
    private bool IsNormalizedOffset => _marker is IImageEditableMarker || _marker is ILineEditableMarker;

    /// <summary>저장된 오프셋 쌍 — 이미지=U/V, 그 외=X/Y(라인=비율, 점 심볼=px). 의미는 IsNormalizedOffset가 규정.</summary>
    private (double a, double b) ReadOffset()
        => _marker is IImageEditableMarker img ? (img.LabelOffsetU, img.LabelOffsetV) : (_marker.LabelOffsetX, _marker.LabelOffsetY);

    /// <summary>오프셋 쌍 저장 — 이미지=U/V, 그 외=X/Y.</summary>
    private void WriteOffset(double a, double b)
    {
        if (_marker is IImageEditableMarker img) { img.LabelOffsetU = a; img.LabelOffsetV = b; }
        else { _marker.LabelOffsetX = a; _marker.LabelOffsetY = b; }
    }

    /// <summary>footprint 하단 기본위치 + 오프셋. 이미지·라인=비율(U/V, 줌 불변), 점 심볼=px. 라벨박스 중심 좌표.</summary>
    private Point LabelCenter()
    {
        var ic = IconCenter();
        var (hw, hh) = VisualHalfExtents();
        var (ox, oy) = ReadOffset();
        var rel = ComputeLabelCenterRel(hw, hh, ox, oy, isNormalized: IsNormalizedOffset);
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

            // 스타일 반영(FR-05·13 v2.5) + 렌더 캐시(FR-08) — 색=EnumColorType(심볼 색상 콤보 동형).
            // 기본 White/Black(α합성)은 종전 하드코딩(#F0F0F4F8/#CD1C1E22)과 사실상 동일 시각.
            double dpi = VisualTreeHelper.GetDpi(_map).PixelsPerDip;
            double maxW = EffectiveMaxWidth(_marker.TitleMaxWidth);
            if (_cachedFg == null || _cachedFgColor != _marker.TitleColor)
            { _cachedFgColor = _marker.TitleColor; _cachedFg = BrushFromColorType(_cachedFgColor, null); _cachedText = null; }
            if (_cachedBg == null || _cachedBgColor != _marker.TitleBackground)
            { _cachedBgColor = _marker.TitleBackground; _cachedBg = BrushFromColorType(_cachedBgColor, ChipAlpha); }
            if (_cachedText == null || Math.Abs(_cachedDpi - dpi) > 0.001)
            {
                var typeface = GetTypeface(_marker.TitleFontFamily, _marker.TitleBold, _marker.TitleItalic);
                _cachedText = new FormattedText(title, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    typeface, Math.Max(9d, _marker.TitleSize), _cachedFg, dpi)
                { MaxTextWidth = maxW, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                _cachedDpi = dpi;
            }
            var ft = _cachedText;

            var c = LabelCenter();
            // [Rotation 진동 하드닝] 라벨 박스 원점을 정수 픽셀에 스냅 — 회전 팬 중 앵커의
            // 서브픽셀 변동이 매 프레임 ClearType 텍스트 재래스터(shimmer)로 보이던 떨림 제거.
            // 1px 단위 이동은 아이콘 양자화와 동일 보폭이라 상대 정렬 유지.
            c = new Point(Math.Round(c.X), Math.Round(c.Y));
            double w = ft.WidthIncludingTrailingWhitespace + PadX * 2d;
            double h = ft.Height + PadY * 2d;
            var box = new Rect(c.X - w / 2d, c.Y - h / 2d, w, h);
            _labelRect = box;

            // 라벨만 드래그 중일 때만 점선 리더선(아이콘↔라벨) — 심볼 이동 시엔 라벨이 오프셋 유지로 따라올 뿐, 선 없음(FR-LB-04)
            if (_labelDragging)
                dc.DrawLine(_leaderPen, IconCenter(), c);

            // 테두리 없이 배경 칩만 — MarkerEditAdorner 편집박스와 혼동(adorner 박스 2개처럼 보임) 방지. 가독성용 배경만 유지.
            dc.DrawRoundedRectangle(_cachedBg, null, box, 3d, 3d);
            dc.DrawText(ft, new Point(box.X + PadX, box.Y + PadY));

            // 폭 조절 중: 최대폭 가이드(점선) — 텍스트가 짧아 박스가 안 자랄 때도 말줄임 한계선이 보이게(WYSIWYG, FR-13)
            if (_widthResizing)
            {
                var guide = new Rect(c.X - (maxW + PadX * 2d) / 2d, box.Y, maxW + PadX * 2d, box.Height);
                dc.DrawRectangle(null, _leaderPen, guide);
            }
        }
        catch (Exception ex) { _log?.Error($"LabelAdorner 렌더 실패: {ex.Message}"); }
    }

    // 라벨박스에서만 hit(그 외 투과, 불변식#3). MarkerEditAdorner 아이콘 핸들과 비겹침(라벨=아이콘 하단 오프셋).
    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => _map.IsEditMode && !_labelRect.IsEmpty && _labelRect.Contains(hitTestParameters.HitPoint)   // 편집모드일 때만 히트(그 외 클릭스루=이동 불가, L2)
            ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null!;

    /// <summary>폭 조절 스트립 폭 — min(6px, 박스폭 25%): 2글자 라벨(≈20px 박스)에서 이동 존 잠식 방지(2차 검증 W2-014).</summary>
    private static double StripWidth(Rect box) => Math.Min(6d, box.Width * 0.25);

    /// <summary>히트 존 판정 — 0=밖 / 1=내부(이동) / 2=좌 스트립(폭) / 3=우 스트립(폭). (FR-13/R-8 존 분리)</summary>
    private int HitZone(Point p)
    {
        if (_labelRect.IsEmpty || !_labelRect.Contains(p)) return 0;
        double s = StripWidth(_labelRect);
        if (p.X <= _labelRect.Left + s) return 2;
        if (p.X >= _labelRect.Right - s) return 3;
        return 1;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        int zone = _map.IsEditMode ? HitZone(e.GetPosition(this)) : 0;   // 라벨 편집은 맵 편집모드 ON일 때만(L2)
        if (zone > 0)
        {
            _grabScreen = e.GetPosition(_map);   // 델타는 맵컨트롤 공간(디지털줌 자동 역보정, 불변식#10)
            _origOffsetX = _marker.LabelOffsetX;
            _origOffsetY = _marker.LabelOffsetY;
            if (_marker is IImageEditableMarker imgB) { _origOffsetU = imgB.LabelOffsetU; _origOffsetV = imgB.LabelOffsetV; }   // 이미지 undo before=U/V(FR-02)
            if (zone == 1)
            {
                _labelDragging = true;
            }
            else
            {
                // 폭 조절(FR-13) — edge-pinned: 드래그 가장자리=커서 추종, 반대편 고정(오프셋 보상은 OnMouseMove)
                _widthResizing = true;
                _resizeRightEdge = zone == 3;
                _origMaxWidth = EffectiveMaxWidth(_marker.TitleMaxWidth);
            }
            CaptureMouse();
            e.Handled = true;
            InvalidateVisual();
        }
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        // 호버 커서 — 스트립 위=SizeWE(폭), 내부=SizeAll(이동). HitTestCore가 라벨박스 밖을 투과하므로 박스 위에서만 도달.
        if (!_labelDragging && !_widthResizing)
            Cursor = HitZone(e.GetPosition(this)) is 2 or 3 ? Cursors.SizeWE : Cursors.SizeAll;

        if (_widthResizing)
        {
            // FR-13 edge-pinned 폭 조절(2차 검증 W그룹: 유일 성립 모델) — 드래그 가장자리=커서 추종, 반대편 고정.
            // 폭 Δ의 절반만큼 라벨 중심(오프셋)을 드래그 방향으로 보상. 릴리스 시 (오프셋,폭) 쌍 원자 undo.
            var cur = e.GetPosition(_map);
            double dxw = cur.X - _grabScreen.X;
            double dw = _resizeRightEdge ? dxw : -dxw;
            double newW = Math.Clamp(_origMaxWidth + dw, WIDTH_MIN, WIDTH_MAX);
            double actualDw = newW - _origMaxWidth;
            double centerShift = (_resizeRightEdge ? 1 : -1) * actualDw / 2d;
            if (IsNormalizedOffset)
            {
                // 이미지·라인: 폭 Δ/2 보상도 비율 도메인에서(가로만 이동, 세로 오프셋 불변). 이미지=U, 라인=X(비율).
                var (hw, _) = VisualHalfExtents();
                double baseA = _marker is IImageEditableMarker ? _origOffsetU : _origOffsetX;
                double newA = (baseA * Math.Max(1e-6, hw) + centerShift) / Math.Max(1e-6, hw);
                if (_marker is IImageEditableMarker imgW) imgW.LabelOffsetU = newA;
                else _marker.LabelOffsetX = newA;
            }
            else
            {
                _marker.LabelOffsetX = _origOffsetX + centerShift;   // 점 심볼 px
            }
            _marker.TitleMaxWidth = newW;
            InvalidateVisual();
            e.Handled = true;
            base.OnMouseMove(e);
            return;
        }

        if (_labelDragging)
        {
            var cur = e.GetPosition(_map);
            double dx = cur.X - _grabScreen.X, dy = cur.Y - _grabScreen.Y;
            if (IsNormalizedOffset)
            {
                // 이미지·라인/PidsGroup(FR-02/03·OQ-1): px 델타를 하프익스텐트 비율(U/V)로 역산 저장.
                // 상한=3·max(hw,hh) px — 오프셋px와 상한이 2^Δz로 함께 스케일 → 비율은 줌 불변·등방.
                // 저장위치: 이미지=U/V, 라인=X/Y(비율). before는 down에서 캡처(_origOffsetU/V vs _origOffsetX/Y).
                var (hw, hh) = VisualHalfExtents();
                double baseA = _marker is IImageEditableMarker ? _origOffsetU : _origOffsetX;
                double baseB = _marker is IImageEditableMarker ? _origOffsetV : _origOffsetY;
                double nx = baseA * hw + dx, ny = baseB * hh + dy;
                double cap = Math.Max(hw, hh) * IMAGE_LABEL_RADIUS_MULT;
                double len = Math.Sqrt(nx * nx + ny * ny);
                if (len > cap && len > 0d) { nx *= cap / len; ny *= cap / len; }
                WriteOffset(nx / Math.Max(1e-6, hw), ny / Math.Max(1e-6, hh));
            }
            else
            {
                // 점 심볼: 고정 px 오프셋(고정 크기 아이콘이라 px가 줌 불변, FR-LB-04). 상한=반지름×4.
                const double SYMBOL_LABEL_RADIUS_MULT = 4.0;
                double nx = _origOffsetX + dx, ny = _origOffsetY + dy;
                double cap = Math.Max(_marker.Width, _marker.Height) / 2.0 * SYMBOL_LABEL_RADIUS_MULT;
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

    /// <summary>폭 조절 완료 통지 — (before오프셋A/B, before폭) 원자 전달(FR-13).</summary>
    private void RaiseWidthChanged()
    {
        if (_marker is IImageEditableMarker)
            LabelWidthChanged?.Invoke(_marker, _origOffsetU, _origOffsetV, _origMaxWidth);
        else
            LabelWidthChanged?.Invoke(_marker, _origOffsetX, _origOffsetY, _origMaxWidth);
    }

    /// <summary>드래그 종류별 종료 처리 공용 — Up/캡처손실 공유.</summary>
    private void FinishDrag()
    {
        if (_labelDragging)
        {
            _labelDragging = false;
            InvalidateVisual();
            RaiseOffsetChanged();   // lock 밖 발화 — 오프셋 DB 영속 + undo(before=드래그시작 오프셋)
        }
        else if (_widthResizing)
        {
            _widthResizing = false;
            InvalidateVisual();
            RaiseWidthChanged();    // (오프셋,폭) 쌍 영속+원자 undo(FR-13)
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_labelDragging || _widthResizing)
        {
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
            FinishDrag();
        }
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        FinishDrag();   // 캡처 손실 시에도 영속+undo(유실 방지)
        base.OnLostMouseCapture(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _labelDragging = false;     // ReleaseMouseCapture→OnLostMouseCapture 재진입 시 이벤트 발화 차단(P2-02)
        _widthResizing = false;
        LabelOffsetChanged = null;  // 캡처 해제 전에 구독 제거 — Dispose 중 발화 방지
        LabelWidthChanged = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        _map.OnMapZoomChanged -= OnMapChanged;
        _map.OnMapDrag -= OnMapChanged;
        _map.OnPositionChanged -= OnMapPositionChanged;
        if (_marker is INotifyPropertyChanged npc) npc.PropertyChanged -= OnMarkerPropertyChanged;
        _cachedText = null;   // 렌더 캐시 해제(FR-08 — 수명 규약)
        _cachedFg = null;
        _cachedBg = null;
    }

    /// <summary>Typeface 정적 공유 캐시 — 폰트/굵기/이탤릭 조합당 1회 생성(FR-08, 불변 값 객체라 공유 안전).</summary>
    internal static Typeface GetTypeface(string? family, bool bold, bool italic)
    {
        var key = (family ?? string.Empty, bold, italic);
        lock (_typefaceGate)
        {
            if (_typefaceCache.TryGetValue(key, out var tf)) return tf;
            tf = ResolveTypeface(family, bold, italic);
            _typefaceCache[key] = tf;
            return tf;
        }
    }

    /// <summary>유효 최대폭 — 0 이하(레거시/미설정)는 기본 200, 그 외 40~800 클램프(FR-13). 테스트 공유.</summary>
    internal static double EffectiveMaxWidth(double w)
        => w > 0 ? Math.Clamp(w, WIDTH_MIN, WIDTH_MAX) : DEFAULT_MAX_WIDTH;

    /// <summary>배경 칩 고정 알파(종전 하드코딩 0xCD 유지) — Black 선택 시 #CD212121 ≈ 종전 #CD1C1E22.</summary>
    internal const byte ChipAlpha = 0xCD;

    /// <summary>EnumColorType → Frozen 브러시(FillColor 파이프라인 동형, v2.5). Transparent=완전투명.
    /// overrideAlpha 지정 시(배경 칩) 해당 알파로 합성 — 글자색은 null(불투명).</summary>
    internal static Brush BrushFromColorType(EnumColorType colorType, byte? overrideAlpha)
    {
        if (colorType == EnumColorType.Transparent) return Brushes.Transparent;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(Helpers.ColorHelper.ToHexString(colorType));
            if (overrideAlpha.HasValue) c.A = overrideAlpha.Value;
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
        catch
        {
            return overrideAlpha.HasValue ? _bg : _fg;   // 변환 실패 폴백=종전 하드코딩 브러시
        }
    }

    /// <summary>폰트 패밀리+굵기+이탤릭 → Typeface. 빈값/비정상 폰트명은 Segoe UI 폴백(architect Q4 — 무음 폴백 UX 방지는 패널 콤보가 담당).</summary>
    internal static Typeface ResolveTypeface(string? family, bool bold, bool italic)
    {
        try
        {
            var fam = string.IsNullOrWhiteSpace(family) ? new FontFamily("Segoe UI") : new FontFamily(family);
            return new Typeface(fam, italic ? FontStyles.Italic : FontStyles.Normal,
                bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        }
        catch
        {
            return new Typeface("Segoe UI");
        }
    }

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
    private static Pen FrozenDashPen(Color c, double t)
    { var p = new Pen(Frozen(new SolidColorBrush(c)), t) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) }; p.Freeze(); return p; }
}
