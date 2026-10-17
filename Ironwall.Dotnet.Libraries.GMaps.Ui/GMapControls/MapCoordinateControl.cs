using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

public class MapCoordinateControl : Control
{
    static MapCoordinateControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MapCoordinateControl),
            new FrameworkPropertyMetadata(typeof(MapCoordinateControl)));
    }

    public double Latitude
    {
        get { return (double)GetValue(LatitudeProperty); }
        set { SetValue(LatitudeProperty, value); }
    }

    public static readonly DependencyProperty LatitudeProperty =
        DependencyProperty.Register("Latitude", typeof(double),
            typeof(MapCoordinateControl), new PropertyMetadata(0.0));

    public double Longitude
    {
        get { return (double)GetValue(LongitudeProperty); }
        set { SetValue(LongitudeProperty, value); }
    }

    public static readonly DependencyProperty LongitudeProperty =
        DependencyProperty.Register("Longitude", typeof(double),
            typeof(MapCoordinateControl), new PropertyMetadata(0.0));

    public string MGRS
    {
        get { return (string)GetValue(MGRSProperty); }
        set { SetValue(MGRSProperty, value); }
    }

    public static readonly DependencyProperty MGRSProperty =
        DependencyProperty.Register("MGRS", typeof(string),
            typeof(MapCoordinateControl), new PropertyMetadata(string.Empty));

    public string UTM
    {
        get { return (string)GetValue(UTMProperty); }
        set { SetValue(UTMProperty, value); }
    }

    public static readonly DependencyProperty UTMProperty =
        DependencyProperty.Register("UTM", typeof(string),
            typeof(MapCoordinateControl), new PropertyMetadata(string.Empty));

    public bool IsShowWGS84
    {
        get { return (bool)GetValue(IsShowWGS84Property); }
        set { SetValue(IsShowWGS84Property, value); }
    }

    public static readonly DependencyProperty IsShowWGS84Property =
        DependencyProperty.Register("IsShowWGS84", typeof(bool),
            typeof(MapCoordinateControl), new PropertyMetadata(true));

    public bool IsShowMGRS
    {
        get { return (bool)GetValue(IsShowMGRSProperty); }
        set { SetValue(IsShowMGRSProperty, value); }
    }

    public static readonly DependencyProperty IsShowMGRSProperty =
        DependencyProperty.Register("IsShowMGRS", typeof(bool),
            typeof(MapCoordinateControl), new PropertyMetadata(false));

    public bool IsShowUTM
    {
        get { return (bool)GetValue(IsShowUTMProperty); }
        set { SetValue(IsShowUTMProperty, value); }
    }

    public static readonly DependencyProperty IsShowUTMProperty =
        DependencyProperty.Register("IsShowUTM", typeof(bool),
            typeof(MapCoordinateControl), new PropertyMetadata(false));
}
