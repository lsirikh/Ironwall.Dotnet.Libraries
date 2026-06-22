namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>함체 임계값 설정 — 서버 EnclosureThresholdConfig 스키마 정렬.</summary>
public interface IEnclosureThresholdConfigModel
{
    double? TempHigh { get; set; }
    double? TempLow { get; set; }
    double? HumidityHigh { get; set; }
    double? CurrentHigh { get; set; }
    double? VoltageLow { get; set; }
    int? VibrationHigh { get; set; }
}
