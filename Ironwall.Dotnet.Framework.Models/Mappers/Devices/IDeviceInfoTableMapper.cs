using Ironwall.Dotnet.Framework.Models.Mappers;

namespace Ironwall.Dotnet.Framework.Models
{
    public interface IDeviceInfoTableMapper : IUpdateMapperBase
    {
        int Camera { get; }
        int Controller { get; }
        int Sensor { get; }
    }
}