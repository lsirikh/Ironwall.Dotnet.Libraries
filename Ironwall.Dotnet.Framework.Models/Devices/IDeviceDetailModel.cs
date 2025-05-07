using Ironwall.Dotnet.Framework.Models.Communications;
using System;

namespace Ironwall.Dotnet.Framework.Models.Devices
{ 
    public interface IDeviceDetailModel : IUpdateDetailBaseModel
    {
        int Camera { get; }
        int Controller { get; }
        int Sensor { get; }
        
    }
}