using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Models.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class ConnectionResponseModel
        : ResponseModel, IConnectionResponseModel
    {
        public ConnectionResponseModel()
        {

        }

        public ConnectionResponseModel(bool success, string msg, IConnectionRequestModel model = null)
            : base (success, msg)
        {
            Command = EnumCmdType.EVENT_CONNECTION_RESPONSE;
            RequestModel = model is ConnectionRequestModel eventModel
               ? eventModel : throw new InvalidCastException($"model은 {typeof(ConnectionRequestModel)} 타입이어야 합니다.");
        }

        [JsonProperty("request_model", Order = 4)]
        public ConnectionRequestModel RequestModel { get; set; }
    }
}
