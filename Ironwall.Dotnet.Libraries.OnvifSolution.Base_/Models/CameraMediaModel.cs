using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.PTZPresets;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/21/2023 10:34:22 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraMediaModel : ICameraMediaModel
    {

        public CameraMediaModel()
        {
            Profiles = new List<CameraProfile>();
            PTZPresets = new List<PTZPresetModel>();
        }

        [JsonProperty("default_token", Order = 1)]
        public string Token { get; set; }

        [JsonProperty("profiles", Order = 2)]
        public List<CameraProfile> Profiles { get; set; }
        [JsonProperty("presets", Order = 3)]
        public List<PTZPresetModel> PTZPresets { get; set; }
       

        [JsonIgnore]
        public string ProfileTitle { get; set; }
    }
}
