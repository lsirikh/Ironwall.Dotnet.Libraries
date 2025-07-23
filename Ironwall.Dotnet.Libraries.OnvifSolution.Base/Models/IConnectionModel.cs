using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
public interface IConnectionModel : IBaseModel
{
    string? DeviceName { get; set; }
    string? DummyOption { get; set; }
    string? IpAddress { get; set; }
    bool IsDummy { get; set; }
    string? Password { get; set; }
    int Port { get; set; }
    int PortOnvif { get; set; }
    int PortRtsp { get; set; }
    bool RtspAuthRequired { get; set; }
    string? Username { get; set; }
    void Update(IConnectionModel model);
}