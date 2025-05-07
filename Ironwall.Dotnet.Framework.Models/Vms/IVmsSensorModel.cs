using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Vms
{
    public interface IVmsSensorModel : IBaseModel
    {
        int GroupNumber { get; set; }
        BaseDeviceModel Device { get; set; }
        EnumTrueFalse Status { get; set; }
    }
}