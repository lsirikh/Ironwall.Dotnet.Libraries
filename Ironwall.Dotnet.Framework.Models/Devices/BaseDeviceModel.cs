using Ironwall.Dotnet.Framework.Enums;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public class BaseDeviceModel : BaseModel, IBaseDeviceModel
{
    public BaseDeviceModel()
    {

    }

    public BaseDeviceModel(IDeviceMapperBase model) : base(model)
    {
        DeviceNumber = model.DeviceNumber;
        DeviceName = model.DeviceName;
        DeviceType = model.DeviceType;
        Version = model.Version;
        Status = model.Status;
    }

    public BaseDeviceModel(IBaseDeviceModel model) : base(model)
    {
        DeviceGroups = model.DeviceGroups;
        DeviceNumber = model.DeviceNumber;
        DeviceName = model.DeviceName;
        DeviceType = model.DeviceType;
        Version = model.Version;
        Status = model.Status;
    }

    [JsonProperty("device_groups", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? DeviceGroups { get; set; }
    [JsonProperty("device_number", Order = 2)]
    public int DeviceNumber { get; set; }
    [JsonProperty("device_name", Order = 4)]
    public string DeviceName { get; set; }  = string.Empty;
    [JsonProperty("device_type", Order = 5)]
    public EnumDeviceType DeviceType { get; set; }
    [JsonProperty("version", Order = 6)]
    public string Version { get; set; } = string.Empty;
    [JsonIgnore]
    public EnumDeviceStatus Status { get; set; }
}
