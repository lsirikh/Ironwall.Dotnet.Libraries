using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Models.Devices;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public interface ICameraTableMapper : IDeviceMapperBase
    {
        string IpAddress { get; set; }
        int Port { get; set; }
        string UserName { get; set; }
        string Password { get; set; }
        EnumCameraType Category { get; set; }
        string DeviceModel { get; set; }
        int RtspPort { get; set; }
        string RtspUri { get; set; }
        int Mode { get; set; }
    }
}