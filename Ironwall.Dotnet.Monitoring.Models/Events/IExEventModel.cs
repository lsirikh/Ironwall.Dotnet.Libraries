using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
public interface IExEventModel : IBaseEventModel
{
    string? EventGroup { get; set; }
    IBaseDeviceModel? Device { get; set; }
    EnumTrueFalse Status { get; set; }
}