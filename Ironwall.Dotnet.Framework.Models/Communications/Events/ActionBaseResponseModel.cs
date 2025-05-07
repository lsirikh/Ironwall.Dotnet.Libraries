using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/25/2024 5:45:23 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public abstract class ActionBaseResponseModel<T> : ResponseModel, IActionBaseResponseModel<T> where T : MetaEventModel
    {
        #region - Ctors -
        public ActionBaseResponseModel()
        {
            Command = EnumCmdType.EVENT_ACTION_RESPONSE;
        }

        public ActionBaseResponseModel(bool success, string msg, T model = default)
            : base(EnumCmdType.EVENT_ACTION_RESPONSE, success, msg)
        {
            Event = model;
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
        [JsonProperty("request_event", Order = 4)]
        public T? Event { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}