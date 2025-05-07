using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    public class CameraDataRequestModel : BaseMessageModel, ICameraDataRequestModel
    {
        public CameraDataRequestModel(EnumCmdType command = EnumCmdType.CAMERA_DATA_REQUEST)
            :base(EnumCmdType.CAMERA_DATA_REQUEST)
        {
        }
    }
}
