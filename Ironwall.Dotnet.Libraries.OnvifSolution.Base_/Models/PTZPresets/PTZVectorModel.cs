using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.PTZPresets
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 1/29/2024 1:01:42 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PTZVectorModel : IPTZVectorModel
    {

        #region - Ctors -
        public PTZVectorModel()
        {
            Zoom = new Vector1DModel();
            PanTilt = new Vector2DModel();
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
        [JsonProperty("zoom", Order = 1)]
        public Vector1DModel Zoom { get; set; }
        [JsonProperty("pan_tilt", Order = 2)]
        public Vector2DModel PanTilt { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
