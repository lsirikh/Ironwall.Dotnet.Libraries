using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public class DetectionEventMapper : MetaEventMapper, IDetectionEventMapper
    {

        public DetectionEventMapper()
        {

        }

        public DetectionEventMapper(IDetectionEventModel model) : base (model)
        {
            Result = model.Result;
        }


        public int Result { get; set; }
    }
}
