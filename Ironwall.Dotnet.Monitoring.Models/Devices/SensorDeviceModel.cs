using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 1:55:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SensorDeviceModel : BaseDeviceModel, ISensorDeviceModel
{
    public SensorDeviceModel()
    {
        Controller = new ControllerDeviceModel();
    }

    [JsonProperty("controller", Order = 6)]
    [JsonConverter(typeof(DeviceModelConverter))]
    public IControllerDeviceModel Controller { get; set; }
}