using Ironwall.Dotnet.Framework.Models.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public class ActionEventMapper
        : EventMapperBase, IActionEventMapper
    {
        public ActionEventMapper()
        {

        }
        public ActionEventMapper(IActionEventModel model) : base(model)
        {
            FromEventId = model.FromEvent.Id;
            Content = model.Content;
            User = model.User;
        }
        [JsonProperty("from_event", Order = 4)]
        public int FromEventId { get; set; }
        [JsonProperty("content", Order = 5)]
        public string Content { get; set; }
        [JsonProperty("user", Order = 6)]
        public string User { get; set; }
    }
}
