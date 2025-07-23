using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/12/2025 4:43:29 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 카메라의 광학 및 물리적 사양 (시야각 및 거리 계산에 활용)
/// </summary>
public class CameraOpticsModel : BaseModel, ICameraOpticsModel
{
    [JsonProperty("zoom_level", Order = 1)]
    public float ZoomLevel { get; set; } // 예: 1~50배

    [JsonProperty("focal_length_mm", Order = 2)]
    public float FocalLength { get; set; } // 단위 mm

    [JsonProperty("sensor_width_mm", Order = 3)]
    public float SensorWidth { get; set; } // 단위 mm

    [JsonProperty("sensor_height_mm", Order = 4)]
    public float SensorHeight { get; set; } // 단위 mm

    [JsonProperty("horizontal_fov_deg", Order = 5)]
    public float HorizontalFOV => 2 * (float)(Math.Atan((SensorWidth / (2 * FocalLength))) * (180 / Math.PI));

    [JsonProperty("view_distance_m", Order = 6)]
    public float ViewDistance => ZoomLevel * 10; // 예: 줌 30x이면 300m
}