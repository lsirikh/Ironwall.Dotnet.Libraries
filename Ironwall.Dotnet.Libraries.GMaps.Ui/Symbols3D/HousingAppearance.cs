using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

/// <summary>Per-session presentation setting. Does not change shared marker color contracts or persisted data.</summary>
public static class HousingAppearance
{
    public static readonly DependencyProperty TintStrengthProperty = DependencyProperty.RegisterAttached(
        "TintStrength", typeof(double), typeof(HousingAppearance), new FrameworkPropertyMetadata(.35,
            FrameworkPropertyMetadataOptions.Inherits, (d, e) =>
            { if (d is HousingVisual visual) visual.RefreshMaterials(); },
            (d, v) => double.IsFinite((double)v) ? Math.Clamp((double)v, 0, 1) : .35));
    public static double GetTintStrength(DependencyObject d) => (double)d.GetValue(TintStrengthProperty);
    public static void SetTintStrength(DependencyObject d, double value) => d.SetValue(TintStrengthProperty, value);
}
