using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 12:51:03 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

/*---------------------------  PTZ-Configuration DTO  ---------------------------*/
public sealed class PTZConfigDto : ConfigEntityDto
{
    /*- 기본 URI/토큰 공간 필드 -*/
    [JsonProperty("node_token", Order = 4)] public string NodeToken { get; init; } = default!;
    [JsonProperty("abs_pan_tilt_pos_space", Order = 5)] public string? AbsPanTiltPosSpace { get; init; }
    [JsonProperty("abs_zoom_pos_space", Order = 6)] public string? AbsZoomPosSpace { get; init; }
    [JsonProperty("rel_pan_tilt_trans_space", Order = 7)] public string? RelPanTiltTransSpace { get; init; }
    [JsonProperty("rel_zoom_trans_space", Order = 8)] public string? RelZoomTransSpace { get; init; }
    [JsonProperty("cont_pan_tilt_vel_space", Order = 9)] public string? ContPanTiltVelSpace { get; init; }
    [JsonProperty("cont_zoom_vel_space", Order = 10)] public string? ContZoomVelSpace { get; init; }

    /*- 기본 속도/타임아웃 -*/
    [JsonProperty("default_speed", Order = 11)] public PtzSpeedDto? DefaultSpeed { get; init; }
    [JsonProperty("default_timeout", Order = 12)] public string? DefaultTimeout { get; init; }

    /*- 제한 범위 -*/
    [JsonProperty("pan_tilt_limits", Order = 13)] public PanTiltLimitsDto? PanTiltLimits { get; init; }
    [JsonProperty("zoom_limits", Order = 14)] public ZoomLimitsDto? ZoomLimits { get; init; }

    /*- 확장 영역 -*/
    [JsonProperty("extension", Order = 15)] public PtzConfigExtDto? Extension { get; init; }

    /*- 램프값(옵션) -*/
    [JsonProperty("move_ramp", Order = 16)] public int? MoveRamp { get; init; }
    [JsonProperty("preset_ramp", Order = 17)] public int? PresetRamp { get; init; }
    [JsonProperty("preset_tour_ramp", Order = 18)] public int? PresetTourRamp { get; init; }
    [JsonProperty("ptz_node", Order = 18)] public PTZNodeDto? PTZNode { get; set; }
}

/*---------------------------  PTZ-Config-Extension DTOs  ---------------------------*/
public sealed class PtzConfigExt2Dto
{
    [JsonProperty("any", Order = 1)] public IReadOnlyList<string>? Any { get; init; }
}

public sealed class PtzConfigExtDto
{
    [JsonProperty("any", Order = 1)] public IReadOnlyList<string>? Any { get; init; }
    [JsonProperty("pt_control", Order = 2)] public PtControlDirectionDto? PTControlDirection { get; init; }
    [JsonProperty("ext", Order = 3)] public PtzConfigExt2Dto? Extension { get; init; }
}