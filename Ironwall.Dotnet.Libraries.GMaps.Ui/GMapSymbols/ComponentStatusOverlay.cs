using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/// <summary>
/// 장비 아이콘 위에 얹는 <b>부품 층</b> — 우하단 건강 배지 + 좌하단 문 표시 + 아이콘 아래 부품 칸 줄(L2).
/// 2D · 3D · 폴백 세 템플릿이 같은 요소 하나를 쓴다(R10).
/// </summary>
/// <remarks>
/// <para><b>이벤트가 항상 이긴다</b>(분석 §4-1): 이벤트는 우상단 <b>원</b> · 색 채움 · 깜빡임 · 펄스 링을 쓴다. 이 층은
/// 우하단 <b>둥근 사각 + 공구 글리프(+ 숫자)</b>로 <b>모양</b>이 다르고, 절대 깜빡이지 않으며(애니메이션 0건), 빨강을 쓰지 않는다.
/// 고장 = 채운 공구 · 1.5px 테두리 / 저하 = 속 빈 공구 · 1px 테두리 — 색이 아니라 모양으로 가른다.</para>
/// <para><b>테마</b>: 브러시는 전부 <c>SetResourceReference</c> 로 토큰을 물고 있어(Surface · StatusWarning · TextSecondary · TextPrimary)
/// 라이트/다크 전환 때 다시 해석되고 다시 그려진다. 1회 해석 · 정적 캐시 브러시는 쓰지 않는다.</para>
/// <para><b>칸 줄(L2, component-display-unify FR-04)</b>: 아이콘이 화면에 48px 이상일 때 아래에 대표 4칸 + "+n".
/// 정상 = 윤곽 · 가동 = 채움 · 고장 = 공구 표지 · 사용 안 함 = 사선. 지도가 크게 보이는 아이콘을 30개 넘게 세면
/// (<see cref="IsStripCrowdedProperty"/>) 고장 · 선택 · 호버한 아이콘만 그린다. 제목 라벨이 기본 자리를 덮으면 라벨 아래로 내린다.</para>
/// <para><b>비용</b>: 요소 하나 · <see cref="OnRender"/> 한 번. 값이 바뀔 때만 다시 그린다. 히트테스트 없음. 애니메이션 0.</para>
/// </remarks>
public sealed class ComponentStatusOverlay : FrameworkElement
{
    /// <summary>배지 한 변(px, 마커 좌표) — 우상단 이벤트 점(12px)과 같은 무게.</summary>
    internal const double BadgeSize = 12.0;
    /// <summary>배지가 아이콘 밖으로 걸치는 양 — 이벤트 점의 -6px 걸침과 대칭.</summary>
    internal const double Overhang = 4.0;

    // MDI "Wrench" (24×24) — 기하는 브러시가 아니므로 공유 고정해도 테마와 무관하다.
    private static readonly Geometry WrenchGeometry = CreateWrench();

    public ComponentStatusOverlay()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = true;
        SetResourceReference(SurfaceFillProperty, "SurfaceBrush");
        SetResourceReference(AlertBrushProperty, "StatusWarningBrush");
        SetResourceReference(NeutralBrushProperty, "TextSecondaryBrush");
        SetResourceReference(InkBrushProperty, "TextPrimaryBrush");
        SetResourceReference(ActiveBrushProperty, "PrimaryBrush");
        SetResourceReference(MutedBrushProperty, "TextMutedBrush");
    }

    #region Value DPs
    public static readonly DependencyProperty HealthProperty = DependencyProperty.Register(nameof(Health), typeof(ComponentHealthLevel),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(ComponentHealthLevel.None, FrameworkPropertyMetadataOptions.AffectsRender));
    public ComponentHealthLevel Health { get => (ComponentHealthLevel)GetValue(HealthProperty); set => SetValue(HealthProperty, value); }

    public static readonly DependencyProperty BadgeCountProperty = DependencyProperty.Register(nameof(BadgeCount), typeof(int),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public int BadgeCount { get => (int)GetValue(BadgeCountProperty); set => SetValue(BadgeCountProperty, value); }

    public static readonly DependencyProperty DoorProperty = DependencyProperty.Register(nameof(Door), typeof(DoorIndicatorKind),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(DoorIndicatorKind.None, FrameworkPropertyMetadataOptions.AffectsRender));
    public DoorIndicatorKind Door { get => (DoorIndicatorKind)GetValue(DoorProperty); set => SetValue(DoorProperty, value); }

    public static readonly DependencyProperty ScreenScaleProperty = DependencyProperty.Register(nameof(ScreenScale), typeof(double),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double ScreenScale { get => (double)GetValue(ScreenScaleProperty); set => SetValue(ScreenScaleProperty, value); }

    /// <summary>모양 표시(ShowShape) — false 면 아이콘과 함께 숨는다.</summary>
    public static readonly DependencyProperty ShowShapeProperty = DependencyProperty.Register(nameof(ShowShape), typeof(bool),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool ShowShape { get => (bool)GetValue(ShowShapeProperty); set => SetValue(ShowShapeProperty, value); }

    /// <summary>팔레트 미리보기 — 실시간 상태를 그리지 않는다.</summary>
    public static readonly DependencyProperty IsPreviewProperty = DependencyProperty.Register(nameof(IsPreview), typeof(bool),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsPreview { get => (bool)GetValue(IsPreviewProperty); set => SetValue(IsPreviewProperty, value); }

    /// <summary>부품 칸 줄(L2). 비었으면(6.3 · 미수신) 그리지 않는다.</summary>
    public static readonly DependencyProperty StripProperty = DependencyProperty.Register(nameof(Strip), typeof(ComponentStrip),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(ComponentStrip.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
    public ComponentStrip? Strip { get => (ComponentStrip?)GetValue(StripProperty); set => SetValue(StripProperty, value); }

    /// <summary>심볼의 화면 크기(px) — 마커 컨트롤이 심볼 크기 × 디지털 배율로 준다. NaN 이면 요소 크기 × <see cref="ScreenScale"/>.</summary>
    public static readonly DependencyProperty MarkerPixelsProperty = DependencyProperty.Register(nameof(MarkerPixels), typeof(double),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
    public double MarkerPixels { get => (double)GetValue(MarkerPixelsProperty); set => SetValue(MarkerPixelsProperty, value); }

    /// <summary>제목 라벨 상자(아이콘 중심 기준) — 칸 줄이 이 상자를 피해 아래로 내려간다. 라벨이 없으면 <see cref="Rect.Empty"/>.</summary>
    public static readonly DependencyProperty LabelBoxProperty = DependencyProperty.Register(nameof(LabelBox), typeof(Rect),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
    public Rect LabelBox { get => (Rect)GetValue(LabelBoxProperty); set => SetValue(LabelBoxProperty, value); }

    /// <summary>선택 · 호버 · 조립 카드 대상 — 밀집 중에도 칸 줄을 그린다.</summary>
    public static readonly DependencyProperty IsEmphasizedProperty = DependencyProperty.Register(nameof(IsEmphasized), typeof(bool),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsEmphasized { get => (bool)GetValue(IsEmphasizedProperty); set => SetValue(IsEmphasizedProperty, value); }

    /// <summary>
    /// 밀집 판정(상속) — 지도가 뷰포트가 바뀔 때 한 번 세어 각 심볼(<c>GMapPidsMarker.ComponentStripCrowded</c>)에 내려 주고,
    /// 마커 컨트롤이 자기에게 이 값을 걸면 템플릿 안의 부품 층으로 상속된다. 값이 바뀔 때만 다시 그린다(NFR-02).
    /// </summary>
    public static readonly DependencyProperty IsStripCrowdedProperty = DependencyProperty.RegisterAttached("IsStripCrowded", typeof(bool),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public static bool GetIsStripCrowded(DependencyObject d) => (bool)d.GetValue(IsStripCrowdedProperty);
    public static void SetIsStripCrowded(DependencyObject d, bool value) => d.SetValue(IsStripCrowdedProperty, value);
    #endregion

    #region Theme brush DPs (DynamicResource)
    public static readonly DependencyProperty SurfaceFillProperty = DependencyProperty.Register(nameof(SurfaceFill), typeof(Brush),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? SurfaceFill { get => (Brush?)GetValue(SurfaceFillProperty); set => SetValue(SurfaceFillProperty, value); }

    public static readonly DependencyProperty AlertBrushProperty = DependencyProperty.Register(nameof(AlertBrush), typeof(Brush),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? AlertBrush { get => (Brush?)GetValue(AlertBrushProperty); set => SetValue(AlertBrushProperty, value); }

    public static readonly DependencyProperty NeutralBrushProperty = DependencyProperty.Register(nameof(NeutralBrush), typeof(Brush),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? NeutralBrush { get => (Brush?)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }

    public static readonly DependencyProperty InkBrushProperty = DependencyProperty.Register(nameof(InkBrush), typeof(Brush),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? InkBrush { get => (Brush?)GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    /// <summary>가동 칸 채움 — <c>PrimaryBrush</c>(선택 표지와 같은 색이지만 모양(작은 채운 사각)으로 가른다).</summary>
    public static readonly DependencyProperty ActiveBrushProperty = DependencyProperty.Register(nameof(ActiveBrush), typeof(Brush),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? ActiveBrush { get => (Brush?)GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }

    /// <summary>미상 칸 윤곽 — <c>TextMutedBrush</c>.</summary>
    public static readonly DependencyProperty MutedBrushProperty = DependencyProperty.Register(nameof(MutedBrush), typeof(Brush),
        typeof(ComponentStatusOverlay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? MutedBrush { get => (Brush?)GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    #endregion

    /// <summary>시험용 — 지금 크기 · 배율에서 배지를 그리는가.</summary>
    internal bool IsBadgeDrawn => ShowShape && !IsPreview
        && ComponentBadgeLod.ShowsBadge(Health, CurrentScreenPixels());

    /// <summary>시험용 — 지금 크기 · 배율에서 문 표시를 그리는가.</summary>
    internal bool IsDoorDrawn => ShowShape && !IsPreview
        && ComponentBadgeLod.ShowsDoor(Door, CurrentScreenPixels());

    /// <summary>
    /// 심볼의 화면 크기 — 마커 컨트롤이 준 값(<see cref="MarkerPixels"/> = 심볼 크기 × 디지털 배율, 지도 밀도 판정과 같은 함수)을 쓰고,
    /// 없으면(단독 사용) 요소 크기 × <see cref="ScreenScale"/>.
    /// </summary>
    internal double CurrentScreenPixels()
        => double.IsNaN(MarkerPixels) ? ComponentBadgeLod.ScreenPixels(ActualWidth, ActualHeight, ScreenScale) : MarkerPixels;

    /// <summary>시험용 — 칸 줄 윗변(요소 좌표). 라벨이 기본 자리를 덮으면 라벨 아래.</summary>
    internal double StripTopFor(double w, double h, double stripWidth)
    {
        var defaultStrip = new Rect((w - stripWidth) / 2, h + Overhang + StripGap, Math.Max(stripWidth, 0), ChipSize);
        var label = LabelBox;
        if (!label.IsEmpty) label.Offset(w / 2, h / 2);   // 라벨 상자는 아이콘 중심 기준으로 온다
        return ComponentStripRules.StripTop(defaultStrip, label, StripGap);
    }

    /// <summary>시험용 — 지금 크기 · 배율 · 밀집에서 칸 줄을 그리는가.</summary>
    internal bool IsStripDrawn => ShowShape && !IsPreview
        && ComponentStripRules.ShowsStrip(Strip, CurrentScreenPixels(),
            GetIsStripCrowded(this), IsEmphasized);

    protected override void OnRender(DrawingContext dc)
    {
        if (!ShowShape || IsPreview) return;
        double w = ActualWidth, h = ActualHeight;
        var px = CurrentScreenPixels();

        // 토큰이 병합되지 않은 곳(오프스크린 등)에서도 형태는 보이게 — 시스템 기본 브러시로 폴백한다(캐시하지 않음).
        var surface = SurfaceFill ?? Brushes.White;
        var alert = AlertBrush ?? Brushes.DarkGoldenrod;
        var neutral = NeutralBrush ?? Brushes.DimGray;
        var ink = InkBrush ?? Brushes.Black;

        if (ComponentBadgeLod.ShowsBadge(Health, px)) DrawBadge(dc, w, h, surface, alert);
        if (ComponentBadgeLod.ShowsDoor(Door, px)) DrawDoor(dc, h, surface, neutral, ink);
        if (ComponentStripRules.ShowsStrip(Strip, px, GetIsStripCrowded(this), IsEmphasized))
            DrawStrip(dc, w, h, Strip!, surface, alert, neutral, ActiveBrush ?? Brushes.SteelBlue, MutedBrush ?? Brushes.Gray);
    }

    /// <summary>칸 한 변(px, 마커 좌표). 48px 이상일 때만 그리므로 화면에서는 10px 남짓.</summary>
    internal const double ChipSize = 8.0;
    /// <summary>칸 사이 간격.</summary>
    internal const double ChipGap = 2.0;
    /// <summary>아이콘 아래 모서리(배지 걸침 포함)에서 줄까지의 틈.</summary>
    internal const double StripGap = 2.0;

    /// <summary>
    /// 아이콘 아래 가운데 칸 줄 — 정상 = 윤곽 · 가동 = 채움 · 고장 = 경고 테두리 + 채운 공구 · 저하 = 경고 테두리 + 빈 공구 ·
    /// 미상 = 흐린 점선 윤곽 · 사용 안 함 = 사선. 남은 부품은 "+n". 원형 점 · 빨강 · 깜빡임은 쓰지 않는다(이벤트 몫).
    /// </summary>
    private void DrawStrip(DrawingContext dc, double w, double h, ComponentStrip strip, Brush surface, Brush alert, Brush neutral, Brush active, Brush muted)
    {
        FormattedText? more = strip.MoreCount > 0
            ? new FormattedText("+" + strip.MoreCount.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 7.5, neutral,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            : null;

        int n = strip.Chips.Count;
        double total = n * ChipSize + Math.Max(0, n - 1) * ChipGap + (more == null ? 0 : ChipGap + more.Width);
        double x = (w - total) / 2;
        double y = StripTopFor(w, h, total);

        var outline = new Pen(neutral, 1.0);
        foreach (var chip in strip.Chips)
        {
            var rect = new Rect(x + 0.5, y + 0.5, ChipSize - 1, ChipSize - 1);
            switch (chip.Kind)
            {
                case ComponentChipKind.Active:
                    dc.DrawRoundedRectangle(active, new Pen(active, 1.0), rect, 1.5, 1.5);
                    break;
                case ComponentChipKind.Fault:
                case ComponentChipKind.Degraded:
                    bool fault = chip.Kind == ComponentChipKind.Fault;
                    dc.DrawRoundedRectangle(surface, new Pen(alert, fault ? 1.4 : 1.0), rect, 1.5, 1.5);
                    const double glyph = 6.0;
                    var transform = new TransformGroup();
                    transform.Children.Add(new ScaleTransform(glyph / 24.0, glyph / 24.0));
                    transform.Children.Add(new TranslateTransform(x + (ChipSize - glyph) / 2, y + (ChipSize - glyph) / 2));
                    dc.PushTransform(transform);
                    if (fault) dc.DrawGeometry(alert, null, WrenchGeometry);
                    else dc.DrawGeometry(null, new Pen(alert, 3.0), WrenchGeometry);
                    dc.Pop();
                    break;
                case ComponentChipKind.OutOfService:
                    dc.DrawRoundedRectangle(surface, outline, rect, 1.5, 1.5);
                    dc.DrawLine(outline, new Point(rect.Left + 1, rect.Bottom - 1), new Point(rect.Right - 1, rect.Top + 1));   // 사선
                    break;
                case ComponentChipKind.Unknown:
                    dc.DrawRoundedRectangle(surface, new Pen(muted, 1.0) { DashStyle = new DashStyle(new[] { 1.0, 1.0 }, 0) }, rect, 1.5, 1.5);
                    break;
                default:
                    dc.DrawRoundedRectangle(surface, outline, rect, 1.5, 1.5);
                    break;
            }
            x += ChipSize + ChipGap;
        }

        if (more != null)
            dc.DrawText(more, new Point(x, y + (ChipSize - more.Height) / 2));
    }

    private void DrawBadge(DrawingContext dc, double w, double h, Brush surface, Brush alert)
    {
        bool fault = Health == ComponentHealthLevel.Fault;
        var countText = BadgeCount >= 2 ? (BadgeCount > 9 ? "9+" : BadgeCount.ToString(CultureInfo.InvariantCulture)) : null;
        FormattedText? count = countText == null ? null : new FormattedText(countText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 8.5, alert, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double badgeW = BadgeSize + (count == null ? 0 : count.Width + 2);
        var rect = new Rect(w - badgeW + Overhang, h - BadgeSize + Overhang, badgeW, BadgeSize);
        var border = new Pen(alert, fault ? 1.5 : 1.0);
        dc.DrawRoundedRectangle(surface, border, rect, 3, 3);

        // 공구 글리프 — 고장=채움, 저하=속 빈 윤곽(모양 차이).
        const double glyph = 8.0;
        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(glyph / 24.0, glyph / 24.0));
        transform.Children.Add(new TranslateTransform(rect.X + (BadgeSize - glyph) / 2, rect.Y + (BadgeSize - glyph) / 2));
        dc.PushTransform(transform);
        if (fault) dc.DrawGeometry(alert, null, WrenchGeometry);
        else dc.DrawGeometry(null, new Pen(alert, 2.4), WrenchGeometry);   // 24 좌표계에서 2.4 = 화면 0.8px
        dc.Pop();

        if (count != null)
            dc.DrawText(count, new Point(rect.X + BadgeSize - 1, rect.Y + (BadgeSize - count.Height) / 2));
    }

    private static void DrawDoor(DrawingContext dc, double h, Brush surface, Brush neutral, Brush ink, DoorIndicatorKind kind)
    {
        var rect = new Rect(-Overhang, h - BadgeSize + Overhang, BadgeSize, BadgeSize);
        dc.DrawRoundedRectangle(surface, new Pen(neutral, 1.0), rect, 3, 3);
        var frame = new Rect(rect.X + 3.5, rect.Y + 2, 5, 8);
        var pen = new Pen(ink, 1.0);
        switch (kind)
        {
            case DoorIndicatorKind.Closed:
                dc.DrawRectangle(ink, null, frame);                                             // 꽉 찬 문짝
                break;
            case DoorIndicatorKind.Open:
                dc.DrawRectangle(null, pen, frame);                                             // 빈 문틀
                var leaf = new StreamGeometry();                                                // 비스듬히 열린 문짝
                using (var g = leaf.Open())
                {
                    g.BeginFigure(new Point(frame.Left, frame.Top), true, true);
                    g.LineTo(new Point(frame.Left + 3.2, frame.Top + 1.4), true, false);
                    g.LineTo(new Point(frame.Left + 3.2, frame.Bottom + 1.0), true, false);
                    g.LineTo(new Point(frame.Left, frame.Bottom), true, false);
                }
                leaf.Freeze();
                dc.DrawGeometry(ink, null, leaf);
                break;
            case DoorIndicatorKind.Running:
                var arrows = new StreamGeometry();                                              // ◀ ▶ 좌우로 움직이는 중
                using (var g = arrows.Open())
                {
                    double cy = rect.Y + BadgeSize / 2, l = rect.X + 1.8, r = rect.Right - 1.8, cx = rect.X + BadgeSize / 2;
                    g.BeginFigure(new Point(l, cy), true, true);
                    g.LineTo(new Point(cx - 0.6, cy - 3.2), true, false);
                    g.LineTo(new Point(cx - 0.6, cy + 3.2), true, false);
                    g.BeginFigure(new Point(r, cy), true, true);
                    g.LineTo(new Point(cx + 0.6, cy - 3.2), true, false);
                    g.LineTo(new Point(cx + 0.6, cy + 3.2), true, false);
                }
                arrows.Freeze();
                dc.DrawGeometry(ink, null, arrows);
                break;
            default:                                                                             // 모름 — 빈 문틀만(점선 · 색 없이)
                dc.DrawRectangle(null, new Pen(neutral, 1.0), frame);
                dc.DrawEllipse(neutral, null, new Point(frame.Left + frame.Width / 2, frame.Top + frame.Height / 2), 0.9, 0.9);
                break;
        }
    }

    private void DrawDoor(DrawingContext dc, double h, Brush surface, Brush neutral, Brush ink) => DrawDoor(dc, h, surface, neutral, ink, Door);

    private static Geometry CreateWrench()
    {
        var geometry = Geometry.Parse("M22.7,19L13.6,9.9C14.5,7.6 14,4.9 12.1,3C10.1,1 7.1,0.6 4.7,1.7L9,6L6,9L1.6,4.7C0.4,7.1 0.9,10.1 2.9,12.1C4.8,14 7.5,14.5 9.8,13.6L18.9,22.7C19.3,23.1 19.9,23.1 20.3,22.7L22.6,20.4C23.1,20 23.1,19.3 22.7,19Z");
        geometry.Freeze();
        return geometry;
    }
}
