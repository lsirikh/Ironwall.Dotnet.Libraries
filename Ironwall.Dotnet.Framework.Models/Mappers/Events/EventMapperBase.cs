using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Framework.Models.Communications;
using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public abstract class EventMapperBase : BaseModel, IEventMapperBase
    {

        public EventMapperBase()
        {
            Datetime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ff");
        }

        public EventMapperBase(IBaseEventModel model) : base(model.Id)
        {
            MessageType = (int)model.MessageType;
            Datetime = model.DateTime.ToString("yyyy-MM-dd HH:mm:ss.ff");
        }

        public EventMapperBase(IBaseEventMessageModel model) : base(model.Id)
        {
            MessageType = (int)EnumHelper.GetEventType(model.Command);
            Datetime = model.Datetime.ToString("yyyy-MM-dd HH:mm:ss.ff");
        }
        [JsonProperty("type_event", Order = 2)]
        public int MessageType { get; set; }
        [JsonProperty("datetime", Order = 3)]
        public string Datetime { get; set; }
    }
}
