namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IEnclosureThresholdConfigModel
{
    double? TempHigh { get; set; }
    double? TempLow { get; set; }
    double? HumidityHigh { get; set; }
    double? HumidityLow { get; set; }
    double? VibrationThreshold { get; set; }
}
