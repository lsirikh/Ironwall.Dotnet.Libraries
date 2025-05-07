using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Events;

public class ActionDetectionEventModel : ActionEventModel, IActionDetectionEventModel
{
    public ActionDetectionEventModel()
    {
    }
    [JsonProperty("result", Order = 5)]
    public int Result { get; set; }
}
