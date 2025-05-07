using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Models.Events;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/25/2024 5:29:30 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public abstract class ActionBaseRequestModel<T> : BaseEventMessageModel, IActionBaseRequestModel<T> where T : MetaEventModel
    {
        #region - Ctors -
        public ActionBaseRequestModel()
        {
            Command = EnumCmdType.EVENT_ACTION_REQUEST;
        }

        public ActionBaseRequestModel(EnumCmdType cmd, string content, string user, T model)
            : base(model.Id, model.DateTime, cmd)
        {
            Event = model;
            Content = content;
            User = user;
        }


        public ActionBaseRequestModel(EnumCmdType cmd, IActionEventModel model)
            : base(model.Id, model.DateTime, cmd)
        {
            Id = model.Id;
            Event = (T)model.FromEvent;
            Content = model.Content;
            User = model.User;
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
        [JsonProperty("reference_event", Order = 6)]
        public T? Event { get; set; }
        [JsonProperty("content", Order = 7)]
        public string Content { get; set; } = string.Empty;
        [JsonProperty("user", Order = 8)]
        public string User { get; set; } = string.Empty;
        #endregion
        #region - Attributes -
        #endregion
    }
}