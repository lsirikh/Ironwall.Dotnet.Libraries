using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoEncoderConfigs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:37:17 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class VideoEncoderConfigModel : IVideoEncoderConfigModel
    {
        public VideoEncoderConfigModel()
        {
            Resolution = new ResolutionModel();
            MultiCast = new MultiCastModel();
        }

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("use_count", Order = 3)]
        public int UseCount { get; set; }

        [JsonProperty("encoding", Order = 4)]
        public EnumVideoEncoding Encoding { get; set; }

        [JsonProperty("resolution", Order = 5)]
        public ResolutionModel Resolution { get; set; }

        [JsonProperty("session_timeout", Order = 6)]
        public string SessionTimeout { get; set; }

        [JsonProperty("quality", Order = 7)]
        public float Quality { get; set; }

        [JsonProperty("frame_rate", Order = 8)]
        public int FrameRate { get; set; }

        [JsonProperty("bitrate", Order = 9)]
        public int Bitrate { get; set; }

        [JsonProperty("encoding_interval", Order = 10)]
        public int EncodingInterval { get; set; }
        [JsonProperty("multicast", Order = 11)]
        public MultiCastModel MultiCast { get; set; }
    }
}
