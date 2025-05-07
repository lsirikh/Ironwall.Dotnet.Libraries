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
    public class ModeWindyRequestModel : BaseMessageModel
        , IModeWindyRequestModel
    {
        public ModeWindyRequestModel()
        {
            Command = EnumCmdType.MODE_WINDY_REQUEST;
        }

        public ModeWindyRequestModel(IModeWindyEventModel model) : base(EnumCmdType.MODE_WINDY_REQUEST)
        {
        }


        [JsonProperty("mode_windy", Order = 1)]
        public EnumWindyMode ModeWindy { get; set; }
    }
}
