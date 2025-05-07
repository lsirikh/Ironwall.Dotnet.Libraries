using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class ActionResponseModel
    : ResponseModel, IActionResponseModel
    {
        public ActionResponseModel()
        {
            Command = EnumCmdType.EVENT_ACTION_RESPONSE;
        }

        public ActionResponseModel(bool success, string msg, IActionEventModel model)
            : base(EnumCmdType.EVENT_ACTION_RESPONSE, success, msg)
        {
            Command = EnumCmdType.EVENT_ACTION_RESPONSE;
            Body = model is ActionEventModel eventModel
               ? eventModel : throw new InvalidCastException($"model은 {typeof(ActionEventModel)} 타입이어야 합니다.");
        }


        [JsonProperty("request_model", Order = 4)]
        public ActionEventModel? Body { get; set; }

        

    }
}
