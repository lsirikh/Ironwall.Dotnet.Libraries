using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public class DeviceDetailModel : UpdateDetailBaseModel, IDeviceDetailModel
{
    public DeviceDetailModel()
    {
    }

    public DeviceDetailModel(int controller, int sensor, int camera, DateTime updateTime)
                            : base(updateTime)
    {
        Controller = controller;
        Sensor = sensor;
        Camera = camera;
    }

    public DeviceDetailModel(IDeviceDetailModel model)
                            : base(model)
    {
        Controller = model.Controller;
        Sensor = model.Sensor;
        Camera = model.Camera;
    }

    public DeviceDetailModel(IDeviceInfoTableMapper model)
                            : base(model)
    {
        Controller = model.Controller;
        Sensor = model.Sensor;
        Camera = model.Camera;
    }


    [JsonProperty("controllers", Order = 1)]
    public int Controller { get; set; }
    [JsonProperty("sensors", Order = 2)]
    public int Sensor { get; set; }
    [JsonProperty("cameras", Order = 3)]
    public int Camera { get; set; }

}
