using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 2:57:53 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class WhiteBalanceModel : IWhiteBalanceModel
    {
        [JsonProperty("mode", Order = 1)]
        public EnumWhiteBalanceMode Mode { get; set; }

        [JsonProperty("yr_gain", Order = 2)]
        public float YrGain { get; set; }

        [JsonProperty("yb_gain", Order = 3)]
        public float YbGain { get; set; }
    }
}
