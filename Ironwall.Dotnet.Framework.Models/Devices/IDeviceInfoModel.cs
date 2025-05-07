using System;

namespace Ironwall.Dotnet.Framework.Models.Devices
{
    public interface IDeviceInfoModel
    {
        int Camera { get; set; }
        int Controller { get; set; }
        int Sensor { get; set; }
        DateTime UpdateTime { get; set; }
    }
}