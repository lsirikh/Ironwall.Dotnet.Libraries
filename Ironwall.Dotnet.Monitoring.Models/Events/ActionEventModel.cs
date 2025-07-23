using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/25/2025 7:21:15 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ActionEventModel : BaseEventModel, IActionEventModel
{
    #region - Ctors -
    public ActionEventModel()
    {
        MessageType = EnumEventType.Action;
    }

    public ActionEventModel(IBaseEventModel model) : base(model)
    {
        MessageType = EnumEventType.Action;
    }

    public ActionEventModel(IActionEventModel model) : base(model)
    {
        OriginEvent = model.OriginEvent;
        Content = model.Content;
        User = model.User;
        MessageType = EnumEventType.Action;
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
    [JsonProperty("content", Order = 2)]
    public string? Content { get; set; }
    [JsonProperty("user", Order = 3)]
    public string? User { get; set; }
    [JsonProperty("from_event", Order = 4)]
    [JsonConverter(typeof(EventModelConverter))] 
    public IExEventModel? OriginEvent { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}