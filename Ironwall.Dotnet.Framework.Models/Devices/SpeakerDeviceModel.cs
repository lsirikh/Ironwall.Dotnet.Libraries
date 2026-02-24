using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Devices;

public class SpeakerDeviceModel : BaseDeviceModel, ISpeakerDeviceModel
{
    public SpeakerDeviceModel()
    {
        DeviceType = EnumDeviceType.IpSpeaker;
    }

    [JsonProperty("speaker_type", Order = 7)]
    public string SpeakerType { get; set; } = "NORMAL";

    [JsonProperty("description", Order = 8)]
    public string? Description { get; set; }
}
