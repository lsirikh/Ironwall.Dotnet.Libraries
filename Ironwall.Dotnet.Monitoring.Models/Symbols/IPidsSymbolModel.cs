using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface IPidsSymbolModel :  ISymbolModel
{
    double DetectionAngle { get; set; }
    double DetectionBearing { get; set; }
    double DetectionRange { get; set; }
    EnumDeviceType DeviceType { get; set; }
    EnumColorType FOVColor { get; set; }
    double FOVOpacity { get; set; }
    int LinkedDeviceId { get; set; }
    bool ShowFOV { get; set; }
    EnumEventStatus EventStatus { get; set; }
    void SetUpdate();
    event EventHandler Update;
}