namespace Ironwall.Dotnet.Framework.Models.Ais;

public interface INetworkSettingModel : IBaseModel
{
    bool IsAvailable { get; set; }
    string Name { get; set; }
    string IpAddress { get; set; }
    int Port { get; set; }
}