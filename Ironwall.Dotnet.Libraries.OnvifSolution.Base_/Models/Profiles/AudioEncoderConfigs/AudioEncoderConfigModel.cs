using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioEncoderConfigs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:38:11 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class AudioEncoderConfigModel : IAudioEncoderConfigModel
    {
        public AudioEncoderConfigModel()
        {
            //MultiCast = new MultiCastModel();
        }

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("use_count", Order = 3)]
        public int UseCount { get; set; }

        [JsonProperty("encoding", Order = 4)]
        public EnumAudioEncoding Encoding { get; set; }

        [JsonProperty("bitrate", Order = 5)]
        public int Bitrate { get; set; }

        [JsonProperty("sample_rate", Order = 6)]
        public int SampleRate { get; set; }

        [JsonProperty("session_timeout", Order = 7)]
        public string SessionTimeout { get; set; }

        [JsonProperty("multicast", Order = 8)]
        public MultiCastModel MultiCast { get; set; }
    }
}
