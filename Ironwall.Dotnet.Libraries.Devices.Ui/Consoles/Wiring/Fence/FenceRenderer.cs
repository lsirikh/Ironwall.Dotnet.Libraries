using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 그림 목록(<see cref="FenceShape"/>) → <see cref="DrawingContext"/>. 잉크 → 색은 <b>그릴 때마다</b> 테마 토큰에서 다시 푼다
/// (<c>TryFindResource</c> · 캐시 · Frozen 정적 브러시 없음 — 테마 전환 뒤 옛 색으로 굳지 않게, drag-first-ux 규칙).
/// </summary>
/// <remarks>
/// 목업 CSS 의 토큰 대응: 기둥 = <c>DividerBrush</c>(옆면은 어둡게 · 윗면은 밝게 섞음), 제품 올리브 = <c>ChartSeries4Brush</c> 40% + 기둥색,
/// 스마트 제어기 카드 = <c>TextPrimaryBrush</c> 와 <c>BgBrush</c> 중 어두운 쪽. 상태색(Normal · Accent)은 쓰지 않는다.
/// </remarks>
public sealed class FenceRenderer
{
    private const string BODY_FONT = "Malgun Gothic, Segoe UI";
    private const string MONO_FONT = "Cascadia Mono, Consolas, Malgun Gothic";

    private readonly FrameworkElement _source;
    private readonly Dictionary<string, Color> _colors = new();
    private readonly double _dpi;

    private FenceRenderer(FrameworkElement source)
    {
        _source = source;
        _dpi = VisualTreeHelper.GetDpi(source).PixelsPerDip;
    }

    /// <summary>이번 그리기의 팔레트를 토큰에서 새로 풀어 그린다.</summary>
    public static void Draw(DrawingContext dc, FrameworkElement source, IEnumerable<FenceShape> shapes, Geometry? rangeClip = null)
    {
        var r = new FenceRenderer(source);
        foreach (var shape in shapes) r.DrawOne(dc, shape, rangeClip);
    }

    /// <summary>사람이 고른 색 글자(#RRGGBB) → 색. 읽을 수 없으면 <c>null</c>(잉크 기본색).</summary>
    internal static Color? ParseColor(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return (Color)ColorConverter.ConvertFromString(text); }
        catch (FormatException) { return null; }
    }

    /// <summary>시험용 — 잉크의 채움색(없으면 <c>null</c>).</summary>
    internal static Color? FillColorOf(FrameworkElement source, FenceInk ink)
        => (new FenceRenderer(source).Style(ink).Fill as SolidColorBrush)?.Color;

    #region - Palette -
    private Color Token(string key, string fallback)
    {
        if (_colors.TryGetValue(key, out var c)) return c;
        c = _source.TryFindResource(key) is SolidColorBrush b ? b.Color : (Color)ColorConverter.ConvertFromString(fallback);
        _colors[key] = c;
        return c;
    }

    private Color Bg => Token("BgBrush", "#DDE3EB");
    private Color Surface => Token("SurfaceBrush", "#F2F5F9");
    private Color Alt => Token("SurfaceAltBrush", "#FFFFFF");
    private Color Sunken => Token("SurfaceSunkenBrush", "#E2E8F0");
    private Color Hover => Token("SurfaceHoverBrush", "#D8E0EA");
    private Color Pressed => Token("SurfacePressedBrush", "#C5D2E0");
    private Color Border => Token("BorderBrush", "#6B7C90");
    private Color Divider => Token("DividerBrush", "#7D8C9E");
    private Color RowLine => Token("RowLineBrush", "#AAB7C7");
    private Color Tx1 => Token("TextPrimaryBrush", "#13202C");
    private Color Tx2 => Token("TextSecondaryBrush", "#3D4D5C");
    private Color Tx3 => Token("TextMutedBrush", "#5E6B79");
    private Color Primary => Token("PrimaryBrush", "#0C6B89");
    private Color OnPrimary => Token("OnPrimaryBrush", "#FFFFFF");
    private Color Critical => Token("StatusCriticalBrush", "#C62121");
    private Color Warning => Token("StatusWarningBrush", "#B26A00");
    private Color Info => Token("StatusInfoBrush", "#15589F");
    private Color Selection => Token("SelectionBrush", "#0C6B89");
    private Color TintInfo => Token("TintInfoBrush", "#2215589F");
    private Color Series4 => Token("ChartSeries4Brush", "#257A2E");
    private Color TintAccent => Token("TintAccentBrush", "#220C6B89");

    // 펜스 재질(fence-wiring-editor NFR-05) — 라이트/다크 쌍 토큰. 토큰이 없는 창(미리보기 · 시험)에서는 라이트 값.
    private Color Brick => Token("FenceBrickBrush", "#A0522D");
    private Color Mortar => Token("FenceBrickMortarBrush", "#D9CFC4");
    private Color Concrete => Token("FenceConcreteBrush", "#B4B3AC");
    private Color ConcreteSeamColor => Token("FenceConcreteSeamBrush", "#8C8B84");
    private Color Design => Token("FenceDesignBrush", "#2F7D4A");
    private Color RazorColor => Token("FenceRazorBrush", "#5E6B79");
    private Color Accent => Token("AccentBrush", "#B25A06");

    /// <summary>칠 위 글자색 — 글자 토큰(진한 쪽)과 바탕 토큰(밝은 쪽) 중 대비가 큰 것.</summary>
    private Color On(Color fill) => Math.Abs(Luma(fill) - Luma(Tx1)) >= Math.Abs(Luma(fill) - Luma(Surface)) ? Tx1 : Surface;

    private Color Post => Divider;
    private Color PostSide => Mix(Divider, Colors.Black, 0.18);
    private Color PostTop => Mix(Divider, Colors.White, 0.30);
    private Color Olive => Mix(Post, Series4, 0.40);
    private Color OliveSide => Mix(PostSide, Series4, 0.40);
    private Color OliveTop => Mix(PostTop, Series4, 0.30);
    private Color CardSmart => Luma(Tx1) < Luma(Bg) ? Tx1 : Bg;
    private Color CardSmartText => Luma(Surface) > Luma(Tx1) ? Surface : Tx1;

    /// <summary>a 에서 b 쪽으로 t 만큼(0…1) 섞는다 — 알파도 섞는다.</summary>
    internal static Color Mix(Color a, Color b, double t)
        => Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static double Luma(Color c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
    #endregion

    #region - Styles -
    private readonly record struct InkStyle(Brush? Fill, Brush? Stroke, double Thickness, Brush? TextBrush = null, bool Mono = false,
                                            FontWeight? Weight = null, PenLineCap Cap = PenLineCap.Flat, PenLineJoin Join = PenLineJoin.Miter,
                                            double[]? Dash = null);

    private static SolidColorBrush B(Color c) => new(c);

    private InkStyle Style(FenceInk ink, Color? custom = null) => ink switch
    {
        // ── 펜스 편집기 — 사람이 고른 색(custom)이 있으면 그 색이 이긴다 ──
        FenceInk.Mesh when custom is { } c => new(MeshBrush(c), B(c), 0.6),
        FenceInk.Rail when custom is { } c => new(null, B(c), 2.2, Cap: PenLineCap.Round),
        FenceInk.PanelSelectFill => new(B(TintAccent), null, 0),
        FenceInk.PanelSelectEdge => new(null, B(Selection), 2.2, Join: PenLineJoin.Round),
        FenceInk.RubberBand => new(B(Color.FromArgb(0x1A, Primary.R, Primary.G, Primary.B)), B(Primary), 1, Dash: new[] { 5.0, 3.0 }),
        FenceInk.Razor => new(null, B(custom ?? RazorColor), 1.2),
        FenceInk.RazorBand => new(B(custom ?? RazorColor), null, 0),
        // ── 펜스 모양 5종(fence-style-art) — 재질 토큰(FenceRazor · FenceDesign · FenceBrick · FenceBrickMortar · FenceConcrete)에서 매번 푼다 ──
        FenceInk.RazorCoil => new(null, B(custom ?? RazorColor), 0.7),
        FenceInk.RazorBarb => new(null, B(Mix(custom ?? RazorColor, Tx1, 0.35)), 0.9, Cap: PenLineCap.Round),
        FenceInk.RazorStrand => new(null, B(Mix(RazorColor, Divider, 0.5)), 0.8),
        FenceInk.DesignWire => new(null, B(custom ?? Design), 0.9),
        FenceInk.DesignFold => new(null, B(Mix(custom ?? Design, Colors.White, 0.35)), 1.4, Cap: PenLineCap.Round),
        FenceInk.DesignClamp => new(B(Mix(custom ?? Design, Colors.Black, 0.3)), null, 0),
        FenceInk.BrickMortarFace => new(B(Mortar), null, 0),
        FenceInk.BrickTone0 => new(B(custom ?? Brick), null, 0),
        FenceInk.BrickTone1 => new(B(Mix(custom ?? Brick, Colors.Black, 0.12)), null, 0),
        FenceInk.BrickTone2 => new(B(Mix(custom ?? Brick, Colors.White, 0.12)), null, 0),
        FenceInk.ConcreteSpeckleDark => new(B(Mix(custom ?? Concrete, Colors.Black, 0.22)), null, 0),
        FenceInk.ConcreteSpeckleLight => new(B(Mix(custom ?? Concrete, Colors.White, 0.28)), null, 0),
        // ── 두 줄 개념도(FR-20) — Ch1 = 정보 계열 실선 · Ch2 = 앰버 계열(실선 / 점선 {6,4}) · 펜스 격자는 디자인펜스 초록 토큰 ──
        FenceInk.ConceptCh1 => new(null, B(Info), 2.2, Cap: PenLineCap.Round, Join: PenLineJoin.Round),
        FenceInk.ConceptCh2 => new(null, B(Accent), 2.2, Cap: PenLineCap.Round, Join: PenLineJoin.Round),
        FenceInk.ConceptCh2Dash => new(null, B(Accent), 1.6, Join: PenLineJoin.Round, Dash: new[] { 6.0, 4.0 }),
        FenceInk.ConceptFence => new(null, B(Mix(Design, Surface, 0.15)), 1.2),
        FenceInk.ConceptMesh => new(null, B(Mix(Design, Surface, 0.65)), 0.6),
        FenceInk.ConceptPost => new(null, B(Design), 3.2),
        FenceInk.ConceptGround => new(null, B(RowLine), 1),
        FenceInk.ConceptTick => new(null, B(Tx3), 1),
        FenceInk.ConceptTickText => new(null, null, 0, B(Tx2), false, FontWeights.SemiBold),
        FenceInk.ConceptLabelLower => new(null, null, 0, B(Info), true, FontWeights.Bold),
        FenceInk.ConceptLabelUpper => new(null, null, 0, B(Accent), true, FontWeights.Bold),
        FenceInk.ConceptChipLower => new(B(Info), B(Mix(Info, Colors.Black, 0.25)), 1),
        FenceInk.ConceptChipUpper => new(B(Accent), B(Mix(Accent, Colors.Black, 0.25)), 1),
        FenceInk.ConceptChipTextLower => new(null, null, 0, B(On(Info)), true, FontWeights.Bold),
        FenceInk.ConceptChipTextUpper => new(null, null, 0, B(On(Accent)), true, FontWeights.Bold),
        FenceInk.ConceptVbus => new(B(Sunken), B(Tx2), 1.2),
        FenceInk.ConceptVbusText => new(null, null, 0, B(Tx1), false, FontWeights.Bold),
        FenceInk.ConceptTarget => new(null, B(Primary), 2),
        FenceInk.RazorArm => new(null, B(Divider), 2.2, Cap: PenLineCap.Round),
        FenceInk.BrickFront => new(BrickBrush(custom ?? Brick, Mortar), B(Mix(custom ?? Brick, Colors.Black, 0.3)), 0.8),
        FenceInk.BrickSide => new(B(Mix(custom ?? Brick, Colors.Black, 0.3)), null, 0),
        FenceInk.BrickTop => new(B(Mix(custom ?? Brick, Colors.White, 0.25)), null, 0),
        FenceInk.ConcreteFront => new(B(custom ?? Concrete), B(ConcreteSeamColor), 0.8),
        FenceInk.ConcreteSeam => new(null, B(ConcreteSeamColor), 1.4),
        FenceInk.WallSide => new(B(Mix(custom ?? Concrete, Colors.Black, 0.25)), null, 0),
        FenceInk.WallTopFace => new(B(Mix(custom ?? Concrete, Colors.White, 0.25)), null, 0),
        FenceInk.WallCap => new(B(Mix(Concrete, Colors.White, 0.15)), B(ConcreteSeamColor), 0.8),
        FenceInk.DesignFace => new(DesignBrush(custom ?? Design), null, 0),
        FenceInk.DesignRail => new(null, B(custom ?? Design), 3, Cap: PenLineCap.Round),
        FenceInk.DesignPost => new(B(custom ?? Design), null, 0),
        FenceInk.PostFront when custom is { } c => new(B(c), null, 0),
        // ── 개념도(FR-12) — 점선 {4,3} · {5,3} 은 다른 뜻에 배정돼 있어 선에 쓰지 않는다 ──
        FenceInk.ConceptWire => new(null, B(Primary), 2.6, Cap: PenLineCap.Round, Join: PenLineJoin.Round),
        FenceInk.ConceptReturn => new(null, B(Tx3), 1.6, Cap: PenLineCap.Round, Join: PenLineJoin.Round),
        FenceInk.ConceptArrow => new(B(Primary), null, 0),
        FenceInk.ConceptNode => new(B(Alt), B(Border), 1.6),
        FenceInk.ConceptNodeIp => new(null, B(Info), 1.6),
        FenceInk.ConceptNodeText => new(null, null, 0, B(Tx1), true, FontWeights.Bold),
        FenceInk.ConceptNodeSub => new(null, null, 0, B(Tx3), false, FontWeights.SemiBold),
        FenceInk.ConceptController => new(B(Pressed), B(Border), 1.2),
        FenceInk.ConceptControllerText => new(null, null, 0, B(Tx1), false, FontWeights.Bold),
        FenceInk.ConceptPort => new(B(Tx2), B(Alt), 1),
        FenceInk.ConceptPortText => new(null, null, 0, B(Tx2), true, FontWeights.SemiBold),
        FenceInk.ConceptTitle => new(null, null, 0, B(Tx3), false, FontWeights.SemiBold),
        FenceInk.ConceptInfo => new(null, null, 0, B(Info), false, FontWeights.SemiBold),
        FenceInk.ConceptInsert => new(B(Primary), B(Primary), 3, Cap: PenLineCap.Round),
        FenceInk.ConceptInternalNet => new(null, B(Info), 1.2, Dash: new[] { 6.0, 4.0 }),
        FenceInk.Ground => new(B(Sunken), B(RowLine), 1),
        FenceInk.Section => new(B(Bg), B(Divider), 1),
        FenceInk.Strata => new(null, B(RowLine), 1),
        FenceInk.Grid => new(null, B(RowLine), 0.8),
        FenceInk.Base => new(null, B(Divider), 1.6),
        FenceInk.PostNumber => new(null, null, 0, B(Tx3), true, FontWeights.Medium),
        FenceInk.Axis => new(null, null, 0, B(Tx3), false, FontWeights.Medium),
        FenceInk.Caption => new(null, null, 0, B(Tx3), false, FontWeights.SemiBold),
        FenceInk.Mesh => new(MeshBrush(), B(RowLine), 0.6),
        FenceInk.Rail => new(null, B(Divider), 2.2, Cap: PenLineCap.Round),
        FenceInk.PostFront => new(B(Post), null, 0),
        FenceInk.PostSide => new(B(PostSide), null, 0),
        FenceInk.PostTop => new(B(PostTop), null, 0),
        FenceInk.CapFront => new(B(PostTop), null, 0),
        FenceInk.CapSide => new(B(Post), null, 0),
        FenceInk.CapTop => new(B(PostTop), null, 0),
        FenceInk.Chain => new(null, B(Primary), 3.2, Cap: PenLineCap.Round, Join: PenLineJoin.Round),
        FenceInk.ReturnOuter => new(null, B(Tx2), 4.6, Join: PenLineJoin.Round),
        FenceInk.ReturnInner => new(null, B(Sunken), 1.6, Join: PenLineJoin.Round),
        FenceInk.EndCap => new(B(Tx2), B(Sunken), 1.2),
        FenceInk.Chevron => new(B(Primary), B(Sunken), 0.8),
        FenceInk.LabelChain => new(null, null, 0, B(Primary), false, FontWeights.Bold),
        FenceInk.LabelReturn => new(null, null, 0, B(Tx2), false, FontWeights.Bold),
        FenceInk.OliveFront => new(B(Olive), B(OliveSide), 0.7),
        FenceInk.OliveSide => new(B(OliveSide), null, 0),
        FenceInk.OliveTop => new(B(OliveTop), B(OliveSide), 0.5),
        FenceInk.GlandFront => new(B(Border), null, 0),
        FenceInk.GlandSide => new(B(PostSide), null, 0),
        FenceInk.GlandTop => new(B(Divider), null, 0),
        FenceInk.Pir => new(B(Color.FromArgb(0xCC, Alt.R, Alt.G, Alt.B)), null, 0),
        FenceInk.Plate => new(B(Alt), B(OliveSide), 0.8),
        FenceInk.Number => new(null, null, 0, B(Tx1), true, FontWeights.Bold),
        FenceInk.NumberSmall => new(null, null, 0, B(Tx1), true, FontWeights.Bold),
        FenceInk.FenceLabel => new(null, null, 0, B(Tx2), true, FontWeights.SemiBold),
        FenceInk.Ball => new(B(PostTop), B(PostSide), 1),
        FenceInk.Rod => new(B(Olive), B(OliveSide), 0.8),
        FenceInk.RodRib => new(null, B(OliveSide), 1.2),
        FenceInk.UgCap => new(B(OliveTop), B(OliveSide), 0.8),
        FenceInk.VbusFront => new(B(Olive), B(OliveSide), 0.8),
        FenceInk.VbusSide => new(B(OliveSide), null, 0),
        FenceInk.VbusTop => new(B(OliveTop), null, 0),
        FenceInk.VbusRib => new(null, B(OliveSide), 1.4),
        FenceInk.VbusEar => new(B(OliveSide), null, 0),
        FenceInk.VbusText => new(null, null, 0, B(Alt), true, FontWeights.ExtraBold),
        FenceInk.VbusLabel => new(null, null, 0, B(Tx2), false, FontWeights.SemiBold),
        FenceInk.EnclosureFront => new(B(Alt), B(Primary), 1.8),
        FenceInk.EnclosureSide => new(B(Pressed), B(Primary), 0.8),
        FenceInk.EnclosureTop => new(B(Hover), B(Primary), 0.8),
        FenceInk.Dock => new(B(Sunken), B(Border), 0.8),
        FenceInk.CardSmart => new(B(CardSmart), B(Border), 0.6),
        FenceInk.CardVbus => new(B(Info), null, 0),
        FenceInk.CardSmartText => new(null, null, 0, B(CardSmartText), false, FontWeights.Bold),
        FenceInk.CardVbusText => new(null, null, 0, B(OnPrimary), false, FontWeights.Bold),
        FenceInk.Port => new(B(Surface), B(CardSmartText), 1),
        FenceInk.PortText => new(null, null, 0, B(CardSmartText), true, FontWeights.Bold),
        FenceInk.ControllerFront => new(B(Pressed), B(Border), 1.2),
        FenceInk.ControllerSide => new(B(PostSide), null, 0),
        FenceInk.ControllerTop => new(B(PostTop), null, 0),
        FenceInk.ControllerText => new(null, null, 0, B(Tx1), false, FontWeights.Bold),
        FenceInk.ControllerSub => new(null, null, 0, B(Tx2), true, FontWeights.SemiBold),
        FenceInk.Range => new(B(TintInfo), B(Info), 1),
        FenceInk.GapMark => new(B(Surface), B(Critical), 2),
        FenceInk.GapText => new(null, null, 0, B(Critical), false, FontWeights.Bold),
        FenceInk.Hit => new(Brushes.Transparent, null, 0),
        FenceInk.Select => new(null, B(Selection), 2.2),
        FenceInk.Draft => new(B(Warning), null, 0),
        FenceInk.Proposal => new(B(Info), null, 0),
        FenceInk.GroupBack => new(B(Alt), B(Divider), 1.2),
        FenceInk.GroupBody => new(B(Alt), B(Border), 1.4),
        FenceInk.GroupText => new(null, null, 0, B(Tx1), false, FontWeights.Bold),
        FenceInk.GroupSub => new(null, null, 0, B(Tx3), true, FontWeights.SemiBold),
        FenceInk.Pill => new(B(Alt), B(Border), 1, B(Tx1), false, FontWeights.SemiBold),
        FenceInk.PillDuplicate => new(B(Sunken), B(Divider), 1, B(Tx2), false, FontWeights.Medium),
        FenceInk.PillInsert => new(B(Primary), null, 0, B(OnPrimary), false, FontWeights.Bold),
        FenceInk.PillPort => new(B(Alt), B(RowLine), 0.8, B(Tx2), true, FontWeights.SemiBold),
        FenceInk.Insert => new(B(Primary), B(Primary), 3, Cap: PenLineCap.Round),
        // 보는 쪽(FR-20) — 옅은 채움 + 실선 윤곽(형태로 가른다) · "뒤" 표지는 진한 판에 밝은 글자.
        FenceInk.FacingTag => new(B(Tx2), B(Alt), 1),
        FenceInk.FacingTagText => new(null, null, 0, B(Alt), false, FontWeights.Bold),
        FenceInk.SideLabel => new(null, null, 0, B(Tx2), false, FontWeights.SemiBold),
        _ => new(B(Tx1), null, 0),
    };

    /// <summary>철망 무늬 — 12×12 마름모 격자(목업 <c>#meshpat</c>). 테마마다 다시 만든다.</summary>
    private Brush MeshBrush(Color? line = null)
    {
        var geometry = Geometry.Parse("M0,6 L6,0 12,6 6,12Z");
        var drawing = new GeometryDrawing(null, new Pen(B(line ?? RowLine), 0.8), geometry);
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 12, 12),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 12, 12),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
    }
    #endregion

    /// <summary>벽돌 무늬 — 24×12 타일(줄눈 가로 두 줄 · 세로는 줄마다 엇갈림). 매번 새로 만든다(Frozen · 캐시 없음).</summary>
    private static Brush BrickBrush(Color brick, Color mortar)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(B(brick), null, new RectangleGeometry(new Rect(0, 0, 24, 12))));
        var pen = new Pen(B(mortar), 1);
        group.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse("M0,0.5 H24 M0,6.5 H24 M0.5,0.5 V6.5 M12.5,6.5 V12")));
        return new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 24, 12),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 24, 12),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
    }

    /// <summary>디자인펜스 세로살 — 8×8 타일에 3.5 폭 살 하나(나머지는 비친다).</summary>
    private static Brush DesignBrush(Color slat)
    {
        var drawing = new GeometryDrawing(B(slat), null, new RectangleGeometry(new Rect(0, 0, 3.5, 8)));
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 8, 8),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 8, 8),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
    }

    #region - Draw -
    private void DrawOne(DrawingContext dc, FenceShape shape, Geometry? rangeClip)
    {
        var style = Style(shape.Ink, ParseColor(shape.Color));
        var pen = style.Stroke is null || style.Thickness <= 0 ? null
            : new Pen(style.Stroke, style.Thickness) { StartLineCap = style.Cap, EndLineCap = style.Cap, LineJoin = style.Join };
        if (pen is not null && style.Dash is { } dash) pen.DashStyle = new DashStyle(dash, 0);

        var faded = shape.Opacity < 1 || shape.Ink == FenceInk.Range;
        if (faded) dc.PushOpacity(shape.Ink == FenceInk.Range ? 0.6 * shape.Opacity : shape.Opacity);
        var clipped = shape.Ink == FenceInk.Range && rangeClip is not null;
        if (clipped) dc.PushClip(rangeClip!);

        switch (shape.Kind)
        {
            case FenceShapeKind.Polygon:
                dc.DrawGeometry(style.Fill, pen, PolyGeometry(shape.Points, closed: true, filled: style.Fill is not null));
                break;
            case FenceShapeKind.Polyline:
                dc.DrawGeometry(null, pen, PolyGeometry(shape.Points, closed: false, filled: false));
                break;
            case FenceShapeKind.Line:
                if (pen is not null) dc.DrawLine(pen, shape.Points[0], shape.Points[1]);
                break;
            case FenceShapeKind.Ellipse:
                dc.DrawEllipse(style.Fill, pen, shape.Points[0], shape.RadiusX, shape.RadiusY);
                break;
            case FenceShapeKind.Rect:
                dc.DrawRoundedRectangle(style.Fill, pen, new Rect(shape.Points[0], shape.Points[1]), shape.RadiusX, shape.RadiusY);
                break;
            case FenceShapeKind.Text:
                DrawText(dc, shape.Points[0], shape.Text ?? string.Empty, shape.FontSize, style, shape.Anchor);
                break;
            case FenceShapeKind.Pill:
                DrawPill(dc, shape, style, pen);
                break;
            case FenceShapeKind.Strokes:
                if (pen is not null && shape.Points.Length > 1) dc.DrawGeometry(null, pen, FiguresGeometry(shape));
                break;
            case FenceShapeKind.Patches:
                if (shape.Points.Length > 2) dc.DrawGeometry(style.Fill, pen, FiguresGeometry(shape));
                break;
        }

        if (clipped) dc.Pop();
        if (faded) dc.Pop();
    }

    /// <summary>
    /// 묶음 그림(<see cref="FenceShapeKind.Strokes"/> · <see cref="FenceShapeKind.Patches"/>)의 기하 — 그림 값마다 한 번 만들어 얼려 둔다(약한 참조 표).
    /// 장면이 다시 세워지면(망 · 줌이 바뀜) 새 그림 값이라 새로 만들고, 테마만 바뀌면 같은 기하에 새 토큰 색만 칠한다.
    /// </summary>
    private static readonly ConditionalWeakTable<FenceShape, StreamGeometry> FiguresCache = new();

    internal static StreamGeometry FiguresGeometry(FenceShape shape)
        => FiguresCache.GetValue(shape, static s =>
        {
            var g = new StreamGeometry();
            var filled = s.Kind == FenceShapeKind.Patches;
            using (var ctx = g.Open())
            {
                var counts = s.Figures ?? new[] { s.Points.Length };
                var at = 0;
                foreach (var count in counts)
                {
                    if (count >= 2 && at + count <= s.Points.Length)
                    {
                        ctx.BeginFigure(s.Points[at], filled, s.Closed);
                        for (var i = 1; i < count; i++) ctx.LineTo(s.Points[at + i], true, false);
                    }
                    at += count;
                }
            }
            g.Freeze();
            return g;
        });

    private static StreamGeometry PolyGeometry(IReadOnlyList<Point> points, bool closed, bool filled)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(points[0], filled, closed);
            ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
        }
        return g;
    }

    private FormattedText Format(string text, double size, InkStyle style)
        => new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
               new Typeface(new FontFamily(style.Mono ? MONO_FONT : BODY_FONT), FontStyles.Normal, style.Weight ?? FontWeights.Normal, FontStretches.Normal),
               size, style.TextBrush ?? Brushes.Black, _dpi);

    private void DrawText(DrawingContext dc, Point baseline, string text, double size, InkStyle style, FenceTextAnchor anchor)
    {
        if (text.Length == 0) return;
        var ft = Format(text, size, style);
        var x = anchor switch
        {
            FenceTextAnchor.Start => baseline.X,
            FenceTextAnchor.End => baseline.X - ft.WidthIncludingTrailingWhitespace,
            _ => baseline.X - ft.WidthIncludingTrailingWhitespace / 2,
        };
        dc.DrawText(ft, new Point(x, baseline.Y - ft.Baseline));
    }

    /// <summary>알약 — 목업 <c>pill()</c>(폭 = 글자 + 16 · 높이 = 글자 + 9) · A/B 번호표는 폭 = 글자 + 10 · 높이 14.</summary>
    private void DrawPill(DrawingContext dc, FenceShape shape, InkStyle style, Pen? pen)
    {
        var text = shape.Text ?? string.Empty;
        var size = shape.FontSize;
        var ft = Format(text, size, style);
        var port = shape.Ink == FenceInk.PillPort;
        var w = ft.WidthIncludingTrailingWhitespace + (port ? 10 : 16);
        var h = port ? 14 : size + 9;
        var c = shape.Points[0];
        dc.DrawRoundedRectangle(style.Fill, pen, new Rect(c.X - w / 2, c.Y - h / 2, w, h), h / 2, h / 2);
        dc.DrawText(ft, new Point(c.X - ft.WidthIncludingTrailingWhitespace / 2, c.Y - ft.Height / 2));
    }
    #endregion
}
