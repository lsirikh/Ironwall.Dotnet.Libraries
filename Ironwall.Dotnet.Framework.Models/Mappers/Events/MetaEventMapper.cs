using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Framework.Models.Communications;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public class MetaEventMapper
        : EventMapperBase, IMetaEventMapper
    {

        public MetaEventMapper()
        {

        }
        public MetaEventMapper(IMetaEventModel model) : base (model)
        {
            EventGroup = model.EventGroup;
            MessageType = (int)model.MessageType;
            Device = model.Device.Id;
            Status = EnumHelper.GetStatusType(model.Status);
        }

        public string EventGroup { get; set; } = string.Empty;
        public int Device { get; set; }
        public bool Status { get; set; }
    }
}
