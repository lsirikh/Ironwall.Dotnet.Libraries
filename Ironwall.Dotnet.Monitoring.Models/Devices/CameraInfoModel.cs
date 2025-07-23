using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/25/2025 4:01:25 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CameraInfoModel : BaseModel, ICameraInfoModel
{
    [JsonProperty("name", Order = 1)]
    public string? Name { get; set; }

    [JsonProperty("location", Order = 2)]
    public string? Location { get; set; }

    [JsonProperty("manufacturer", Order = 3)]
    public string? Manufacturer { get; set; }

    [JsonProperty("model", Order = 4)]
    public string? Model { get; set; }

    [JsonProperty("hardware", Order = 5)]
    public string? Hardware { get; set; }

    [JsonProperty("firmware", Order = 6)]
    public string? Firmware { get; set; }

    [JsonProperty("device_id", Order = 7)]
    public string? DeviceId { get; set; }

    [JsonProperty("mac_address", Order = 8)]
    public string? MacAddress { get; set; }

    [JsonProperty("onvif_version", Order = 9)]
    public string? OnvifVersion { get; set; }

    [JsonProperty("uri", Order = 10)]
    public string? Uri { get; set; }

}