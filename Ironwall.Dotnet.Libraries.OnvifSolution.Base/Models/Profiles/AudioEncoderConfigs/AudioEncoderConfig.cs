using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioEncoderConfigs;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/16/2025 4:18:47 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*───────────────────────────────────────────────────────────────*\
     | 1. Audio-Encoder TOP DTO                                       |
     |    – 기본 필드(Name·UseCount·Token)는                           |
     |      기존 ConfigEntityDto 에서 상속                           |
    \*───────────────────────────────────────────────────────────────*/
public sealed class AudioEncoderConfigDto : ConfigEntityDto
{
    /* ConfigEntityDto 에서 이미            Order = 1,2,3 을 사용했으므로
     * 이어지는 필드는 4 부터 순차 부여                               */

    [JsonProperty("encoding", Order = 4)]
    public EnumAudioEncoding Encoding { get; init; }

    [JsonProperty("bitrate", Order = 5)]
    public int Bitrate { get; init; }

    [JsonProperty("sample_rate", Order = 6)]
    public int SampleRate { get; init; }

    [JsonProperty("session_timeout", Order = 7)]
    public string SessionTimeout { get; init; } = default!;   // ISO-8601 duration (e.g. “PT10S”)

    [JsonProperty("multicast", Order = 8)]
    public MulticastConfigDto? Multicast { get; init; }
}