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
    public class MalfunctionRequestModel
    : BaseMessageModel, IMalfunctionRequestModel
    {
        public MalfunctionRequestModel()
        {
            Command = EnumCmdType.EVENT_MALFUNCTION_REQUEST;
        }

        public MalfunctionRequestModel(IMalfunctionEventModel model) 
            : base(EnumCmdType.EVENT_MALFUNCTION_REQUEST)
        {
            Body = model is MalfunctionEventModel eventModel ? eventModel : throw new InvalidCastException($"model은 {typeof(MalfunctionEventModel)} 타입이어야 합니다.");
        }

        [JsonProperty("detail", Order = 6)]
        public MalfunctionEventModel? Body { get; set; }

    }
}
