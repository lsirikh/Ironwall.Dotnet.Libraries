using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/21/2023 10:27:08 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SearchActionResponseModel : ResponseModel, ISearchActionReponseModel
    {
        #region - Ctors -
        public SearchActionResponseModel()
        {
            Command = EnumCmdType.SEARCH_EVENT_ACTION_RESPONSE;
        }

        public SearchActionResponseModel(bool success, string msg
            //, List<IDetectionEventModel> detections
            //, List<IMalfunctionEventModel> malfunctions 
            , List<IActionEventModel> body
            )
             : base(success, msg)
        {
            Command = EnumCmdType.SEARCH_EVENT_ACTION_RESPONSE;
            //Detections = detections.OfType<DetectionEventModel>().ToList();
            //Malfunctions = malfunctions.OfType<MalfunctionEventModel>().ToList();
            Body = body.OfType<ActionEventModel>().ToList();
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
        public List<ActionEventModel>? Body { get; set; }
        //[JsonProperty("detections", Order = 5)]
        //public List<DetectionEventModel> Detections { get; set; }
        //[JsonProperty("malfunctions", Order = 6)]
        //public List<MalfunctionEventModel> Malfunctions { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
