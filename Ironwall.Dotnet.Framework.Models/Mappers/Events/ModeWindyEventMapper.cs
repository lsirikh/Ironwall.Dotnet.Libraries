using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public class ModeWindyEventMapper
        : EventMapperBase, IModeWindyEventMapper
    {
        public ModeWindyEventMapper()
        {

        }

        public ModeWindyEventMapper(IModeWindyEventModel model)
            : base(model)
        {
            ModeWindy = model.ModeWindy;
        }

        public ModeWindyEventMapper(IModeWindyRequestModel model)
        {
            ModeWindy = model.ModeWindy;
        }

        public EnumWindyMode ModeWindy { get; set; }
    }
}
