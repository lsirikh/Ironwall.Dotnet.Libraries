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
        Presets = new List<ICameraPresetModel>();
        DeviceType = EnumDeviceType.IpCamera;
    }

    public CameraDeviceModel(int id) : this()
    {
        Id = id;
    }

    [JsonProperty("ip_address", Order = 7)]
    public string IpAddress { get; set; } = string.Empty;

    [JsonProperty("ip_port", Order = 8)]
    public int Port { get; set; }

    [JsonProperty("user_name", Order = 9)]
    public string? Username { get; set; }

    [JsonProperty("user_password", Order = 10)]
    public string? Password { get; set; }

    [JsonProperty("rtsp_uri", Order = 11)]
    public string? RtspUri { get; set; }

    [JsonProperty("rtsp_port", Order = 12)]
    public int RtspPort { get; set; }

    [JsonProperty("mode", Order = 13)]
    public EnumCameraMode Mode { get; set; } = EnumCameraMode.NONE;

    [JsonProperty("category", Order = 14)]
    public EnumCameraType Category { get; set; } = EnumCameraType.NONE;

    [JsonProperty("identification", Order = 15)]
    public ICameraInfoModel? Identification { get; set; }

    [JsonProperty("ptz_capability", Order = 16)]
    public ICameraPtzCapabilityModel? PtzCapability { get; set; }

    [JsonProperty("position", Order = 17)]
    public ICameraPositionModel? Position { get; set; }

    [JsonProperty("presets", Order = 18)]
    public List<ICameraPresetModel>? Presets { get; set; }

    [JsonProperty("optics", Order = 19)]
    public ICameraOpticsModel? Optics { get; set; }
}