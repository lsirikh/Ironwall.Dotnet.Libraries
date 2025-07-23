using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 2:24:56 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ExposureModel : IExposureModel
    {
        public ExposureModel()
        {
            ExposureTimeRange = new LevelModel();
            GainRange = new LevelModel();
            IrisRange = new LevelModel();
        }

        [JsonProperty("mode", Order = 1)]
        public EnumExposureMode Mode { get; set; }

        [JsonProperty("priority", Order = 2)]
        public EnumExposurePriority Priority { get; set; }

        [JsonProperty("exposure_time", Order = 3)]
        public float ExposureTime { get; set; }

        [JsonProperty("exposure_time_range", Order = 4)]
        public LevelModel ExposureTimeRange { get; set; }

        [JsonProperty("gain", Order = 5)]
        public float Gain { get; set; }

        [JsonProperty("gain_range", Order = 6)]
        public LevelModel GainRange { get; set; }

        [JsonProperty("iris", Order = 7)]
        public float Iris { get; set; }

        [JsonProperty("iris_range", Order = 8)]
        public LevelModel IrisRange { get; set; }
    }
}
