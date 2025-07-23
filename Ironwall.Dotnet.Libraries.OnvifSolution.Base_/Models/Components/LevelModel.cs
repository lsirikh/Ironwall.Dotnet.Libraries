using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/22/2023 11:01:11 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class LevelModel : ILevelModel
    {
        [JsonProperty("min", Order = 1)]
        public float Min { get; set; }
        [JsonProperty("max", Order = 1)]
        public float Max { get; set; }
    }
}
