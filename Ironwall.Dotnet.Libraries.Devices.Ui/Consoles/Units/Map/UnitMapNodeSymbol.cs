using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System;
using System.Windows;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 노드의 기호 — 틀 · 표지 · ★ · ▲ · 핀 · 괄호를 요소 하나가 직접 그린다 (NFR-01)
   Created By   : GHLee
   Created On   : 9/29/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>노드 기호 요소가 그리는 몫 — 모르는 제대의 "?" 글(<c>ConsoleText</c>)을 틀 위 · 배지 아래에 두려고 둘로 나눈다.</summary>
public enum UnitMapSymbolPart
{
    /// <summary>전부(L0 — "?" 글이 없다).</summary>
    All,

    /// <summary>틀 바탕 · 틀 · 중지(사선 해치 / L0 사선) · 제대 표지(선 · 점).</summary>
    Base,

    /// <summary>★ 내 부대 · ▲ 오류(L0 · L1) · 핀 옮김(L1 · L2) · 선택 괄호(L0 · L1).</summary>
    Badges,
}

/// <summary>
/// 노드 템플릿의 <c>Path</c> 여러 개(L1 기준 9개 + 각자 <c>Visuals.*</c> 바인딩)를 대신해 같은 도형을 같은 순서 · 같은 펜으로 그리는 요소 하나.
/// </summary>
/// <remarks>
/// <para><b>왜</b>(2026-09-29 NFR-01 분해): 노드마다 <c>{Binding Visuals.FrameGeometry, RelativeSource=TemplatedParent}</c> 같은 두 단계 경로 바인딩이
/// 6~9개였다. 둘째 단계가 알림 없는 CLR 속성이라 WPF 가 반사로 풀고 값 변화 구독까지 단다 — 노드 200개 템플릿 입히기에서 경로 층이
/// 레이아웃 시간의 절반을 먹었다(L1 126 → 61 ms, 경로 제거 실험). 이 요소는 템플릿 바인딩(<c>TemplateBinding</c>) 몇 개와 토큰 몇 개만 받는다.</para>
/// <para><b>그림이 같다</b>: <c>Shape.OnRender</c> 와 같은 호출(<see cref="DrawingContext.DrawGeometry"/>) · 같은 기하(노드의 얼린 <see cref="UnitMapNodeVisuals"/>) ·
/// 같은 펜 값(굵기 · 끝 모양 · 모서리 · 한계)으로 같은 순서로 그린다. 스냅숏 픽셀 비교로 확인한다(<c>device-console-preview --units-map --snapshot</c>).</para>
/// <para><b>픽셀 맞춤까지 같다</b>: 옛 <c>Path</c> 는 템플릿 뿌리의 <c>SnapsToDevicePixels=True</c> 를 물려받아(FrameworkElement 에서 상속 속성)
/// <b>제 크기</b>(펜 포함 자연 크기 · 레이아웃 반올림)의 가장자리 <c>{0, W} × {0, H}</c> 에 픽셀 기준선을 달았다 — 100% 가 아닌 배율에서 가장자리
/// 번짐이 그 기준선을 따른다. 그래서 이 요소는 제 기준선을 끄고(<see cref="UIElement.SnapsToDevicePixels"/> = false) 도형마다 옛 <c>Path</c> 가
/// 가졌을 기준선을 <see cref="DrawingContext.PushGuidelineSet"/> 으로 되살린다(<c>UnitMapNodeRenderCostTests</c> 가 96 · 144 dpi 로 대조).</para>
/// <para><b>색은 토큰</b>: 브러시는 전부 템플릿의 <c>DynamicResource</c> 로 받는다 — 테마가 바뀌면 속성이 바뀌어 다시 그린다(NFR-07).
/// 정적 · 얼린 브러시를 두지 않는다. 해치 브러시도 그릴 때마다 그 순간의 토큰으로 새로 만든다.</para>
/// </remarks>
public sealed class UnitMapNodeSymbol : FrameworkElement
{
    // 템플릿이 쓰던 펜 굵기 — 값을 바꾸면 그림이 바뀐다(스냅숏 비교).
    private const double L0_FRAME_STROKE = 1.2;
    private const double FRAME_STROKE = 1.5;
    private const double MARK_STROKE = 1.5;
    private const double STAR_STROKE = 0.8;
    private const double PIN_STROKE = 1.4;
    private const double BRACKET_STROKE = 2.0;
    private const double HATCH_TILE = 7.0;

    private static readonly Geometry s_hatchLine = FrozenLine();

    public UnitMapNodeSymbol()
    {
        // 제 기준선(0 × 0 크기)은 끈다 — 도형마다 옛 Path 의 기준선을 직접 단다(OnRender).
        SnapsToDevicePixels = false;
    }

    #region - 모양 -
    public static readonly DependencyProperty VisualsProperty = DependencyProperty.Register(
        nameof(Visuals), typeof(UnitMapNodeVisuals), typeof(UnitMapNodeSymbol),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>노드의 (단계, 제대) 도형 한 벌 — 단계도 여기서 읽는다.</summary>
    public UnitMapNodeVisuals? Visuals { get => (UnitMapNodeVisuals?)GetValue(VisualsProperty); set => SetValue(VisualsProperty, value); }

    public static readonly DependencyProperty PartProperty = DependencyProperty.Register(
        nameof(Part), typeof(UnitMapSymbolPart), typeof(UnitMapNodeSymbol),
        new FrameworkPropertyMetadata(UnitMapSymbolPart.All, FrameworkPropertyMetadataOptions.AffectsRender));

    public UnitMapSymbolPart Part { get => (UnitMapSymbolPart)GetValue(PartProperty); set => SetValue(PartProperty, value); }
    #endregion

    #region - 상태 -
    public static readonly DependencyProperty IsSuspendedProperty = Flag(nameof(IsSuspended));
    public bool IsSuspended { get => (bool)GetValue(IsSuspendedProperty); set => SetValue(IsSuspendedProperty, value); }

    public static readonly DependencyProperty IsMineProperty = Flag(nameof(IsMine));
    public bool IsMine { get => (bool)GetValue(IsMineProperty); set => SetValue(IsMineProperty, value); }

    public static readonly DependencyProperty HasErrorsProperty = Flag(nameof(HasErrors));
    public bool HasErrors { get => (bool)GetValue(HasErrorsProperty); set => SetValue(HasErrorsProperty, value); }

    public static readonly DependencyProperty IsMovedProperty = Flag(nameof(IsMoved));
    public bool IsMoved { get => (bool)GetValue(IsMovedProperty); set => SetValue(IsMovedProperty, value); }

    public static readonly DependencyProperty IsSelectedNodeProperty = Flag(nameof(IsSelectedNode));
    public bool IsSelectedNode { get => (bool)GetValue(IsSelectedNodeProperty); set => SetValue(IsSelectedNodeProperty, value); }
    #endregion

    #region - 토큰(템플릿이 DynamicResource 로 준다) -
    /// <summary>틀 바탕 · ★ 테두리 — <c>SurfaceAltBrush</c>.</summary>
    public static readonly DependencyProperty SurfaceBrushProperty = Paint(nameof(SurfaceBrush));
    public Brush? SurfaceBrush { get => (Brush?)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    /// <summary>틀 틴트 — <c>TintInfoBrush</c>.</summary>
    public static readonly DependencyProperty TintBrushProperty = Paint(nameof(TintBrush));
    public Brush? TintBrush { get => (Brush?)GetValue(TintBrushProperty); set => SetValue(TintBrushProperty, value); }

    /// <summary>틀 선 · 표지 · L0 사선 — <c>TextPrimaryBrush</c>.</summary>
    public static readonly DependencyProperty InkBrushProperty = Paint(nameof(InkBrush));
    public Brush? InkBrush { get => (Brush?)GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    /// <summary>중지 해치 선 — <c>TextMutedBrush</c>.</summary>
    public static readonly DependencyProperty HatchBrushProperty = Paint(nameof(HatchBrush));
    public Brush? HatchBrush { get => (Brush?)GetValue(HatchBrushProperty); set => SetValue(HatchBrushProperty, value); }

    /// <summary>★ — <c>PrimaryBrush</c>.</summary>
    public static readonly DependencyProperty AccentBrushProperty = Paint(nameof(AccentBrush));
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <summary>▲ — <c>StatusCriticalBrush</c>.</summary>
    public static readonly DependencyProperty CriticalBrushProperty = Paint(nameof(CriticalBrush));
    public Brush? CriticalBrush { get => (Brush?)GetValue(CriticalBrushProperty); set => SetValue(CriticalBrushProperty, value); }

    /// <summary>핀 — <c>TextSecondaryBrush</c>.</summary>
    public static readonly DependencyProperty PinBrushProperty = Paint(nameof(PinBrush));
    public Brush? PinBrush { get => (Brush?)GetValue(PinBrushProperty); set => SetValue(PinBrushProperty, value); }

    /// <summary>선택 괄호 — <c>SelectionBrush</c>.</summary>
    public static readonly DependencyProperty SelectionBrushProperty = Paint(nameof(SelectionBrush));
    public Brush? SelectionBrush { get => (Brush?)GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    #endregion

    /// <summary>
    /// 옛 템플릿의 <c>Path</c> 순서 그대로 그린다. 한 줄 = 옛 <c>Path</c> 하나(보이지 않던 <c>Path</c> 는 건너뛴다).
    /// </summary>
    protected override void OnRender(DrawingContext dc)
    {
        if (Visuals is not { } v) return;
        var level = v.Shape.Level;
        var dpi = VisualTreeHelper.GetDpi(this);
        void Draw(Brush? fill, Brush? stroke, double thickness, Geometry? geometry, PenLineCap cap = PenLineCap.Flat)
            => DrawLikePath(dc, v, dpi, fill, stroke, thickness, cap, geometry);

        if (Part != UnitMapSymbolPart.Badges)
        {
            Draw(SurfaceBrush, null, 0, v.FrameGeometry);                                                 // 틀 바탕
            Draw(TintBrush, InkBrush, level == UnitMapLevel.L0 ? L0_FRAME_STROKE : FRAME_STROKE, v.FrameGeometry);
            if (level == UnitMapLevel.L0)
            {
                if (IsSuspended) Draw(null, InkBrush, L0_FRAME_STROKE, v.SlashGeometry);                   // 중지 사선
            }
            else
            {
                if (IsSuspended) Draw(Hatch(HatchBrush), null, 0, v.FrameGeometry);                        // 중지 해치
                Draw(null, InkBrush, MARK_STROKE, v.MarkStrokeGeometry, PenLineCap.Square);                // 제대 표지 선
                Draw(InkBrush, null, 0, v.MarkDotGeometry);                                                // 제대 표지 점
            }
        }

        if (Part != UnitMapSymbolPart.Base)
        {
            if (IsMine) Draw(AccentBrush, SurfaceBrush, STAR_STROKE, v.StarGeometry);                       // ★
            if (level != UnitMapLevel.L2 && HasErrors) Draw(CriticalBrush, null, 0, v.ErrorGeometry);       // ▲(L2 는 글 줄에)
            if (level != UnitMapLevel.L0 && IsMoved) Draw(null, PinBrush, PIN_STROKE, v.PinGeometry);       // 핀
            if (level != UnitMapLevel.L2 && IsSelectedNode) Draw(null, SelectionBrush, BRACKET_STROKE, v.BracketsGeometry);   // 괄호
        }
    }

    /// <summary>
    /// 옛 <c>Path</c> 하나(<c>Stretch=None</c>, 캔버스 (0,0), 뿌리에서 물려받은 <c>SnapsToDevicePixels=True</c>)가 그렸을 그대로 그린다 —
    /// 그 <c>Path</c> 의 크기(<c>RenderSize</c> = 자연 크기를 레이아웃 반올림)로 <c>{0, W} × {0, H}</c> 기준선을 달고 같은 호출을 한다.
    /// </summary>
    private static void DrawLikePath(DrawingContext dc, UnitMapNodeVisuals visuals, DpiScale dpi, Brush? fill, Brush? stroke,
                                     double thickness, PenLineCap cap, Geometry? geometry)
    {
        var pen = Stroke(stroke, thickness, cap);
        if (geometry is null || fill is null && pen is null) return;       // Path: 채움도 펜도 없으면 그림 없음

        // Shape.GetNaturalSize 는 Stroke 가 있을 때만 펜을 넣는다(브러시 없는 Path 는 펜 없음).
        var natural = visuals.NaturalSize(geometry, pen is null ? 0 : thickness, cap);
        var width = RoundLayout(natural.Width, dpi.DpiScaleX);
        var height = RoundLayout(natural.Height, dpi.DpiScaleY);
        dc.PushGuidelineSet(new GuidelineSet(new[] { 0.0, width }, new[] { 0.0, height }));
        dc.DrawGeometry(fill, pen, geometry);
        dc.Pop();
    }

    /// <summary><c>UIElement.RoundLayoutValue</c> 와 같은 식(레이아웃 반올림 — 100% 는 정수, 그 밖은 장치 픽셀 격자).</summary>
    private static double RoundLayout(double value, double dpiScale)
    {
        if (Math.Abs(dpiScale - 1.0) < 1e-9) return Math.Round(value);
        var rounded = Math.Round(value * dpiScale) / dpiScale;
        return double.IsNaN(rounded) || double.IsInfinity(rounded) ? value : rounded;
    }

    /// <summary><c>Shape.GetPen</c> 과 같은 값 — 끝 모양 · 파선 끝은 기본 Flat, 모서리 Miter, 한계 10.</summary>
    private static Pen? Stroke(Brush? brush, double thickness, PenLineCap cap = PenLineCap.Flat)
        => brush is null ? null : new Pen(brush, thickness)
        {
            StartLineCap = cap,
            EndLineCap = cap,
            DashCap = PenLineCap.Flat,
            LineJoin = PenLineJoin.Miter,
            MiterLimit = 10.0,
        };

    /// <summary>옛 템플릿의 해치 <c>DrawingBrush</c> 그대로(7×7 타일 · 45° · 가운데 세로선 1px) — 그 순간의 토큰으로 새로 만든다.</summary>
    private static Brush? Hatch(Brush? ink)
        => ink is null ? null : new DrawingBrush(new GeometryDrawing(null, new Pen(ink, 1.0), s_hatchLine))
        {
            Stretch = Stretch.None,
            TileMode = TileMode.Tile,
            Viewbox = new Rect(0, 0, HATCH_TILE, HATCH_TILE),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, HATCH_TILE, HATCH_TILE),
            ViewportUnits = BrushMappingMode.Absolute,
            Transform = new RotateTransform(45),
        };

    // 기하만 얼린다(브러시 아님 — 테마와 무관).
    private static Geometry FrozenLine()
    {
        var line = Geometry.Parse("M3.5,0 L3.5,7");
        line.Freeze();
        return line;
    }

    private static DependencyProperty Flag(string name) => DependencyProperty.Register(
        name, typeof(bool), typeof(UnitMapNodeSymbol), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private static DependencyProperty Paint(string name) => DependencyProperty.Register(
        name, typeof(Brush), typeof(UnitMapNodeSymbol), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
}
