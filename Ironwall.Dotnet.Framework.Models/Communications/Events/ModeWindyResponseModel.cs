using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class ModeWindyResponseModel
        : ResponseModel, IModeWindyResponseModel
    {
        public ModeWindyResponseModel()
        {

        }

        public ModeWindyResponseModel(bool success, string msg, IModeWindyRequestModel model)
            : base(success, msg)
        {
            Command = EnumCmdType.MODE_WINDY_RESPONSE;
            RequestModel = model is ModeWindyRequestModel eventModel ? eventModel : throw new InvalidCastException($"model은 {typeof(ModeWindyRequestModel)} 타입이어야 합니다.");
        }

        [JsonProperty("request_model", Order = 4)]
        public ModeWindyRequestModel? RequestModel { get; set; }
    }
}
