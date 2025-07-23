using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.MetadataConfigs;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 10:16:58 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*---------------------------  METADATA-CONFIG DTOs  ---------------------------*/
public sealed class MetadataConfigDto : ConfigEntityDto
{
    /*---------------------------  Core fields  ---------------------------*/
    [JsonProperty("ptz_status", Order = 4)]
    public PtzFilterDto? PTZStatus { get; init; }

    [JsonProperty("events", Order = 5)]
    public EventSubscriptionDto? Events { get; init; }

    [JsonProperty("analytics_enabled", Order = 6)]
    public bool? Analytics { get; init; }

    [JsonProperty("multicast", Order = 7)]
    public MulticastConfigDto? Multicast { get; init; }

    [JsonProperty("session_timeout", Order = 8)]
    public string? SessionTimeout { get; init; }

    [JsonProperty("analytics_engine", Order = 9)]
    public AnalyticsEngineConfigDto? AnalyticsEngineConfiguration { get; init; }

    [JsonProperty("compression_type", Order = 10)]
    public string? CompressionType { get; init; }

    [JsonProperty("geo_location", Order = 11)]
    public bool? GeoLocation { get; init; }

    [JsonProperty("shape_polygon", Order = 12)]
    public bool? ShapePolygon { get; init; }
}