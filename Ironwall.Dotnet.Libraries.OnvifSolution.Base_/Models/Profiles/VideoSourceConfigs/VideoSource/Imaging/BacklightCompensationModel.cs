using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 2:52:11 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class BacklightCompensationModel : IBacklightCompensationModel
    {
        [JsonProperty("mode", Order = 1)]
        public EnumBacklightCompensationMode Mode { get; set; }
        [JsonProperty("level", Order = 2)]
        public float Level { get; set; }
        [JsonProperty("level_range", Order = 3)]
        public LevelModel LevelRange { get; set; }
        [JsonProperty("level_specified", Order = 4)]
        public bool LevelSpecified { get; set; }
    }
}
