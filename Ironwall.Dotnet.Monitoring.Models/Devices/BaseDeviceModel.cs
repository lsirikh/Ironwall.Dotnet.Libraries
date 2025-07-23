using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 12:43:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class BaseDeviceModel : BaseModel, IBaseDeviceModel
{
    public BaseDeviceModel()
    {

    }

    public BaseDeviceModel(IBaseDeviceModel model) : base(model)
    {
        DeviceGroup = model.DeviceGroup;
        DeviceNumber = model.DeviceNumber;
        DeviceName = model.DeviceName;
        DeviceType = model.DeviceType;
        Version = model.Version;
        Status = model.Status;
    }

    [JsonProperty("device_number", Order = 2)]
    public int DeviceNumber { get; set; }
    [JsonProperty("device_group", Order = 3)]
    public int DeviceGroup { get; set; }
    [JsonProperty("device_name", Order = 4)]
    public string? DeviceName { get; set; }
    [JsonProperty("device_type", Order = 5)]
    public EnumDeviceType DeviceType { get; set; } = EnumDeviceType.NONE;
    [JsonProperty("version", Order = 6)]
    public string? Version { get; set; } 
    [JsonIgnore]
    public EnumDeviceStatus Status { get; set; } = EnumDeviceStatus.DEACTIVATED;
}