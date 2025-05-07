using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/19/2023 3:41:39 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SearchDetectionResponseModel : ResponseModel, ISearchDetectionResponseModel
    {
        #region - Ctors -
        public SearchDetectionResponseModel()
        {
            Command = EnumCmdType.SEARCH_EVENT_DETECTION_RESPONSE;
        }

        public SearchDetectionResponseModel(bool success, string msg, List<IDetectionEventModel> body)
             : base(success, msg)
        {
            Command = EnumCmdType.SEARCH_EVENT_DETECTION_RESPONSE;
            Body = body.OfType<DetectionEventModel>().ToList();
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
        [JsonProperty("body", Order = 4)]
        public List<DetectionEventModel>? Body { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
