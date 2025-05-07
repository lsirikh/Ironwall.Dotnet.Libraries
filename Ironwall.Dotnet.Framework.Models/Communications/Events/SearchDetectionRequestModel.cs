using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/19/2023 3:33:16 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SearchDetectionRequestModel : UserSessionBaseRequestModel, ISearchDetectionRequestModel
    {
        #region - Ctors -
        public SearchDetectionRequestModel()
        {
            Command = EnumCmdType.SEARCH_EVENT_DETECTION_REQUEST;
        }
        public SearchDetectionRequestModel(string startTime, string endTime, ILoginSessionModel model)
        : base(model)
        {
            Command = EnumCmdType.SEARCH_EVENT_DETECTION_REQUEST;
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
