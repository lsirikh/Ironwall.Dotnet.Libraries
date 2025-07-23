using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioEncoderConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.ProfileExtensions.Audio;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.ProfileExtensions;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 4:24:24 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*--------------------------- DTO ---------------------------*/
public sealed class ProfileExtensionDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }

    [JsonProperty("audio_output_cfg", Order = 2)]
    public AudioOutputConfigDto? AudioOutputConfiguration { get; init; }

    [JsonProperty("audio_decoder_cfg", Order = 3)]
    public AudioDecoderConfigDto? AudioDecoderConfiguration { get; init; }

    [JsonProperty("ext", Order = 4)]
    public ProfileExtension2Dto? Extension { get; init; }
}