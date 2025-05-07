using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public class DetectionEventModel: MetaEventModel, IDetectionEventModel
    {

        public DetectionEventModel()
        {

        }

        public DetectionEventModel(IDetectionEventMapper model, IBaseDeviceModel device) 
            : base(model, device)
        {
            Result = model.Result;
        }

        public DetectionEventModel(IDetectionEventModel model): base(model)
        {
            Result = model.Result;
        }


        [JsonProperty("result", Order = 6)]
        public int Result { get; set; }
    }
}
