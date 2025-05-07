using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/20/2023 3:21:57 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SearchMalfunctionResponseModel : ResponseModel, ISearchMalfunctionResponseModel
    {
        #region - Ctors -
        public SearchMalfunctionResponseModel()
        {
            Command = EnumCmdType.SEARCH_EVENT_MALFUNCTION_RESPONSE;
        }
        public SearchMalfunctionResponseModel(bool success, string msg, List<IMalfunctionEventModel> body)
             : base(success, msg)
        {
            Command = EnumCmdType.SEARCH_EVENT_MALFUNCTION_RESPONSE;
            Body = body.OfType<MalfunctionEventModel>().ToList(); ;
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
        public List<MalfunctionEventModel>? Body { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
