using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoAnalyticConfigs;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/16/2025 3:28:42 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
#region 0. Video-Analytics Top
/// <summary>
/// ONVIF <see cref="VideoAnalyticsConfiguration"/> 대응 DTO
/// </summary>
public sealed class VideoAnalyticsConfigDto : ConfigEntityDto
{
    [JsonProperty("analytics_engine_cfg", Order = 4)]
    public AnalyticsEngineConfigDto? AnalyticsEngineConfiguration { get; init; }

    [JsonProperty("rule_engine_cfg", Order = 5)]
    public RuleEngineConfigDto? RuleEngineConfiguration { get; init; }
}
#endregion
