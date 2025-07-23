using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 4:57:48 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*--------------------------- Video-Source ----------------------------*/
public sealed class VideoSourceDto : DeviceEntityDto
{
    [JsonProperty("fps", Order = 2)]
    public float Framerate { get; init; }

    [JsonProperty("resolution", Order = 3)]
    public VideoResolutionDto Resolution { get; init; } = default!;

    [JsonProperty("imaging", Order = 4)]
    public ImagingSettingsDto? Imaging { get; init; }

    [JsonProperty("ext", Order = 5)]
    public VideoSourceExtensionDto? Extension { get; init; }
}