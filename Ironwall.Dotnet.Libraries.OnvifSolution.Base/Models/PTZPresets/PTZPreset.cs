using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.PTZPresets;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 7:54:02 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*---------------------------  PTZ-Preset DTO  ---------------------------*/
public sealed class PTZPresetDto
{
    [JsonProperty("name", Order = 1)] public string Name { get; init; } = default!;
    [JsonProperty("token", Order = 2)] public string Token { get; init; } = default!;

    /* PTZVector → Position */
    [JsonProperty("ptz_pos", Order = 3)]
    public PtzVectorDto? Position { get; init; }
}