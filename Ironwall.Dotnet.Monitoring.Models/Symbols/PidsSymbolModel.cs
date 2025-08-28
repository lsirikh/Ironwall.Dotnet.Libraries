using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/26/2025 10:39:19 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class PidsSymbolModel : SymbolModel, IPidsSymbolModel
{
    #region - Ctors -
    public PidsSymbolModel()
    {
        Category = EnumMarkerCategory.PIDS_EQUIPMENT;
    }

    public PidsSymbolModel(string title, double latitude, double longitude, double zoom,
        EnumDeviceType deviceType = EnumDeviceType.Fence) : base(title, latitude, longitude, zoom)
    {
        Category = EnumMarkerCategory.PIDS_EQUIPMENT;
        DeviceType = deviceType;
    }

    
    #endregion
    #region - Properties -
    [JsonProperty("device_id", Order = 20)]
    public int LinkedDeviceId { get; set; }

    [JsonProperty("device_type", Order = 21)]
    public EnumDeviceType DeviceType { get; set; }

    // 모든 PIDS 장비에 공통으로 사용되는 FOV 속성
    [JsonProperty("detection_range", Order = 22)]
    public double DetectionRange { get; set; } = 200; // 미터

    [JsonProperty("detection_angle", Order = 23)]
    public double DetectionAngle { get; set; } = 120; // 도

    [JsonProperty("detection_bearing", Order = 24)]
    public double DetectionBearing { get; set; } = 0; // 도 (북쪽 기준)

    [JsonProperty("show_fov", Order = 25)]
    public bool ShowFOV { get; set; } = false;

    [JsonProperty("fov_color", Order = 26)]
    public EnumColorType FOVColor { get; set; } = EnumColorType.Blue;

    [JsonProperty("fov_opacity", Order = 27)]
    public double FOVOpacity { get; set; } = 0.3;

    [JsonProperty("event_status", Order = 28)]
    public EnumEventStatus EventStatus { get; set; } = EnumEventStatus.Normal;

    public event EventHandler Update;

    public void SetUpdate()
    {
        Update?.Invoke(this, EventArgs.Empty);
    }

    #endregion



}