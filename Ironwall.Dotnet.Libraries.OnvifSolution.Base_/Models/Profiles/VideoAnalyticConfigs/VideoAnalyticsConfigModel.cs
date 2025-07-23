using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoAnalyticConfigs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:50:12 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class VideoAnalyticsConfigModel : IVideoAnalyticsConfigModel
    {
        public VideoAnalyticsConfigModel()
        {
            Analytics = new List<string>();
        }
        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("use_count", Order = 3)]
        public int UseCount { get; set; }

        [JsonProperty("analytics", Order = 4)]
        public List<string> Analytics { get; set; }
    }
}
