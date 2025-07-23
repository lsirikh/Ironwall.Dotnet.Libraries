using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs.AudioSource
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:26:33 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class AudioSourceModel : IAudioSourceModel
    {
        [JsonProperty("token", Order = 1)]
        public string Token { get; set; }

        [JsonProperty("channels", Order = 2)]
        public int Channels { get; set; }
    }
}
