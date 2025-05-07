using Ironwall.Dotnet.Framework.Helpers;
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
    public class ActionRequestModel
        : BaseEventMessageModel, IActionRequestModel
    {
        public ActionRequestModel()
        {
            Command = EnumCmdType.EVENT_ACTION_REQUEST;
        }

        public ActionRequestModel(IActionEventModel model)
            : base(EnumCmdType.EVENT_ACTION_REQUEST)
        {
            Body = model is ActionEventModel eventModel
               ? eventModel : throw new InvalidCastException($"model은 {typeof(ActionEventModel)} 타입이어야 합니다.");
        }

        [JsonProperty("body", Order = 6)]
        public ActionEventModel? Body { get; set; }

    }
}
