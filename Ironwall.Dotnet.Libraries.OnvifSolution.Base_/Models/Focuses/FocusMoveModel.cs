using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Focuses
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 1/30/2024 5:22:41 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class FocusMoveModel : IFocusMoveModel
    {

        #region - Ctors -
        public FocusMoveModel()
        {
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
        [JsonProperty("absolute_focus", Order = 1)]
        public AbsoluteFocusModel AbsoluteFocus { get; set; }
        [JsonProperty("relative_focus", Order = 2)]
        public RelativeFocusModel RelativeFocus { get; set; }
        [JsonProperty("continuous_focus", Order = 3)]
        public ContinuousFocusModel ContinuousFocus { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
