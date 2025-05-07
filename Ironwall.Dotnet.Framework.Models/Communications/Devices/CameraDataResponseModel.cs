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
    public class CameraDataResponseModel
        : ResponseModel, ICameraDataResponseModel
    {
        public CameraDataResponseModel()
        {
            Command = EnumCmdType.CAMERA_DATA_RESPONSE;
        }
        public CameraDataResponseModel(bool success, string content, List<CameraDeviceModel> body)
            : base(EnumCmdType.CAMERA_DATA_RESPONSE, success, content)
        {
            Body = body;
        }
        [JsonProperty("body", Order = 4)]
        public List<CameraDeviceModel> Body { get; private set; }
    }
}
