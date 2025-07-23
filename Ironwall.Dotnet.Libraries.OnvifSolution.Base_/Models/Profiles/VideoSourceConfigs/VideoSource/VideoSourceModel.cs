using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:08:01 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class VideoSourceModel : IVideoSourceModel
    {
        public VideoSourceModel()
        {
            Resolution = new ResolutionModel();
            //ImagingOption = new ImagingOptionModel();
        }

        [JsonProperty("token", Order = 1)]
        public string Token { get; set; }

        [JsonProperty("frame_rate", Order = 2)]
        public float FrameRate { get; set; }

        [JsonProperty("resolution", Order = 3)]
        public ResolutionModel Resolution { get; set; }

        [JsonProperty("imaging_option", Order = 4)]
        public ImagingOptionModel ImagingOption { get; set; }
    }
}
