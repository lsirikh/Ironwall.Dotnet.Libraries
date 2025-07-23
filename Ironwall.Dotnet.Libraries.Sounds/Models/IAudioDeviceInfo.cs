using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Sounds.Models;
public interface IAudioDeviceInfo : IBaseModel
{
    string Description { get; set; }
    int DeviceNumber { get; set; }
    EnumAudioType DeviceType { get; set; }
    bool IsDefault { get; set; }
    bool IsEnabled { get; set; }
    string Name { get; set; }
    object? NativeDevice { get; set; }
}