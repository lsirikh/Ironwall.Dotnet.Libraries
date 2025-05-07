using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public class SensorDeviceModel : BaseDeviceModel, ISensorDeviceModel
{
    public SensorDeviceModel()
    {
        Controller = new ControllerDeviceModel();
    }
    public SensorDeviceModel(IDeviceMapperBase model) : base(model)
    {
    }

    public SensorDeviceModel(IDeviceMapperBase model, IControllerDeviceModel controller): base(model)
    {
        Controller = controller as ControllerDeviceModel;
    }

    [JsonProperty("controller", Order = 6)]
    public ControllerDeviceModel? Controller { get; set; } = null;
}
