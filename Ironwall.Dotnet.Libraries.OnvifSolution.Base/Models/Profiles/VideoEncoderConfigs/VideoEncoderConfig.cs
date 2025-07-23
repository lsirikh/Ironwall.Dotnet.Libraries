using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoEncoderConfigs;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/16/2025 4:57:26 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class VideoEncoderConfigDto : ConfigEntityDto
{
    /*─ 고유 필드 ───────────────────────────────────────────────*/
    [JsonProperty("encoding", Order = 4)]
    public EnumVideoEncoding Encoding { get; init; }

    [JsonProperty("resolution", Order = 5)]
    public VideoResolutionDto Resolution { get; init; } = default!;

    [JsonProperty("quality", Order = 6)]
    public float Quality { get; init; }

    [JsonProperty("rate_control", Order = 7)]
    public VideoRateControlDto RateControl { get; init; } = default!;

    [JsonProperty("mpeg4", Order = 8)]
    public Mpeg4ConfigDto? Mpeg4 { get; init; }

    [JsonProperty("h264", Order = 9)]
    public H264ConfigDto? H264 { get; init; }

    [JsonProperty("multicast", Order = 10)]
    public MulticastConfigDto? Multicast { get; init; }

    [JsonProperty("session_timeout", Order = 11)]
    public string? SessionTimeout { get; init; }

    [JsonProperty("guaranteed_fps", Order = 12)]
    public bool? GuaranteedFrameRate { get; init; }
}