using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/12/2025 4:32:17 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CameraPtzCapabilityModel : BaseModel, ICameraPtzCapabilityModel
{
    [JsonProperty("min_pan", Order = 1)]
    public float MinPan { get; set; }

    [JsonProperty("max_pan", Order = 2)]
    public float MaxPan { get; set; }

    [JsonProperty("min_tilt", Order = 3)]
    public float MinTilt { get; set; }

    [JsonProperty("max_tilt", Order = 4)]
    public float MaxTilt { get; set; }

    [JsonProperty("min_zoom", Order = 5)]
    public float MinZoom { get; set; }

    [JsonProperty("max_zoom", Order = 6)]
    public float MaxZoom { get; set; }

    [JsonProperty("horizontal_fov", Order = 7)]
    public float HorizontalFov { get; set; }

    [JsonProperty("vertical_fov", Order = 8)]
    public float VerticalFov { get; set; }

    [JsonProperty("max_visible_distance", Order = 9)]
    public float MaxVisibleDistance { get; set; }

    [JsonProperty("zoom_level", Order = 10)]
    public int ZoomLevel { get; set; }
}