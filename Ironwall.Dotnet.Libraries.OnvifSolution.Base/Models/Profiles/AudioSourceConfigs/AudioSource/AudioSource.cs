using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs.AudioSource;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 1:56:30 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*--------------------------- Audio-Source DTO ---------------------------*/
public sealed class AudioSourceDto
{
    [JsonProperty("token", Order = 1)]
    public string Token { get; init; } = default!;

    [JsonProperty("channels", Order = 2)]
    public int Channels { get; init; }

    [JsonProperty("any", Order = 3)]
    public IReadOnlyList<string>? Any { get; init; }
}