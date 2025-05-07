using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    public class ControllerDataRequestModel : BaseMessageModel, IControllerDataRequestModel
    {

        public ControllerDataRequestModel(EnumCmdType command = EnumCmdType.CONTROLLER_DATA_REQUEST)
         : base(command)
        {
        }

    }
}
