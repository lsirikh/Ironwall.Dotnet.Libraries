using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:02:02 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class WideDynamicRangeModel : IWideDynamicRangeModel
    {
        [JsonProperty("mode", Order = 1)]
        public EnumWideDynamicMode Mode { get; set; }

        [JsonProperty("level", Order = 2)]
        public float Level { get; set; }
    }
}
