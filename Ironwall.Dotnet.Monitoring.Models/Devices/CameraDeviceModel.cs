using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 5:01:34 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CameraDeviceModel : BaseDeviceModel, ICameraDeviceModel
{
    public CameraDeviceModel()
    {
        DeviceType = EnumDeviceType.IpCamera;
    }

    public CameraDeviceModel(int id) : this()
    {
        Id = id;
    }

    [JsonProperty("ip_address", Order = 7)]
    public string IpAddress { get; set; } = string.Empty;

    [JsonProperty("ip_port", Order = 8)]
    public int IpPort { get; set; }

    [JsonProperty("user_name", Order = 9)]
    public string? UserName { get; set; }

    [JsonProperty("user_password", Order = 10)]
    public string? UserPassword { get; set; }

    [JsonProperty("mode", Order = 13)]
    public EnumCameraMode Mode { get; set; } = EnumCameraMode.NONE;

    [JsonProperty("category", Order = 14)]
    public EnumCameraType Category { get; set; } = EnumCameraType.NONE;

    [JsonProperty("hardware_spec", Order = 15)]
    public ICameraInfoModel? HardwareSpec { get; set; }

    [JsonProperty("urls", Order = 16)]
    public ICameraUrlsModel? Urls { get; set; }

    [JsonProperty("setting", Order = 21)]
    public ICameraSettingModel? Setting { get; set; }

    [JsonProperty("is_record", Order = 22)]
    public bool? IsRecord { get; set; }
}