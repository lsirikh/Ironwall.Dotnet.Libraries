namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public interface ISensorTableMapper :IDeviceMapperBase
    {
        int Controller { get; set; }
    }
}