using Ironwall.Dotnet.Libraries.Enums;
using System.Windows;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 펜스 모양 견본(속성 칸의 판 종류 단추 · fence-wiring-editor FR-03) — 펜스 캔버스와 <b>같은 그림</b>(<see cref="FenceScene.Swatch"/>)을
/// 같은 렌더러로 그린다. 색은 그릴 때마다 토큰에서 푼다(테마 전환 시 다시 그린다).
/// </summary>
public sealed class FenceStyleSwatch : FrameworkElement
{
    public static readonly DependencyProperty FenceStyleProperty = DependencyProperty.Register(
        nameof(FenceStyle), typeof(EnumFenceStyle), typeof(FenceStyleSwatch),
        new FrameworkPropertyMetadata(EnumFenceStyle.ChainLink, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SwatchColorProperty = DependencyProperty.Register(
        nameof(SwatchColor), typeof(string), typeof(FenceStyleSwatch),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(object), typeof(FenceStyleSwatch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public FenceStyleSwatch()
    {
        Width = 40;
        Height = 26;
        IsHitTestVisible = false;
        SetResourceReference(ThemeProbeProperty, "SurfaceSunkenBrush");
    }

    /// <summary>그릴 모양.</summary>
    public EnumFenceStyle FenceStyle
    {
        get => (EnumFenceStyle)GetValue(FenceStyleProperty);
        set => SetValue(FenceStyleProperty, value);
    }

    /// <summary>사람이 고른 색(#RRGGBB) — 없으면 모양 기본색.</summary>
    public string? SwatchColor
    {
        get => (string?)GetValue(SwatchColorProperty);
        set => SetValue(SwatchColorProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        FenceRenderer.Draw(drawingContext, this, FenceScene.Swatch(FenceStyle, w, h, SwatchColor));
        drawingContext.Pop();
    }
}
