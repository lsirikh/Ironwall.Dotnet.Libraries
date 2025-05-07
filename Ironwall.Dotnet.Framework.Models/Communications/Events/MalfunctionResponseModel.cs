using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class MalfunctionResponseModel
    : ResponseModel, IMalfunctionResponseModel
    {
        public MalfunctionResponseModel()
        {
        }

        public MalfunctionResponseModel(bool success, string msg, IMalfunctionRequestModel model = null)
            : base(success, msg)
        {
            Command = EnumCmdType.EVENT_MALFUNCTION_RESPONSE;
            RequestModel = model is MalfunctionRequestModel eventModel ? eventModel : throw new InvalidCastException($"model은 {typeof(MalfunctionRequestModel)} 타입이어야 합니다.");
        }

        [JsonProperty("request_model", Order = 4)]
        public MalfunctionRequestModel? RequestModel { get; set; }

    }
}
