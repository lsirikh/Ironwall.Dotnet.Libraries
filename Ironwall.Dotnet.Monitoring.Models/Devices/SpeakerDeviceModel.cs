using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public class SpeakerDeviceModel : BaseDeviceModel, ISpeakerDeviceModel
{
    public SpeakerDeviceModel()
    {
        DeviceType = EnumDeviceType.IpSpeaker;
    }

    public SpeakerDeviceModel(ISpeakerDeviceModel model) : base(model)
    {
        SpeakerType = model.SpeakerType;
        Description = model.Description;
        Server = model.Server;
    }

    [JsonProperty("speaker_type", Order = 7)]
    public string SpeakerType { get; set; } = "NORMAL";

    [JsonProperty("description", Order = 8)]
    public string? Description { get; set; }

    [JsonIgnore]
    public IServerModel? Server { get; set; }
}
