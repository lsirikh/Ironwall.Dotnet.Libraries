using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 4:59:18 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PTZSpeedModel : IPTZSpeedModel
    {
        public PTZSpeedModel()
        {
            PanTilt = new Vector2DModel();
            Zoom = new Vector1DModel();
        }
        [JsonProperty("pan_tilt", Order = 1)]
        public Vector2DModel PanTilt { get; set; }

        [JsonProperty("zoom", Order = 2)]
        public Vector1DModel Zoom { get; set; }

    }
}
