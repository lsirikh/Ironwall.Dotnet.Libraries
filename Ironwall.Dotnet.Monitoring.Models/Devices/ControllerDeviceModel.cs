using Ironwall.Dotnet.Libraries.Enums;
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
        // (422 fix) Camera/Speaker/Enclosure/Lamp 모델과 동일하게 타입 명시.
        //   누락 시 DeviceType=NONE → ToControllerDeviceDto가 type_device="NONE" 전송 → 서버 422(enum 검증 실패).
        DeviceType = EnumDeviceType.Controller;
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