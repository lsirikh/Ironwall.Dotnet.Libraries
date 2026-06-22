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
        Location = model.Location;
        Latitude = model.Latitude;
        Longitude = model.Longitude;
        IsEnable = model.IsEnable;
        Heading = model.Heading;
        Altitude = model.Altitude;
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
    [JsonProperty("location", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public string? Location { get; set; }
    [JsonProperty("latitude", Order = 11)]
    public double Latitude { get; set; }
    [JsonProperty("longitude", Order = 12)]
    public double Longitude { get; set; }
    [JsonProperty("is_enable", Order = 13)]
    public bool IsEnable { get; set; }
    /// <summary>장비 설치 방위각 0~360° (v4.4 geolocation.heading). 심볼 FOV BaseBearing 구동용. optional.</summary>
    [JsonProperty("heading", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public double? Heading { get; set; }
    /// <summary>장비 설치 고도(m) (v4.4 geolocation.altitude). optional.</summary>
    [JsonProperty("altitude", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public double? Altitude { get; set; }
}