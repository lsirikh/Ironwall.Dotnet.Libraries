using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                                            FontWeight? Weight = null, PenLineCap Cap = PenLineCap.Flat, PenLineJoin Join = PenLineJoin.Miter);

    private static SolidColorBrush B(Color c) => new(c);

    private InkStyle Style(FenceInk ink) => ink switch
    {
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
        _ => new(B(Tx1), null, 0),
    };

    /// <summary>철망 무늬 — 12×12 마름모 격자(목업 <c>#meshpat</c>). 테마마다 다시 만든다.</summary>
    private Brush MeshBrush()
    {
        var geometry = Geometry.Parse("M0,6 L6,0 12,6 6,12Z");
        var drawing = new GeometryDrawing(null, new Pen(B(RowLine), 0.8), geometry);
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

    #region - Draw -
    private void DrawOne(DrawingContext dc, FenceShape shape, Geometry? rangeClip)
    {
        var style = Style(shape.Ink);
        var pen = style.Stroke is null || style.Thickness <= 0 ? null
            : new Pen(style.Stroke, style.Thickness) { StartLineCap = style.Cap, EndLineCap = style.Cap, LineJoin = style.Join };

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
        }

        if (clipped) dc.Pop();
        if (faded) dc.Pop();
    }

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
