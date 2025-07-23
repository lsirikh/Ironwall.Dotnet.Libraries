using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 1/30/2024 5:23:18 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class AbsoluteFocusModel : IAbsoluteFocusModel
    {
        [JsonProperty("position", Order = 1)]
        public float Position { get; set; }

        [JsonProperty("speed", Order = 2)]
        public float Speed { get; set; }
        [JsonIgnore]
        public bool SpeedSpecified { get; set; }
    }
}
