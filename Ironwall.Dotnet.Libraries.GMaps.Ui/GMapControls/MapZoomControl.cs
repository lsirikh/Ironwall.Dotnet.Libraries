using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

public class MapZoomControl : Control
{
    static MapZoomControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MapZoomControl),
            new FrameworkPropertyMetadata(typeof(MapZoomControl)));
    }

    public double Zoom
    {
        get { return (double)GetValue(ZoomProperty); }
        set { SetValue(ZoomProperty, value); }
    }

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register("Zoom", typeof(double),
            typeof(MapZoomControl),
            new FrameworkPropertyMetadata(15.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public int MinZoom
    {
        get { return (int)GetValue(MinZoomProperty); }
        set { SetValue(MinZoomProperty, value); }
    }

    public static readonly DependencyProperty MinZoomProperty =
        DependencyProperty.Register("MinZoom", typeof(int),
            typeof(MapZoomControl), new PropertyMetadata(2));

    public int MaxZoom
    {
        get { return (int)GetValue(MaxZoomProperty); }
        set { SetValue(MaxZoomProperty, value); }
    }

    public static readonly DependencyProperty MaxZoomProperty =
        DependencyProperty.Register("MaxZoom", typeof(int),
            typeof(MapZoomControl), new PropertyMetadata(19));

    public ICommand ZoomInCommand
    {
        get { return (ICommand)GetValue(ZoomInCommandProperty); }
        set { SetValue(ZoomInCommandProperty, value); }
    }

    public static readonly DependencyProperty ZoomInCommandProperty =
        DependencyProperty.Register("ZoomInCommand", typeof(ICommand),
            typeof(MapZoomControl));

    public ICommand ZoomOutCommand
    {
        get { return (ICommand)GetValue(ZoomOutCommandProperty); }
        set { SetValue(ZoomOutCommandProperty, value); }
    }

    public static readonly DependencyProperty ZoomOutCommandProperty =
        DependencyProperty.Register("ZoomOutCommand", typeof(ICommand),
            typeof(MapZoomControl));
}
