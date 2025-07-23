using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioEncoderConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.MetadataConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoAnalyticConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoEncoderConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:17:53 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraProfile : ICameraProfile
    {
        public CameraProfile()
        {
            //VideoSourceConfig = new VideoSourceConfigModel();
            //VideoEncoderConfig = new VideoEncoderConfigModel();
            //AudioSourceConfig = new AudioSourceConfigModel();
            //AudioEncoderConfig = new AudioEncoderConfigModel();
            //PTZConfig = new PTZConfigModel();
            //VideoAnalyticsConfig = new VideoAnalyticsConfigModel();
            //MetadataConfig = new MetadataConfigModel();
            //MediaUri = new MediaUriModel();
        }

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("fixed", Order = 3)]
        public bool Fixed { get; set; }

        [JsonProperty("video_source_config", Order = 4)]
        public VideoSourceConfigModel VideoSourceConfig { get; set; }

        [JsonProperty("video_encoder_config", Order = 5)]
        public VideoEncoderConfigModel VideoEncoderConfig { get; set; }

        [JsonProperty("audio_source_config", Order = 6)]
        public AudioSourceConfigModel AudioSourceConfig { get; set; }

        [JsonProperty("audio_encoder_config", Order = 7)]
        public AudioEncoderConfigModel AudioEncoderConfig { get; set; }

        [JsonProperty("ptz_config", Order = 8)]
        public PTZConfigModel PTZConfig { get; set; }

        [JsonProperty("video_analytics_config", Order = 9)]
        public VideoAnalyticsConfigModel VideoAnalyticsConfig { get; set; }

        [JsonProperty("metadata_config", Order = 10)]
        public MetadataConfigModel MetadataConfig { get; set; }

        [JsonProperty("media_uri", Order = 11)]
        public MediaUriModel MediaUri { get; set; }

    }
}
