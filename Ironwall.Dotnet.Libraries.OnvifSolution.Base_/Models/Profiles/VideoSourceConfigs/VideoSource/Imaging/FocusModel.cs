using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 2:26:38 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class FocusModel : IFocusModel
    {
        [JsonProperty("auto_focus_mode", Order = 1)]
        public EnumAutoFocusMode AutoFocusMode { get; set; }

        [JsonProperty("default_speed", Order = 2)]
        public float DefaultSpeed { get; set; }

        [JsonProperty("near_limit", Order = 3)]
        public float NearLimit { get; set; }

        [JsonProperty("far_limit", Order = 4)]
        public float FarLimit { get; set; }
    }
}
