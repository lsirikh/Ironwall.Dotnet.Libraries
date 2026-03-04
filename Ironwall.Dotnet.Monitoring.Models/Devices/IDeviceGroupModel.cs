using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IDeviceGroupModel : IBaseModel
{
    string Name { get; set; }
    string? Description { get; set; }
    int DeviceCount { get; set; }
}
