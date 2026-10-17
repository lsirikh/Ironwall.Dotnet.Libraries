using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

public class MapScaleBarControl : Control
{
    static MapScaleBarControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MapScaleBarControl),
            new FrameworkPropertyMetadata(typeof(MapScaleBarControl)));
    }

    public string Scale
    {
        get { return (string)GetValue(ScaleProperty); }
        set { SetValue(ScaleProperty, value); }
    }

    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.Register("Scale", typeof(string),
            typeof(MapScaleBarControl),
            new PropertyMetadata("100m"));

    public PointCollection ScalePoints
    {
        get { return (PointCollection)GetValue(ScalePointsProperty); }
        set { SetValue(ScalePointsProperty, value); }
    }

    public static readonly DependencyProperty ScalePointsProperty =
        DependencyProperty.Register("ScalePoints", typeof(PointCollection),
            typeof(MapScaleBarControl),
            new PropertyMetadata(null));
}
