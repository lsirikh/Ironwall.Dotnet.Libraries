using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 1:53:56 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ControllerDeviceModel : BaseDeviceModel, IControllerDeviceModel
{

    public ControllerDeviceModel()
    {

    }

    public ControllerDeviceModel(IControllerDeviceModel model) : base(model)
    {
        IpAddress = model.IpAddress;
        Port = model.Port;
        Devices = model.Devices;
    }

    [JsonProperty("ip_address", Order = 6)]
    public string IpAddress { get; set; } = string.Empty;

    [JsonProperty("ip_port", Order = 7)]
    public int Port { get; set; }

    [JsonIgnore]
    public List<IBaseDeviceModel>? Devices { get; set; }
}