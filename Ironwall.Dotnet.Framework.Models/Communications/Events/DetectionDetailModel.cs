using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class DetectionDetailModel : IDetectionDetailModel
    {
        public DetectionDetailModel()
        {
        }

        public DetectionDetailModel(int result)
        {
            Result = result;
        }

        [JsonProperty("result", Order = 1)]
        public int Result { get; set; }

    }
}
