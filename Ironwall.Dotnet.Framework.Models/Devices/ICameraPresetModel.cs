namespace Ironwall.Dotnet.Framework.Models.Devices;

public interface ICameraPresetModel : IBaseOptionModel
{
    string PresetName { get; set; }
    bool IsHome { get; set; }
    double Pan { get; set; }
    double Tilt { get; set; }
    double Zoom { get; set; }
    int Delay { get; set; }
}