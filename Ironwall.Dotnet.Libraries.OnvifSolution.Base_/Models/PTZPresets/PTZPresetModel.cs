using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.PTZPresets
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 1/29/2024 12:59:59 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PTZPresetModel : IPTZPresetModel
    {

        #region - Ctors -
        public PTZPresetModel()
        {
            PTZPosition = new PTZVectorModel();
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
        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }
        [JsonProperty("token", Order = 2)]
        public string Token { get; set; }
        [JsonProperty("ptz_position", Order = 3)]
        public PTZVectorModel PTZPosition { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
