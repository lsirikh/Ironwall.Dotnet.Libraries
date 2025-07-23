using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Extends;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models
{
    public interface IConnectionModel : IBaseModel
    {
        void Update(IConnectionModel model);
        string DeviceName { get; set; }
        CameraZoneModel Zone { get; set; }
        string UserName { get; set; }
        string Password { get; set; }
        string IpAddress { get; set; }
        int Port { get; set; }
        int PortOnvif { get; set; }
        int PortRtsp { get; set; }
        bool RtspAuthRequired { get; set; }
        bool IsDummy { get; set; }
        string DummyOption { get; set; }

    }
}