using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public class ModeWindyEventModel : BaseEventModel , IModeWindyEventModel
    {
        public ModeWindyEventModel()
        {

        }

        public ModeWindyEventModel(IModeWindyRequestModel model)
        {
            ModeWindy = model.ModeWindy;
        }



        public EnumWindyMode ModeWindy { get; set; }
    }
}
