using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 5:00:15 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PTZConfigModel : IPTZConfigModel
    {
        public PTZConfigModel()
        {
            DefaultPTZSpeed = new PTZSpeedModel();
            PanTiltRange = new Space2DModel();
            ZoomRange = new Space1DModel();
            DefaultPTZSpeed = new PTZSpeedModel();
            PTZNode = new PTZNodeModel();
        }

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("token", Order = 2)]
        public string Token { get; set; }

        [JsonProperty("use_count", Order = 3)]
        public int UseCount { get; set; }

        [JsonProperty("pan_tilt_range", Order = 4)]
        public Space2DModel PanTiltRange { get; set; }

        [JsonProperty("zoom_range", Order = 5)]
        public Space1DModel ZoomRange { get; set; }

        [JsonProperty("default_ptz_speed", Order = 6)]
        public PTZSpeedModel DefaultPTZSpeed { get; set; }
        [JsonProperty("timeout", Order = 7)]
        public string Timeout { get; set; }

        [JsonProperty("default_absolute_pan_tilt_position_space", Order = 8)]
        public string DefaultAbsolutePantTiltPositionSpace { get; set; }

        [JsonProperty("default_absolute_zoom_position_space", Order = 9)]
        public string DefaultAbsoluteZoomPositionSpace { get; set; }

        [JsonProperty("default_relative_pan_tilt_translation_space", Order = 10)]
        public string DefaultRelativePanTiltTranslationSpace { get; set; }

        [JsonProperty("default_relative_zoom_translation_space", Order = 11)]
        public string DefaultRelativeZoomTranslationSpace { get; set; }

        [JsonProperty("default_continuous_pan_tilt_velocity_space", Order = 12)]
        public string DefaultContinuousPanTiltVelocitySpace { get; set; }

        [JsonProperty("default_continuous_zoom_velocity_space", Order = 13)]
        public string DefaultContinuousZoomVelocitySpace { get; set; }

        [JsonProperty("ptz_node", Order = 14)]
        public PTZNodeModel PTZNode { get; set; }
    }
}
