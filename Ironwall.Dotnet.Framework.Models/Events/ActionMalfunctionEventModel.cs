using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Events;

public class ActionMalfunctionEventModel : ActionEventModel, IActionMalfunctionEventModel
{
    public ActionMalfunctionEventModel()
    {

    }
    [JsonProperty("reason", Order = 5)]
    public int Reason { get; set; }
    [JsonProperty("first_start", Order = 6)]
    public int FirstStart { get; set; }
    [JsonProperty("first_end", Order = 7)]
    public int FirstEnd { get; set; }
    [JsonProperty("second_start", Order = 8)]
    public int SecondStart { get; set; }
    [JsonProperty("second_end", Order = 9)]
    public int SecondEnd { get; set; }


}
