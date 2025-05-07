using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Redis.Message.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications
{
    public abstract class BaseEventMessageModel : BaseMessageModel, IBaseEventMessageModel
    {

        protected BaseEventMessageModel() : base()
        {
        }

        protected BaseEventMessageModel(EnumCmdType cmd) : base(cmd)
        {
        }

        protected BaseEventMessageModel(int id, DateTime dateTime, EnumCmdType cmd) : base(id, cmd, dateTime)
        {
        }

        protected BaseEventMessageModel(IBaseEventMessageModel model) : base(model)
        {
        }
       
    }
}
