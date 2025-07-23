using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 10:51:45 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class BaseEventModel : BaseModel, IBaseEventModel
{
    public BaseEventModel()
    {
        DateTime = DateTime.Now;
    }

    protected BaseEventModel(IBaseEventModel model) : base(model.Id)
    {
        MessageType = model.MessageType;
        DateTime = model.DateTime;
    }

    [JsonProperty("type_event", Order = 5)]
    public EnumEventType MessageType { get; set; }

    [JsonProperty("datetime", Order = 20)]
    public DateTime DateTime { get; set; }
}