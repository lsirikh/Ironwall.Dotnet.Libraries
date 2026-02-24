using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System.Collections.Generic;

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
        DeviceGroups = model.DeviceGroups;
        DeviceNumber = model.DeviceNumber;
        DeviceName = model.DeviceName;
        DeviceType = model.DeviceType;
        Version = model.Version;
        Status = model.Status;
    }

    [JsonProperty("device_number", Order = 2)]
    public int DeviceNumber { get; set; }
    [JsonProperty("device_groups", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? DeviceGroups { get; set; }
    [JsonIgnore]
    public string DeviceGroupsText =>
        DeviceGroups != null && DeviceGroups.Count > 0
            ? string.Join(", ", DeviceGroups)
            : "";
    [JsonProperty("device_name", Order = 4)]
    public string? DeviceName { get; set; }
    [JsonProperty("device_type", Order = 5)]
    public EnumDeviceType DeviceType { get; set; } = EnumDeviceType.NONE;
    [JsonProperty("version", Order = 6)]
    public string? Version { get; set; } 
    [JsonIgnore]
    public EnumDeviceStatus Status { get; set; } = EnumDeviceStatus.DEACTIVATED;
}