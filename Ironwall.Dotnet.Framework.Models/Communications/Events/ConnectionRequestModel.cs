using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using Ironwall.Redis.Message.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class ConnectionRequestModel
        : BaseEventMessageModel, IConnectionRequestModel
    {
        public ConnectionRequestModel()
        {
            Command = EnumCmdType.EVENT_DETECTION_REQUEST;
        }


        public ConnectionRequestModel(IConnectionEventModel model)
            : base(EnumCmdType.EVENT_DETECTION_REQUEST)
        {
            Body = model is ConnectionEventModel eventModel
               ? eventModel : throw new InvalidCastException($"model은 {typeof(ConnectionEventModel)} 타입이어야 합니다.");
        }
        [JsonProperty("body", Order = 6)]
        public ConnectionEventModel Body { get; set; }
    }
}
