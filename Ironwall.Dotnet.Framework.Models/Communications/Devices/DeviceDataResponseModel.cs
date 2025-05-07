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
    public class DeviceDataResponseModel
        : ResponseModel, IDeviceDataResponseModel
    {
        public DeviceDataResponseModel()
        {
            Command = EnumCmdType.DEVICE_DATA_RESPONSE;
        }

        public DeviceDataResponseModel(
            bool success
            , string content
            , List<ControllerDeviceModel> controllers
            , List<SensorDeviceModel> sensors
            , List<CameraDeviceModel> cameras)
            : base(EnumCmdType.DEVICE_DATA_RESPONSE, success, content)
        {
            Controllers = controllers;
            Sensors = sensors;
            Cameras = cameras;
        }

        [JsonProperty("controllers", Order = 4)]
        public List<ControllerDeviceModel> Controllers { get; private set; }
        [JsonProperty("sensors", Order = 5)]
        public List<SensorDeviceModel> Sensors { get; private set; }
        [JsonProperty("cameras", Order = 6)]
        public List<CameraDeviceModel> Cameras { get; private set; }
    }
}
