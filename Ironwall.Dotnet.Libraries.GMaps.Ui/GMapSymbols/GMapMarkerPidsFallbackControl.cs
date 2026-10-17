using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Ironwall.Dotnet.Libraries.Enums;
using MaterialDesignThemes.Wpf;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/// <summary>2D coverage for device types that have no legacy SVG section.</summary>
public sealed class GMapMarkerPidsFallbackControl : GMapMarkerPidsControl
{
    static GMapMarkerPidsFallbackControl() => DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerPidsFallbackControl), new FrameworkPropertyMetadata(typeof(GMapMarkerPidsFallbackControl)));
    public GMapMarkerPidsFallbackControl(GMapPidsMarker marker) : base(marker) { }
    public static bool NeedsFallback(EnumDeviceType type) => type is EnumDeviceType.Underground or EnumDeviceType.Contact or EnumDeviceType.PIR
        or EnumDeviceType.IoController or EnumDeviceType.Laser or EnumDeviceType.Radar or EnumDeviceType.Lamp or EnumDeviceType.Enclosure or EnumDeviceType.Gate;
}

public sealed class HousingFallbackIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        EnumDeviceType.Underground => PackIconKind.Layers,
        EnumDeviceType.Contact => PackIconKind.DoorClosed,
        EnumDeviceType.PIR => PackIconKind.MotionSensor,
        EnumDeviceType.IoController => PackIconKind.Chip,
        EnumDeviceType.Laser => PackIconKind.RayStartEnd,
        EnumDeviceType.Radar => PackIconKind.Radar,
        EnumDeviceType.Lamp => PackIconKind.LightbulbOn,
        EnumDeviceType.Enclosure => PackIconKind.Server,
        EnumDeviceType.Gate => PackIconKind.Gate,   // VER-05: MDIX 5.2.1 실존은 컴파일로 확인(없으면 DoorClosed)
        _ => PackIconKind.MapMarker
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
