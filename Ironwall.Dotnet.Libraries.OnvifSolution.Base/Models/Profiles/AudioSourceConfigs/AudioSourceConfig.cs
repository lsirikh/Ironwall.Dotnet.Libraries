using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 6:17:14 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*--------------------------- Audio-Source DTO ---------------------------*/
public sealed class AudioSourceConfigDto : ConfigEntityDto
{
    [JsonProperty("source_token", Order = 4)]
    public string SourceToken { get; init; } = default!;

    [JsonProperty("any", Order = 5)]
    public IReadOnlyList<string>? Any { get; init; }
}
