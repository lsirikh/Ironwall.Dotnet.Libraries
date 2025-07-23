using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.ProfileExtensions.Audio;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 4:24:40 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*--------------------------- Audio-Output ---------------------------*/
public sealed class AudioOutputConfigDto : ConfigEntityDto
{
    [JsonProperty("output_token", Order = 4)]
    public string OutputToken { get; init; } = default!;

    [JsonProperty("send_primacy", Order = 5)]
    public string? SendPrimacy { get; init; }

    [JsonProperty("output_level", Order = 6)]
    public int OutputLevel { get; init; }

    [JsonProperty("any", Order = 7)]
    public IReadOnlyList<string>? Any { get; init; }
}