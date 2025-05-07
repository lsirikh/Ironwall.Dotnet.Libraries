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
    public class DeviceInfoResponseModel
        : ResponseModel, IDeviceInfoResponseModel
    {
        public DeviceInfoResponseModel()
        {
            Command = EnumCmdType.DEVICE_INFO_RESPONSE;
        }

        public DeviceInfoResponseModel(bool success, string content, IDeviceDetailModel detail)
            : base(success, content)
        {
            Command = EnumCmdType.DEVICE_INFO_RESPONSE;
            Detail = ResponseFactory.Build<DeviceDetailModel>(detail);
        }

        [JsonProperty("detail", Order = 4)]
        public DeviceDetailModel Detail { get; private set; }
    }
}
