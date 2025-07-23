using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.MetadataConfigs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:53:39 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MetadataConfigModel : IMetadataConfigModel
    {

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("use_count", Order = 3)]
        public int UseCount { get; set; }

        [JsonProperty("session_timeout", Order = 4)]
        public string SessionTimeout { get; set; }
        [JsonProperty("analytics", Order = 5)]
        public bool Analytics { get; set; }
        [JsonProperty("compression_type", Order = 6)]
        public string CompressionType { get; set; }
        [JsonProperty("geo_location", Order = 7)]
        public bool GeoLocation { get; set; }
        [JsonProperty("shape_polygon", Order = 8)]
        public bool ShapePolygon { get; set; }

        [JsonProperty("ptz_status", Order = 9)]
        public PTZFilterModel PTZStatus { get; set; }

        [JsonProperty("event_subscription", Order = 10)]
        public EventSubscriptionModel EventSubscription { get; set; }

        [JsonProperty("multicast", Order = 11)]
        public MultiCastModel MultiCast { get; set; }
    }
}
