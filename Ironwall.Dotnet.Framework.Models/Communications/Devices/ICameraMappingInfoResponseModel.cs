using Ironwall.Dotnet.Framework.Models.Devices;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    public interface ICameraMappingInfoResponseModel : IResponseModel
    {
        MappingInfoModel Detail { get; }
    }
}