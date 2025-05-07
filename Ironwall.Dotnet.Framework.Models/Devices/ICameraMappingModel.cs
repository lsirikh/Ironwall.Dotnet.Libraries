namespace Ironwall.Dotnet.Framework.Models.Devices;

public interface ICameraMappingModel : IBaseModel
{
    string MappingGroup { get; set; }
    SensorDeviceModel? Sensor { get; set; }
    CameraPresetModel? FirstPreset { get; set; }
    CameraPresetModel? SecondPreset { get; set; }
}