using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/21/2023 10:21:32 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SearchActionRequestModel : UserSessionBaseRequestModel, ISearchActionRequestModel
    {
        #region - Ctors -
        public SearchActionRequestModel()
        {
            Command = EnumCmdType.SEARCH_EVENT_ACTION_REQUEST;
        }
        public SearchActionRequestModel(string startTime, string endTime, ILoginSessionModel model)
            : base(model)
        {
            Command = EnumCmdType.SEARCH_EVENT_ACTION_REQUEST;
            StartDateTime = startTime;
            EndDateTime = endTime;
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
        [JsonProperty("start_date_time", Order = 1)]
        public string StartDateTime { get; set; } = string.Empty;
        [JsonProperty("end_date_time", Order = 2)]
        public string EndDateTime { get; set; } = string.Empty;
        #endregion
        #region - Attributes -
        #endregion
    }
}
