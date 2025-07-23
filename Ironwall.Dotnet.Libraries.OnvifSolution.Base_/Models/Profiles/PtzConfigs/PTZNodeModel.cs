using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 5:02:21 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PTZNodeModel : IPTZNode
    {
        public PTZNodeModel()
        {
            AuxiliaryCommands = new List<string>();
        }

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("home_supported", Order = 3)]
        public bool HomeSupported { get; set; }

        [JsonProperty("maximum_number_of_presets", Order = 4)]
        public int MaximumNumberOfPresets { get; set; }

        [JsonProperty("auxiliary_commands", Order = 5)]
        public List<string> AuxiliaryCommands { get; set; }

        [JsonProperty("supported_ptz_spaces", Order = 6)]
        public string SupportedPTZSpaces { get; set; }
    }
}
