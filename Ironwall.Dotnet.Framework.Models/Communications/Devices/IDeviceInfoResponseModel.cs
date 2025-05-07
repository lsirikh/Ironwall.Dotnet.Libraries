using Ironwall.Dotnet.Framework.Models.Devices;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    public interface IDeviceInfoResponseModel : IResponseModel
    {
        DeviceDetailModel Detail { get; }
    }
}