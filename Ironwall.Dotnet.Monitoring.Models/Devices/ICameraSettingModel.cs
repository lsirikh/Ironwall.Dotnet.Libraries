using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ICameraSettingModel : IBaseModel
{
    int CameraId { get; set; }
    string WeatherMode { get; set; }
    string CameraMode { get; set; }
    string Heater { get; set; }
    string Fan { get; set; }
    string Headlight { get; set; }
    string DayNightMode { get; set; }
    string FocusMode { get; set; }
    string IrisMode { get; set; }
    string Tracking { get; set; }
    string? Palette { get; set; }
}
