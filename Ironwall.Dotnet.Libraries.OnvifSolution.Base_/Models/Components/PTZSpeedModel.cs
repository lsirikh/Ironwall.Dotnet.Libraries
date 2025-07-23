using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 1/29/2024 5:59:53 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PTZSpeedModel : IPTZSpeedModel
    {

        #region - Ctors -
        public PTZSpeedModel()
        {
            PanTilt = new Vector2DModel();
            Zoom = new Vector1DModel();
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        [JsonProperty("pan_tilt", Order = 1)]
        public Vector2DModel PanTilt { get; set; }
        [JsonProperty("zoom", Order = 2)]
        public Vector1DModel Zoom { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
